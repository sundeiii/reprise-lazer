// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play.HUD.Counters
{
    /// <summary>
    /// Reprise: Happy_24's BTMC tosu pp counter (its Default skin and settings), in the game: a box outlined in yellow with the map's background,
    /// title, difficulty, star rating and BPM/CS/AR/OD/HP on the left, its 100s, 50s and misses and pp / full combo pp on the right, a mods tab
    /// behind it when the play has mods (other than Relax), and the map's difficulty graph hanging under it, filled as far into the song as the
    /// play is, with a mark for each 100, 50 and miss. Laid out as the overlay's page is (710×160, in its CSS pixels).
    /// </summary>
    public partial class BtmcPerformancePointsCounter : CompositeDrawable, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        private static readonly Color4 gradient1 = Color4Extensions.FromHex("1D3860");
        private static readonly Color4 gradient2 = Color4Extensions.FromHex("072045");
        private static readonly Color4 outline = Color4Extensions.FromHex("F3F589");
        private static readonly Color4 dashes = Color4Extensions.FromHex("254982");
        private static readonly Color4 hundred = Color4Extensions.FromHex("6EF063");
        private static readonly Color4 fifty = Color4Extensions.FromHex("F0E263");
        private static readonly Color4 miss = Color4Extensions.FromHex("F06363");
        private static readonly Color4 graphColour = Color4Extensions.FromHex("2A8270");

        private const float box_x = 100;
        private const float box_width = 570;
        private const float box_height = 92;
        private const float border = 4.8f;
        private const float graph_x = 107;
        private const float graph_width = 549;
        private const float graph_height = 64;

        // The left side (the background, title and map's numbers), in the box.
        private const float left_x = 104 - box_x;
        private const float left_width = 337.2f;
        private const float left_height = 88;

        private readonly LivePlayStats stats = new LivePlayStats();

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        [Resolved]
        private IBindable<WorkingBeatmap>? working { get; set; }

        [Resolved]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved]
        private BeatmapDifficultyCache? difficultyCache { get; set; }

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        private Container modsTab = null!;
        private Marquee modsText = null!;
        private Container background = null!;
        private Marquee title = null!;
        private Marquee difficulty = null!;
        private Container starBox = null!;
        private Box starBoxColour = null!;
        private OsuSpriteText starText = null!;
        private SpriteIcon starIcon = null!;
        private OdometerText bpm = null!;
        private OsuSpriteText cs = null!;
        private OsuSpriteText ar = null!;
        private OsuSpriteText od = null!;
        private OsuSpriteText hp = null!;
        private CountingSpriteText oks = null!;
        private CountingSpriteText mehs = null!;
        private CountingSpriteText misses = null!;
        private OdometerText pp = null!;
        private OdometerText fullComboPp = null!;
        private StrainChart darker = null!;
        private StrainChart lighter = null!;
        private Container progress = null!;
        private Container judgements = null!;

        private double rate = 1;
        private int lastSliderBreaks;

        public BtmcPerformancePointsCounter()
        {
            Size = new Vector2(710, 160);
            Scale = new Vector2(0.6f);
        }

        // A CSS line box of text: its font's line centred on it.
        private static float lineTop(float cssTop, float cssLineHeight, float size, float fontLineHeight) => cssTop + (cssLineHeight - fontLineHeight * size) / 2;

        private static OsuSpriteText text(float size, bool medium = false) => new OsuSpriteText
        {
            Font = medium ? TosuFonts.BalooMedium(size) : TosuFonts.BalooBold(size),
        };

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            InternalChildren = new Drawable[]
            {
                stats,
                // The mods tab, behind the box: slid out to the left while the play has mods (not just Relax), else tucked under it.
                modsTab = new Container
                {
                    Size = new Vector2(103, 104),
                    X = 20 + 103,
                    Children = new Drawable[]
                    {
                        new Sprite
                        {
                            Position = new Vector2(-10, 14),
                            Size = new Vector2(103 * 1.1f, 103 * 1.1f * 236 / 407),
                            Texture = textures.Get(@"Btmc/mods"),
                        },
                        new Container
                        {
                            Position = new Vector2(21, 25 - 0),
                            AutoSizeAxes = Axes.Both,
                            Child = place(text(16).With(t => t.Text = @"Mods"), 0, 16, 16, TosuFonts.BALOO_BOLD_LINE_HEIGHT),
                        },
                        modsText = new Marquee(TosuFonts.BalooBold(11.2f), TosuFonts.BALOO_BOLD_LINE_HEIGHT, 70)
                        {
                            Position = new Vector2(13, lineTop(50, 11.2f, 11.2f, TosuFonts.BALOO_BOLD_LINE_HEIGHT)),
                        },
                    },
                },
                // The box: its top edge cut off (the overlay's "Cut off position: Top"), outlined on the other three, rounded at the bottom.
                new Container
                {
                    Position = new Vector2(box_x, 0),
                    Size = new Vector2(box_width, box_height),
                    Masking = true,
                    Child = new Container
                    {
                        Y = -15,
                        Size = new Vector2(box_width, box_height + 15),
                        Masking = true,
                        CornerRadius = 15,
                        BorderThickness = border,
                        BorderColour = outline,
                        Child = new Container
                        {
                            Y = 15,
                            Size = new Vector2(box_width, box_height),
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, Colour = ColourInfo.GradientVertical(gradient1, gradient2) },
                                // The map's background, darkened, behind the left side.
                                new Container
                                {
                                    X = left_x,
                                    Size = new Vector2(left_width, box_height),
                                    Children = new Drawable[]
                                    {
                                        new Box { Y = 0.4f, Size = new Vector2(left_width, left_height), Colour = ColourInfo.GradientVertical(gradient1, gradient2) },
                                        background = new Container { RelativeSizeAxes = Axes.Both },
                                        new Box { Y = -0.5f, Size = new Vector2(left_width, left_height), Colour = Color4.Black.Opacity(0.6f) },
                                    },
                                },
                                title = new Marquee(TosuFonts.BalooBold(19.2f), TosuFonts.BALOO_BOLD_LINE_HEIGHT, 327.6f)
                                {
                                    Position = new Vector2(108.8f - box_x, lineTop(4.8f, 19.2f, 19.2f, TosuFonts.BALOO_BOLD_LINE_HEIGHT)),
                                },
                                difficulty = new Marquee(TosuFonts.BalooMedium(16), TosuFonts.BALOO_MEDIUM_LINE_HEIGHT, 327.6f)
                                {
                                    Position = new Vector2(108.8f - box_x, lineTop(24, 16, 16, TosuFonts.BALOO_MEDIUM_LINE_HEIGHT)),
                                    Colour = Color4Extensions.FromHex("c2c2c2"),
                                },
                                starBox = new Container
                                {
                                    Position = new Vector2(108.8f - box_x, 56),
                                    Size = new Vector2(81.9f, 27.2f),
                                    Masking = true,
                                    CornerRadius = 15 - 9.6f,
                                    EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Shadow, Colour = Color4.Black.Opacity(0.7f), Radius = 6 },
                                    Children = new Drawable[]
                                    {
                                        starBoxColour = new Box { RelativeSizeAxes = Axes.Both },
                                        starText = place(text(17.6f), 4.8f + 1, 17.6f, 17.6f, TosuFonts.BALOO_BOLD_LINE_HEIGHT),
                                        starIcon = new SpriteIcon { Icon = FontAwesome.Solid.Star, Size = new Vector2(16), Y = 4.8f + 0.8f },
                                    },
                                },
                                statistic(0, @"BPM", bpm = new OdometerText(TosuFonts.BalooBold(14.4f), TosuFonts.BALOO_BOLD_LINE_HEIGHT), Color4Extensions.FromHex("FEFFB8")),
                                statistic(1, @"CS", cs = text(14.4f), Color4.White),
                                statistic(2, @"AR", ar = text(14.4f), Color4.White),
                                statistic(3, @"OD", od = text(14.4f), Color4.White),
                                statistic(4, @"HP", hp = text(14.4f), Color4.White),
                                // The right side: dashed off from the left, its hit counts dashed off from its pp.
                                dashedLine(new Vector2(441.2f - box_x, 0), left_height, vertical: true),
                                dashedLine(new Vector2(444.2f - box_x, 45.1f), 221.8f, vertical: false),
                                hit(0, oks = count(), hundred),
                                hit(1, mehs = count(), fifty),
                                hit(2, misses = count(), miss),
                                ppText(499.65f - box_x, pp = new OdometerText(TosuFonts.BalooBold(16), TosuFonts.BALOO_BOLD_LINE_HEIGHT)),
                                place(text(16).With(t =>
                                {
                                    t.Text = @"/";
                                    t.Anchor = Anchor.TopLeft;
                                    t.Origin = Anchor.TopCentre;
                                    t.X = 555.1f - box_x;
                                }), 60.1f, 16, 16, TosuFonts.BALOO_BOLD_LINE_HEIGHT),
                                ppText(610.55f - box_x, fullComboPp = new OdometerText(TosuFonts.BalooBold(16), TosuFonts.BALOO_BOLD_LINE_HEIGHT)),
                            },
                        },
                    },
                },
                // The difficulty graph under the box: all of it faint, as far into the song as the play is brighter and outlined.
                new Container
                {
                    Position = new Vector2(graph_x, box_height),
                    Size = new Vector2(graph_width, graph_height),
                    Children = new Drawable[]
                    {
                        darker = new StrainChart { RelativeSizeAxes = Axes.Both, FillColour = graphColour.Opacity(0.5f) },
                        progress = new Container
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 0,
                            Masking = true,
                            Child = lighter = new StrainChart
                            {
                                Size = new Vector2(graph_width, graph_height),
                                FillColour = graphColour.Opacity(0.5f),
                                LineColour = graphColour,
                            },
                        },
                        judgements = new Container
                        {
                            Y = 4,
                            Size = new Vector2(graph_width, 32),
                        },
                    },
                },
            };
        }

        // Sets a text's top as CSS sets its line.
        private static T place<T>(T text, float cssTop, float cssLineHeight, float size, float fontLineHeight)
            where T : Drawable
        {
            text.Y = lineTop(cssTop, cssLineHeight, size, fontLineHeight);
            return text;
        }

        // A map statistic: its name over its value, 32px wide, the fifth of them 40px apart (line height 0.9).
        private static Drawable statistic(int index, string name, Drawable value, Color4 colour)
        {
            const float size = 14.4f;
            const float line = 12.96f;

            value.Anchor = Anchor.TopCentre;
            value.Origin = Anchor.TopCentre;
            value.Y = value is OdometerText ? 60 + line + (line - size) / 2 : lineTop(60 + line, line, size, TosuFonts.BALOO_BOLD_LINE_HEIGHT);

            return new Container
            {
                X = 244.4f - box_x + index * 40,
                Width = 32,
                Height = box_height,
                Colour = colour,
                Children = new[]
                {
                    text(size).With(t =>
                    {
                        t.Text = name;
                        t.Anchor = Anchor.TopCentre;
                        t.Origin = Anchor.TopCentre;
                        t.Y = lineTop(60, line, size, TosuFonts.BALOO_BOLD_LINE_HEIGHT);
                    }),
                    value,
                },
            };
        }

        private static CountingSpriteText count() => new CountingSpriteText
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Font = TosuFonts.BalooBold(22.4f),
            Y = lineTop(7.9f, 22.4f, 22.4f, TosuFonts.BALOO_BOLD_LINE_HEIGHT),
        };

        // A hit count over its coloured pill, the three spaced evenly over the right side.
        private static Drawable hit(int index, CountingSpriteText number, Color4 colour) => new Container
        {
            X = 444.2f + 31.45f + index * 63.45f - box_x,
            Width = 32,
            Height = box_height,
            Children = new Drawable[]
            {
                number,
                new Container
                {
                    Y = 30.3f,
                    Size = new Vector2(32, 7),
                    Masking = true,
                    CornerRadius = 3.5f,
                    EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Shadow, Colour = Color4.Black.Opacity(0.5f), Radius = 4 },
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
                },
            },
        };

        // A pp number and "pp", centred on x.
        private static Drawable ppText(float x, OdometerText number) => new FillFlowContainer
        {
            X = x,
            Y = 60,
            Origin = Anchor.TopCentre,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Children = new Drawable[]
            {
                number,
                // In a line of its own: a flow sets its children's positions.
                new Container
                {
                    AutoSizeAxes = Axes.X,
                    Height = 16,
                    Child = place(text(16).With(t => t.Text = @"pp"), 0, 16, 16, TosuFonts.BALOO_BOLD_LINE_HEIGHT),
                },
            },
        };

        // A 3px dashed border line (Chrome's dashes for it: 6px, 3px apart).
        private static Drawable dashedLine(Vector2 position, float length, bool vertical)
        {
            var line = new Container
            {
                Position = position,
                Size = vertical ? new Vector2(3, length) : new Vector2(length, 3),
                Masking = true,
            };

            for (float at = 0; at < length; at += 9)
            {
                line.Add(new Box
                {
                    Position = vertical ? new Vector2(0, at) : new Vector2(at, 0),
                    Size = vertical ? new Vector2(3, 6) : new Vector2(6, 3),
                    Colour = dashes,
                });
            }

            return line;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var beatmapInfo = gameplayState?.Beatmap.BeatmapInfo ?? working?.Value?.BeatmapInfo;
            var mods = gameplayState?.Mods ?? Array.Empty<Mod>();
            rate = ModUtils.CalculateRateWithMods(mods);

            if (beatmapInfo != null)
            {
                var metadata = beatmapInfo.Metadata;
                title.Text = $"{metadata.Artist} - {metadata.Title}";
                difficulty.Text = beatmapInfo.DifficultyName;

                if (gameplayState != null)
                {
                    var adjusted = gameplayState.Ruleset.GetAdjustedDisplayDifficulty(beatmapInfo, mods);
                    cs.Text = adjusted.CircleSize.ToString(@"0.##", CultureInfo.InvariantCulture);
                    ar.Text = adjusted.ApproachRate.ToString(@"0.##", CultureInfo.InvariantCulture);
                    od.Text = adjusted.OverallDifficulty.ToString(@"0.##", CultureInfo.InvariantCulture);
                    hp.Text = adjusted.DrainRate.ToString(@"0.##", CultureInfo.InvariantCulture);
                }

                showStars(beatmapInfo.StarRating);
                difficultyCache?.GetDifficultyAsync(beatmapInfo, gameplayState?.Ruleset.RulesetInfo, mods, cancellation.Token)
                               .ContinueWith(t => Schedule(() =>
                               {
                                   if (t.GetResultSafely() is StarDifficulty stars)
                                       showStars(stars.Stars);
                               }), TaskContinuationOptions.OnlyOnRanToCompletion);
            }

            // The overlay's mods name: their acronyms run together (Classic left out: lazer adds it to stable's plays), spaced in twos.
            string modNames = string.Concat(mods.Where(m => m is not ModClassic).Select(m => m.Acronym));
            modsText.Text = string.Join(' ', Enumerable.Range(0, (modNames.Length + 1) / 2).Select(i => modNames.Substring(i * 2, Math.Min(2, modNames.Length - i * 2))));
            bool showMods = modNames.Length > 0 && modNames != @"RX";
            modsTab.X = showMods ? 20 : 20 + 103;

            if (working?.Value != null)
            {
                var texture = working.Value.GetBackground();

                if (texture != null)
                {
                    float height = left_width * texture.Height / texture.Width;
                    background.Add(new Sprite
                    {
                        Texture = texture,
                        Size = new Vector2(left_width, height),
                        Y = 4.8f - 0.15f * height,
                    });
                }

                calculateGraph(working.Value);
            }

            pp.RollTo(0, true);
            fullComboPp.RollTo(0, true);
            stats.PerformancePoints.BindValueChanged(v => pp.RollTo(v.NewValue), true);
            stats.FullComboPerformancePoints.BindValueChanged(v => fullComboPp.RollTo(v.NewValue), true);
            stats.Oks.BindValueChanged(v => onHit(v, oks, hundred, @"100"), true);
            stats.Mehs.BindValueChanged(v => onHit(v, mehs, fifty, @"50"), true);
            stats.Misses.BindValueChanged(v => onHit(v, misses, miss, @"x"), true);
        }

        private void showStars(double stars)
        {
            Color4 textColour = stars >= 6.5 ? Color4Extensions.FromHex("ffdd55") : Color4.Black;

            starBoxColour.Colour = stars < 0.1 ? Color4Extensions.FromHex("AAAAAA") : colours.ForStarDifficulty(stars);
            starText.Text = stars.ToString(@"0.00", CultureInfo.InvariantCulture);
            starText.Colour = textColour;
            starIcon.Colour = textColour;
        }

        private void calculateGraph(WorkingBeatmap beatmap)
        {
            if (gameplayState == null)
                return;

            var ruleset = gameplayState.Ruleset;
            var playable = gameplayState.Beatmap;
            var mods = gameplayState.Mods.ToArray();
            double songLength = beatmap.Track.Length > 0 ? beatmap.Track.Length : playable.HitObjects.LastOrDefault()?.StartTime ?? 0;

            Task.Run(() => StrainGraph.Calculate(ruleset, beatmap, playable, mods, songLength, cancellation.Token), cancellation.Token)
                .ContinueWith(t => Schedule(() =>
                {
                    darker.Values = t.GetResultSafely();
                    lighter.Values = darker.Values;
                }), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        // A hit count changed: counted up, and a mark on the graph for a new one (a tall one for a 100 or 50 that came with a slider break).
        private void onHit(ValueChangedEvent<int> count, CountingSpriteText number, Color4 colour, string type)
        {
            number.CountTo(count.NewValue);

            if (stats.Oks.Value == 0 && stats.Mehs.Value == 0 && stats.Misses.Value == 0)
            {
                clearJudgements();
                return;
            }

            if (count.NewValue <= count.OldValue || count.NewValue == 0)
                return;

            bool sliderBreak = stats.SliderBreaks.Value != lastSliderBreaks;
            lastSliderBreaks = stats.SliderBreaks.Value;

            float height = type == @"x" || sliderBreak ? 24 : 12;
            var mark = new Container
            {
                X = (float)(stats.SongProgress.Value * graph_width) - 2,
                Size = new Vector2(4, 0),
                Masking = true,
                CornerRadius = 2,
                Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
            };

            judgements.Add(mark);
            judgements.ChangeChildDepth(mark, type == @"x" ? -3 : type == @"50" ? -2 : -1);
            mark.Delay(50).ResizeHeightTo(height, 250, Easing.InOutSine);
        }

        private void clearJudgements()
        {
            lastSliderBreaks = 0;

            foreach (var mark in judgements.Children)
                mark.ResizeHeightTo(0, 250, Easing.InOutSine).Expire();
        }

        protected override void Update()
        {
            base.Update();

            // The overlay's width transition (600ms) following tosu's updates, as smoothly at any frame rate.
            progress.Width = (float)Interpolation.DampContinuously(progress.Width, stats.SongProgress.Value * graph_width, 60, Time.Elapsed);

            if (gameplayState != null)
            {
                double beatsPerMinute = gameplayState.Beatmap.ControlPointInfo.TimingPointAt(gameplayClock?.CurrentTime ?? 0).BPM * rate;
                bpm.RollTo((long)Math.Round(beatsPerMinute));
            }

            // The star rating's text and star, spaced evenly in its box.
            float free = starBox.DrawWidth - 9.6f - starText.DrawWidth - starIcon.DrawWidth;
            starText.X = 4.8f + free / 3;
            starIcon.X = starText.X + starText.DrawWidth + free / 3;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            cancellation.Cancel();
        }

        /// <summary>A line of text, scrolling round (8s a turn, its copy 20px behind) when it's wider than its place, as the overlay's titles do.</summary>
        private partial class Marquee : CompositeDrawable
        {
            private readonly FontUsage font;
            private readonly float lineHeight;
            private readonly FillFlowContainer flow;
            private string text = string.Empty;
            private bool layoutPending;

            public Marquee(FontUsage font, float lineHeight, float width)
            {
                this.font = font;
                this.lineHeight = lineHeight;

                // A line of the font tall, so its glyphs aren't cut (CSS draws them out of their 1em line); placed by the caller as CSS would its line.
                Size = new Vector2(width, font.Size * lineHeight);
                Masking = true;
                InternalChild = flow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(20, 0),
                };
            }

            public string Text
            {
                set
                {
                    text = value;
                    flow.ClearTransforms();
                    flow.X = 0;
                    flow.Clear();
                    flow.Add(line());
                    layoutPending = true;
                }
            }

            private OsuSpriteText line() => new OsuSpriteText
            {
                Text = text,
                Font = font,
            };

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                if (!layoutPending || flow.Count == 0 || flow[0].DrawWidth == 0)
                    return;

                layoutPending = false;
                float width = flow[0].DrawWidth;

                if (width < DrawWidth)
                    return;

                flow.Add(line());
                flow.MoveToX(0).MoveToX(-(width + 20), 8000).Loop();
            }
        }
    }
}

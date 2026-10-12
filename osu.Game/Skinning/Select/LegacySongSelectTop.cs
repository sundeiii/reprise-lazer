// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;
using osu.Game.Screens.Select.Filter;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Select
{
    /// <summary>
    /// The top of osu!stable's song select: "songselect-top" (or, without one, stable's default look), the selected beatmap's details
    /// on the left, and grouping, sorting, the grouping tabs and the search text on the right. Grouping and sorting are song select's
    /// own settings, so the carousel follows them; typing still goes to song select's (unseen) search box.
    /// </summary>
    /// <remarks>
    /// Measured against stable's song select at 1024x768. Elements on the right stay with the right edge on wider screens.
    /// </remarks>
    public partial class LegacySongSelectTop : CompositeDrawable
    {
        public const float HEIGHT = 150;

        // The right side's positions, from stable's 1024px wide layout.
        private const float layout_width = 1024;

        private static readonly Color4 label_colour = new Color4(130, 190, 255, 255);
        private static readonly Color4 search_colour = new Color4(150, 255, 50, 255);

        private readonly IBindable<string> searchText;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        // The selected beatmap's star rating with the selected mods (stable's shows them with mods).
        private IBindable<StarDifficulty>? starDifficulty;
        private CancellationTokenSource? starCancellation;
        private ModSettingChangeTracker? modSettingChangeTracker;
        private BeatmapInfo? starBeatmap;
        private RulesetInfo? starRuleset;

        private BeatmapDifficulty shownDifficulty = new BeatmapDifficulty();
        private double shownStars;

        // What the line shows: rolling to the values above when mods change (straight to them for another beatmap).
        private readonly double[] displayed = new double[5];

        public double DisplayedCircleSize { get => displayed[0]; set => setDisplayed(0, value); }
        public double DisplayedDrainRate { get => displayed[1]; set => setDisplayed(1, value); }
        public double DisplayedApproachRate { get => displayed[2]; set => setDisplayed(2, value); }
        public double DisplayedOverallDifficulty { get => displayed[3]; set => setDisplayed(3, value); }
        public double DisplayedStars { get => displayed[4]; set => setDisplayed(4, value); }

        private void setDisplayed(int index, double value)
        {
            displayed[index] = value;
            Scheduler.AddOnce(writeDifficulty);
        }

        private bool animateDifficulty;

        private void showDifficulty()
        {
            double duration = animateDifficulty ? 300 : 0;

            this.TransformTo(nameof(DisplayedCircleSize), (double)shownDifficulty.CircleSize, duration, Easing.OutQuint);
            this.TransformTo(nameof(DisplayedDrainRate), (double)shownDifficulty.DrainRate, duration, Easing.OutQuint);
            this.TransformTo(nameof(DisplayedApproachRate), (double)shownDifficulty.ApproachRate, duration, Easing.OutQuint);
            this.TransformTo(nameof(DisplayedOverallDifficulty), (double)shownDifficulty.OverallDifficulty, duration, Easing.OutQuint);
            this.TransformTo(nameof(DisplayedStars), shownStars, duration, Easing.OutQuint);
        }

        // Up to two decimals, as the mods make them (DT on AR 9: 10.33).
        private void writeDifficulty() => difficultyLine.Text = string.Create(CultureInfo.InvariantCulture,
            $"CS:{displayed[0]:0.##} HP:{displayed[1]:0.##} AR:{displayed[2]:0.##} OD:{displayed[3]:0.##} Stars:{displayed[4]:0.00}");

        private Bindable<GroupMode> group = null!;
        private Bindable<SortMode> sort = null!;

        private SpriteIcon statusIcon = null!;
        private TruncatingSpriteText title = null!;
        private OsuSpriteText mapper = null!;
        private OsuSpriteText lengthLine = null!;
        private OsuSpriteText countsLine = null!;
        private OsuSpriteText difficultyLine = null!;
        private OsuSpriteText search = null!;

        private CancellationTokenSource? statisticsCancellation;

        // Stable's grouping tabs, and its group and sort choices (in its order), as song select's modes.
        private static readonly (string Text, GroupMode Mode)[] tabs =
        {
            ("Recently Played", GroupMode.LastPlayed),
            ("By Artist", GroupMode.Artist),
            ("By Difficulty", GroupMode.Difficulty),
            ("No Grouping", GroupMode.None),
        };

        private static readonly (string Text, GroupMode Mode)[] group_choices =
        {
            ("No Grouping", GroupMode.None),
            ("By Difficulty", GroupMode.Difficulty),
            ("By Artist", GroupMode.Artist),
            ("Recently Played", GroupMode.LastPlayed),
            ("Collections", GroupMode.Collections),
            ("By Date Added", GroupMode.DateAdded),
            ("By BPM", GroupMode.BPM),
            ("By Creator", GroupMode.Author),
            ("By Length", GroupMode.Length),
            ("By Title", GroupMode.Title),
            ("By Rank Achieved", GroupMode.RankAchieved),
            ("By Source", GroupMode.Source),
            ("By Ranked Status", GroupMode.RankedStatus),
            ("Favourites", GroupMode.Favourites),
            ("My Maps", GroupMode.MyMaps),
        };

        private static readonly (string Text, SortMode Mode)[] sort_choices =
        {
            ("By Artist", SortMode.Artist),
            ("By BPM", SortMode.BPM),
            ("By Creator", SortMode.Author),
            ("By Date Added", SortMode.DateAdded),
            ("By Difficulty", SortMode.Difficulty),
            ("By Length", SortMode.Length),
            ("By Title", SortMode.Title),
            ("By Source", SortMode.Source),
            ("By Last Played", SortMode.LastPlayed),
            ("By Date Ranked", SortMode.DateRanked),
            ("By Date Submitted", SortMode.DateSubmitted),
        };

        /// <summary>
        /// The group and sort choice boxes, to go above everything else (stable draws them above the skin's footer art,
        /// which some skins draw the top with).
        /// </summary>
        public readonly Container Dropdowns;

        public LegacySongSelectTop(IBindable<string> searchText)
        {
            this.searchText = searchText;

            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;

            Dropdowns = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = HEIGHT,
            };
        }

        private bool stable2015;
        private float screenHeight = 1080;

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, OsuConfigManager config, TextureStore textures, GameHost host)
        {
            group = config.GetBindable<GroupMode>(OsuSetting.SongSelectGroupMode);
            sort = config.GetBindable<SortMode>(OsuSetting.SongSelectSortingMode);
            stable2015 = config.Is2015(skin);

            if (host.Window != null)
                screenHeight = host.Window.ClientSize.Height;

            var tabFlow = new FillFlowContainer
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopLeft,
                X = 515 - layout_width,
                Y = 58,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(2, 0),
            };

            var tabTexture = skin.GetTexture(@"selection-tab");

            foreach (var (text, mode) in tabs)
                tabFlow.Add(new LegacyTab(text, group, mode, tabTexture));

            InternalChildren = new Drawable[]
            {
                createBackground(skin, stable2015 ? textures.Get(@"Stable/songselect-top@2x") : null),
                // The selected beatmap.
                // Where stable's is: centred at (12, 12) in its 640x480, as large as its 28 pixel image.
                statusIcon = new SpriteIcon
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(12, 12) * 1.6f,
                    Size = new Vector2(28),
                },
                // Up to the group box (see Update), as far as stable's goes.
                title = new TruncatingSpriteText
                {
                    Position = new Vector2(38, -2),
                    Font = OsuFont.GetFont(size: 24),
                },
                mapper = new OsuSpriteText
                {
                    Position = new Vector2(38, 20),
                    Font = OsuFont.GetFont(size: 18),
                },
                lengthLine = new OsuSpriteText
                {
                    Position = new Vector2(5, 40),
                    Font = OsuFont.GetFont(size: 17),
                },
                countsLine = new OsuSpriteText
                {
                    Position = new Vector2(5, 58),
                    Font = OsuFont.GetFont(size: 17),
                },
                // Stable's has a black backing.
                new Container
                {
                    Position = new Vector2(2, 77),
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black,
                        },
                        difficultyLine = new OsuSpriteText
                        {
                            Margin = new MarginPadding { Horizontal = 2 },
                            Font = OsuFont.GetFont(size: 13),
                        },
                    }
                },
                // Grouping, sorting and searching.
                label("Group", 458),
                label("Sort", 757),
                tabFlow,
                createSearchLine(label: true),
            };

            // Stable's text (2015's and the later one's alike: Aller at its sizes).
            use2015Text();
        }

        // Stable 2015's: Aller at its sizes and places (its field, x1.6): the title 18, the mapper and the lines 12 (the first bold),
        // the difficulty 8.
        private void use2015Text()
        {
            title.Position = new Vector2(21, -3) * 1.6f;
            title.Font = StableFont.Get(18, screenHeight: screenHeight);
            mapper.Position = new Vector2(23, 12) * 1.6f;
            mapper.Font = StableFont.Get(12, screenHeight: screenHeight);
            lengthLine.Position = new Vector2(1, 24) * 1.6f;
            lengthLine.Font = StableFont.Get(12, bold: true, screenHeight: screenHeight);
            countsLine.Position = new Vector2(1, 36) * 1.6f;
            countsLine.Font = StableFont.Get(12, screenHeight: screenHeight);
            difficultyLine.Font = StableFont.Get(8, screenHeight: screenHeight);
            difficultyLine.Parent!.Position = new Vector2(1, 48) * 1.6f;

            // "Group" and "Sort" at 22.
            foreach (var t in InternalChildren.OfType<OsuSpriteText>().Where(t => t.Text == "Group" || t.Text == "Sort"))
                t.Font = StableFont.Get(22, screenHeight: screenHeight);
        }

        /// <summary>
        /// "Search:" and what's typed. Stable draws the label under the skin's art and the typed text above it: two of these,
        /// one with only the label shown and one (in <see cref="Dropdowns"/>) with only the text.
        /// </summary>
        private Drawable createSearchLine(bool label)
        {
            var text = new OsuSpriteText
            {
                Font = OsuFont.GetFont(size: 19, weight: FontWeight.Bold),
                Alpha = label ? 0 : 1,
                AlwaysPresent = true,
            };

            if (!label)
                search = text;

            return new FillFlowContainer
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopLeft,
                X = 676 - layout_width,
                Y = 86,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = "Search:",
                        Font = OsuFont.GetFont(size: 19, weight: FontWeight.Bold),
                        Colour = search_colour,
                        Alpha = label ? 1 : 0,
                        AlwaysPresent = true,
                    },
                    text,
                }
            };
        }

        protected override void Update()
        {
            base.Update();

            // To just before the group box.
            title.MaxWidth = Math.Max(100, DrawWidth - (layout_width - 546) - title.X - 10);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Dropdowns.Children = new Drawable[]
            {
                createSearchLine(label: false),
                new LegacySelectBox<GroupMode>(group, group_choices) { Anchor = Anchor.TopRight, X = 546 - layout_width, Y = 30, Stable2015 = true },
                new LegacySelectBox<SortMode>(sort, sort_choices) { Anchor = Anchor.TopRight, X = 816 - layout_width, Y = 30, Stable2015 = true },
            };

            beatmap.BindValueChanged(_ => updateBeatmap());
            mods.BindValueChanged(m =>
            {
                // A mod's settings too (DT's speed, Difficulty Adjust's AR...), not only which mods.
                modSettingChangeTracker?.Dispose();
                modSettingChangeTracker = new ModSettingChangeTracker(m.NewValue);
                modSettingChangeTracker.SettingChanged += _ => Scheduler.AddOnce(updateBeatmap);

                updateBeatmap();
            }, true);
            ruleset.BindValueChanged(_ => updateBeatmap(), true);
            searchText.BindValueChanged(s => search.Text = string.IsNullOrEmpty(s.NewValue) ? "Type to search!" : s.NewValue, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            modSettingChangeTracker?.Dispose();
            starCancellation?.Cancel();
        }

        private static Drawable label(string text, float x) => new OsuSpriteText
        {
            Anchor = Anchor.TopRight,
            X = x - layout_width,
            Y = 22,
            Text = text,
            Font = OsuFont.GetFont(size: 32, weight: FontWeight.Light),
            Colour = label_colour,
        };

        private void updateBeatmap()
        {
            statisticsCancellation?.Cancel();

            var working = beatmap.Value;

            if (working is DummyWorkingBeatmap)
            {
                statusIcon.Hide();
                title.Text = mapper.Text = lengthLine.Text = countsLine.Text = difficultyLine.Text = string.Empty;
                starBeatmap = null;
                return;
            }

            var info = working.BeatmapInfo;
            var currentMods = mods.Value;
            // Speed mods (DT, HT...) make it shorter or longer and the BPM higher or lower, as in stable's.
            double rate = ModUtils.CalculateRateWithMods(currentMods);

            showStatus(info.Status);
            title.Text = StableMetadata.ArtistTitle(info.Metadata, after: $" [{info.DifficultyName}]");
            mapper.Text = $"Mapped by {info.Metadata.Author.Username}";

            // CS/HP/AR/OD with the mods (HR, EZ, and the speed mods' effect on AR and OD), and the stars with them.
            try
            {
                shownDifficulty = ruleset.Value.CreateInstance().GetAdjustedDisplayDifficulty(info, currentMods);
            }
            catch (Exception)
            {
                shownDifficulty = new BeatmapDifficulty(info.Difficulty);
            }

            // Another beatmap (or mode): its own rating, which then follows the mods and their settings by itself.
            // The same one: the rating keeps going (starting over would show the rating without mods for a moment).
            bool sameBeatmap = info.Equals(starBeatmap) && ruleset.Value.Equals(starRuleset);
            animateDifficulty = sameBeatmap;

            if (!sameBeatmap)
            {
                starBeatmap = info;
                starRuleset = ruleset.Value;
                shownStars = info.StarRating;

                // The previous rating stops (it goes on following the mods otherwise, and would write the old beatmap's line back).
                starCancellation?.Cancel();
                starDifficulty?.UnbindAll();

                starCancellation = new CancellationTokenSource();
                starDifficulty = difficultyCache.GetBindableDifficulty(info, starCancellation.Token);
                starDifficulty.BindValueChanged(d =>
                {
                    shownStars = d.NewValue.Stars;
                    // The first rating for a beatmap rolls in too (from the one without mods).
                    animateDifficulty = true;
                    showDifficulty();
                });

                // The lines that take a moment: empty until they're in (on the same beatmap they stay until the new ones are).
                lengthLine.Text = countsLine.Text = string.Empty;
            }

            showDifficulty();

            // The counts and BPM need the beatmap converted for the ruleset, which can take a moment: once per beatmap and ruleset
            // (they don't change with the mods), the speed mods only scaling the length and BPM (dragging DT's speed converted it every frame).
            if (playableStatistics is { } known && known.Beatmap.Equals(info) && known.Ruleset.Equals(ruleset.Value))
            {
                showPlayableStatistics(known, rate);
                return;
            }

            var cancellation = statisticsCancellation = new CancellationTokenSource();
            var currentRuleset = ruleset.Value;

            Task.Run(() =>
            {
                try
                {
                    var playable = working.GetPlayableBeatmap(currentRuleset);
                    var points = playable.ControlPointInfo;

                    var statistics = new PlayableStatistics(info, currentRuleset, points.BPMMinimum, points.BPMMaximum, 60000 / playable.GetMostCommonBeatLength(),
                        playable.HitObjects.Count, string.Join(" ", playable.GetStatistics().Select(s => $"{s.Name}: {s.Content}")));

                    Schedule(() =>
                    {
                        if (cancellation.IsCancellationRequested)
                            return;

                        playableStatistics = statistics;
                        showPlayableStatistics(statistics, ModUtils.CalculateRateWithMods(mods.Value));
                    });
                }
                catch (Exception)
                {
                    // A beatmap that can't be converted to this ruleset: no counts.
                }
            }, cancellation.Token);
        }

        private record PlayableStatistics(BeatmapInfo Beatmap, RulesetInfo Ruleset, double BPMMinimum, double BPMMaximum, double BPMCommon, int Objects, string Counts);

        private PlayableStatistics? playableStatistics;

        private void showPlayableStatistics(PlayableStatistics statistics, double rate)
        {
            double bpmMinimum = statistics.BPMMinimum * rate;
            double bpmMaximum = statistics.BPMMaximum * rate;
            double common = statistics.BPMCommon * rate;
            string bpm = Math.Round(bpmMinimum) == Math.Round(bpmMaximum)
                ? $"{bpmMinimum:0}"
                : $"{bpmMinimum:0}-{bpmMaximum:0} ({common:0})";
            string length = TimeSpan.FromMilliseconds(statistics.Beatmap.Length / rate).ToString(@"mm\:ss", CultureInfo.InvariantCulture);

            lengthLine.Text = string.Create(CultureInfo.InvariantCulture, $"Length: {length} BPM: {bpm} Objects: {statistics.Objects}");
            countsLine.Text = statistics.Counts;
        }

        // Stable's status icon before the title (only for statuses it marks).
        private void showStatus(BeatmapOnlineStatus status)
        {
            (IconUsage Icon, Color4 Colour)? look = status switch
            {
                BeatmapOnlineStatus.Ranked or BeatmapOnlineStatus.Approved => (FontAwesome.Solid.AngleDoubleUp, new Color4(100, 200, 255, 255)),
                BeatmapOnlineStatus.Qualified => (FontAwesome.Solid.Check, new Color4(140, 220, 60, 255)),
                BeatmapOnlineStatus.Loved => (FontAwesome.Solid.Heart, new Color4(255, 100, 180, 255)),
                _ => null,
            };

            if (look == null)
            {
                statusIcon.Hide();
                return;
            }

            statusIcon.Icon = look.Value.Icon;
            statusIcon.Colour = look.Value.Colour;
            statusIcon.Show();
        }

        /// <summary>
        /// The skin's "songselect-top", or (lazer's default legacy skin has none) stable's default look: dark at the top and,
        /// on the left, down to below the leaderboard's heading, edged by a thin blue line.
        /// </summary>
        private static Drawable createBackground(ISkinSource skin, Texture? stableTop)
        {
            // Stable always has a black bar behind its top (97 of its 480 high): the top is never see-through.
            var blackBar = new Box
            {
                RelativeSizeAxes = Axes.X,
                // 97 texture pixels from -16 (stable's 1x1 sprite scaled, in this 768-high layout): to just under the top bar.
                Height = 97 - 16,
                Colour = Color4.Black,
            };

            // Stable 2015's is always its own (skins' came later).
            var texture = stableTop ?? skin.GetTexture(@"songselect-top");

            if (texture != null)
            {
                return new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = stableTop != null
                        ? new Drawable[]
                        {
                            blackBar,
                            new Sprite { Texture = texture, Size = new Vector2(1366, 155) },
                            // Wider than 16:9: stable stretches its last column to the edge.
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 155,
                                Padding = new MarginPadding { Left = 1365 },
                                Child = new Sprite
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Texture = texture.Crop(new RectangleF(1 - 1 / 1366f, 0, 1 / 1366f, 1), Axes.Both),
                                },
                            },
                        }
                        // A skin's own top as it is: no black behind where it's see-through (it showed as a black strip above BTMC's banner).
                        : new Drawable[] { new Sprite { Texture = texture } }
                };
            }

            // The curve up from the left part (148 down) to the top bar (80), from 330 to 440 across.
            static float curveY(float x)
            {
                float t = Math.Clamp((x - 330) / 110, 0, 1);
                return 148 - 68 * (3 * t * t - 2 * t * t * t);
            }

            var edge = new List<Vector2> { new Vector2(0, 148), new Vector2(330, 148) };

            for (int i = 1; i <= 12; i++)
                edge.Add(new Vector2(330 + 110 * i / 12f, curveY(330 + 110 * i / 12f)));

            edge.Add(new Vector2(layout_width * 4, 80));

            var fill = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Child = new Box
                {
                    Y = 80,
                    Size = new Vector2(330, 68),
                    Colour = Color4.Black,
                },
            };

            // Black down to the curve all the way along it (thin strips), so nothing shows between the fill and the line.
            for (float x = 330; x < 440; x += 2)
            {
                fill.Add(new Box
                {
                    Position = new Vector2(x, 80),
                    Size = new Vector2(2.5f, curveY(x + 1) - 80),
                    Colour = Color4.Black,
                });
            }

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    blackBar,
                    fill,
                    new Path
                    {
                        PathRadius = 1.5f,
                        Colour = new Color4(70, 140, 255, 255),
                        Vertices = edge,
                    },
                }
            };
        }

        /// <summary>
        /// One of stable's grouping tabs: the skin's "selection-tab" (or a box like it) in crimson, or white while its grouping is on.
        /// </summary>
        private partial class LegacyTab : ClickableContainer
        {
            private static readonly Color4 inactive_colour = new Color4(220, 20, 60, 255);

            private readonly Bindable<GroupMode> group;
            private readonly GroupMode mode;
            private readonly Drawable background;
            private readonly OsuSpriteText text;

            public LegacyTab(string label, Bindable<GroupMode> group, GroupMode mode, Texture? texture)
            {
                this.group = group;
                this.mode = mode;

                Size = new Vector2(116, 20);
                Action = () => group.Value = mode;

                Children = new Drawable[]
                {
                    background = texture != null
                        ? new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Texture = texture,
                        }
                        : new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 4,
                            Child = new Box { RelativeSizeAxes = Axes.Both },
                        },
                    text = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = label,
                        Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                group.BindValueChanged(g =>
                {
                    bool active = g.NewValue == mode;
                    background.Colour = active ? Color4.White : inactive_colour;
                    text.Colour = active ? Color4.Black : Color4.White;
                }, true);
            }
        }
    }
}

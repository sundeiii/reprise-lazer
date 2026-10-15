// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Skinning.Ranking;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Modern
{
    /// <summary>
    /// The redesigned results screen's advanced info (revision 11's "Advanced Info Screen"): the beatmap and its numbers, the score
    /// against the most there is, the mods and their multiplier, the player's best and the beatmap's play count; the play's numbers
    /// against their last best; its timing (hit errors and accuracy over the play, the distribution, unstable rate) and the ruleset's
    /// own extras (the accuracy heatmap); and the beatmap's tags. Laid out in the design's 1920x1080 (inside <see cref="ModernResultsScreen"/>).
    /// </summary>
    public partial class AdvancedInfoPage : CompositeDrawable
    {
        private readonly ScoreInfo score;
        private readonly LegacyRankingDetails details;
        private readonly Func<Drawable> createHeader;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        private Container panel = null!;
        private FillFlowContainer bottomRow = null!;
        private Container hitErrorGraph = null!;
        private Container accuracyGraph = null!;
        private readonly CancellationTokenSource loadCancellation = new CancellationTokenSource();

        public AdvancedInfoPage(ScoreInfo score, LegacyRankingDetails details, Func<Drawable> createHeader)
        {
            this.score = score;
            this.details = details;
            this.createHeader = createHeader;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var working = beatmaps.GetWorkingBeatmap(score.BeatmapInfo);

            InternalChildren = new Drawable[]
            {
                createHeader(),
                panel = new Container
                {
                    Position = new Vector2(90, 146),
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                    Height = 832,
                    Padding = new MarginPadding { Right = 114 },
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 20,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = ModernResultsScreen.BACKGROUND_5, Alpha = 0.95f },
                            createBeatmapStrip(working),
                            createScoreRow(),
                            createStatistics(),
                            new GraphPanel("Hit Error", hitErrorGraph = new Container { RelativeSizeAxes = Axes.Both })
                            {
                                Position = new Vector2(301, 208),
                                RelativeSizeAxes = Axes.X,
                                Width = 1,
                                Height = 252,
                                Padding = new MarginPadding { Right = 316 },
                            },
                            new GraphPanel("Accuracy", accuracyGraph = new Container { RelativeSizeAxes = Axes.Both })
                            {
                                Position = new Vector2(301, 472),
                                RelativeSizeAxes = Axes.X,
                                Width = 1,
                                Height = 188,
                                Padding = new MarginPadding { Right = 316 },
                            },
                            bottomRow = new FillFlowContainer
                            {
                                Position = new Vector2(16, 677),
                                RelativeSizeAxes = Axes.X,
                                Height = 145,
                                Padding = new MarginPadding { Right = 32 },
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(12, 0),
                            },
                        }
                    }
                },
            };

            bottomRow.Add(new GraphPanel("Tag the Beatmap!", new UserTagControl(score.BeatmapInfo!) { RelativeSizeAxes = Axes.X }) { RelativeSizeAxes = Axes.Both, Width = 0.49f });

            if (score.HitEvents.Count == 0)
            {
                const string none = "Hit data is only there for a play you've just set or watched.";
                hitErrorGraph.Add(new EmptyNote(none));
                accuracyGraph.Add(new EmptyNote(none));
            }
            else
            {
                hitErrorGraph.Add(new HitErrorOverTime(score.HitEvents) { RelativeSizeAxes = Axes.Both });
                accuracyGraph.Add(new AccuracyOverTime(score) { RelativeSizeAxes = Axes.Both });
            }

            // The ruleset's own statistics (the timing distribution, the accuracy heatmap) need the playable beatmap: made in the background.
            // (Not its performance breakdown: that's the game's pp, not the server's shown with the play.)
            Task.Run(() => working.GetPlayableBeatmap(score.Ruleset, score.Mods), loadCancellation.Token).ContinueWith(task => Schedule(() =>
            {
                if (task.IsFaulted || task.IsCanceled)
                    return;

                var playable = task.GetResultSafely();
                var items = score.Ruleset.CreateInstance().CreateStatisticsForScore(score, playable)
                                 .Where(i => i.Name.ToString() is "Timing Distribution" or "Accuracy Heatmap")
                                 .Where(i => !i.RequiresHitEvents || score.HitEvents.Count > 0)
                                 .Take(2);

                foreach (var item in items)
                {
                    var content = item.CreateContent();

                    // The heatmap is drawn square and tall: a fixed square, scaled down to the panel's height.
                    if (item.Name.ToString() == "Accuracy Heatmap")
                    {
                        content = new Container
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Size = new Vector2(230),
                            Scale = new Vector2(0.4f),
                            Child = content,
                        };
                    }

                    bottomRow.Add(new GraphPanel(item.Name, content) { RelativeSizeAxes = Axes.Both, Width = 0.245f });
                }
            }), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        protected override void Dispose(bool isDisposing)
        {
            loadCancellation.Cancel();
            base.Dispose(isDisposing);
        }

        // ── the beatmap: its cover, title and numbers ────────────────────────────────────────

        private Drawable createBeatmapStrip(WorkingBeatmap working)
        {
            var beatmap = score.BeatmapInfo!;
            var metadata = beatmap.BeatmapSet?.Metadata ?? beatmap.Metadata;

            // Its difficulty with the play's mods (CS, AR, OD, HP as they were played).
            var difficulty = new BeatmapDifficulty(beatmap.Difficulty);
            foreach (var mod in score.Mods.OfType<IApplicableToDifficulty>())
                mod.ApplyToDifficulty(difficulty);

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 104,
                Masking = true,
                Children = new Drawable[]
                {
                    new BeatmapBackgroundSprite(working)
                    {
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fill,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.7f), Color4.Black.Opacity(0.1f)),
                    },
                    new FillFlowContainer
                    {
                        Position = new Vector2(22, 16),
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 4),
                        Children = new Drawable[]
                        {
                            new BeatmapSetOnlineStatusPill { Status = beatmap.Status, TextSize = 12 },
                            new OsuSpriteText
                            {
                                Text = new RomanisableString(metadata.TitleUnicode, metadata.Title),
                                Font = OsuFont.TorusAlternate.With(size: 24, weight: FontWeight.SemiBold),
                                Shadow = true,
                            },
                            new OsuSpriteText
                            {
                                Text = new RomanisableString(metadata.ArtistUnicode, metadata.Artist),
                                Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold),
                                Shadow = true,
                            },
                        }
                    },
                    // The difficulty's card: stars, name, mapper; then its numbers.
                    new Container
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Position = new Vector2(-8, 6),
                        Size = new Vector2(730, 90),
                        Masking = true,
                        CornerRadius = 12,
                        BorderThickness = 2,
                        BorderColour = Color4Extensions.FromHex("ff66ab").Opacity(0.6f),
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.7f },
                            new FillFlowContainer
                            {
                                Position = new Vector2(18, 10),
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
                                Children = new Drawable[]
                                {
                                    new StarRatingDisplay(new StarDifficulty(beatmap.StarRating, 0), StarRatingDisplaySize.Small)
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                    },
                                    new TextFlowContainer(t => t.Colour = colours.ForStarDifficulty(beatmap.StarRating))
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        AutoSizeAxes = Axes.Both,
                                    }.With(t =>
                                    {
                                        t.AddText(beatmap.DifficultyName, s => s.Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold));
                                        t.AddText(" mapped by ", s => s.Font = OsuFont.Torus.With(size: 14));
                                        t.AddText(metadata.Author.Username, s => s.Font = OsuFont.Torus.With(size: 14, weight: FontWeight.SemiBold));
                                    }),
                                }
                            },
                            new FillFlowContainer
                            {
                                Position = new Vector2(18, 44),
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(16, 0),
                                ChildrenEnumerable = beatmapNumbers(difficulty).Select(n => new BeatmapNumber(n.Name, n.Value)),
                            },
                        }
                    },
                }
            };
        }

        private IEnumerable<(LocalisableString Name, string Value)> beatmapNumbers(BeatmapDifficulty difficulty)
        {
            var beatmap = score.BeatmapInfo!;

            if (score.Ruleset.OnlineID == 0)
            {
                // (Counts not known for a beatmap that isn't stored: -1.)
                bool known = beatmap.TotalObjectCount >= 0 && beatmap.EndTimeObjectCount >= 0;
                yield return ("Circles", known ? (beatmap.TotalObjectCount - beatmap.EndTimeObjectCount).ToString("N0", CultureInfo.CurrentCulture) : "-");
                yield return ("Sliders & Spinners", known ? beatmap.EndTimeObjectCount.ToString("N0", CultureInfo.CurrentCulture) : "-");
            }

            yield return ("Circle Size", difficulty.CircleSize.ToString("0.#", CultureInfo.CurrentCulture));
            yield return ("Approach Rate", difficulty.ApproachRate.ToString("0.#", CultureInfo.CurrentCulture));
            yield return ("Accuracy", difficulty.OverallDifficulty.ToString("0.#", CultureInfo.CurrentCulture));
            yield return ("HP Drain", difficulty.DrainRate.ToString("0.#", CultureInfo.CurrentCulture));
        }

        // ── the score against the most there is, the mods, the best and the plays ───────────

        private Drawable createScoreRow()
        {
            long total = score.GetDisplayScore(ScoringMode.Standardised);
            var counts = score.GetStatisticsForDisplay().Where(s => s.MaxCount == null && s.Result.IsBasic()).ToList();
            int objects = Math.Max(1, counts.Sum(c => c.Count));

            var previousBest = new OsuSpriteText { Font = OsuFont.Torus.With(size: 28), Colour = ModernResultsScreen.NUMBER_GRADIENT };
            var plays = new OsuSpriteText { Font = OsuFont.Torus.With(size: 28), Colour = ModernResultsScreen.NUMBER_GRADIENT };
            details.PreviousBest.BindValueChanged(b => previousBest.Text = b.NewValue != null ? b.NewValue.TotalScore.ToString("N0", CultureInfo.CurrentCulture) : "-", true);
            details.OnlineBeatmap.BindValueChanged(b => plays.Text = b.NewValue != null ? b.NewValue.PlayCount.ToString("N0", CultureInfo.CurrentCulture) : "-", true);

            double multiplier = score.Ruleset.CreateInstance()
                                     .CreateScoreMultiplierCalculator(new ScoreMultiplierContext(score.BeatmapInfo!.Difficulty))
                                     .CalculateFor(score.Mods);

            return new Container
            {
                Position = new Vector2(0, 112),
                RelativeSizeAxes = Axes.X,
                Height = 84,
                Children = new Drawable[]
                {
                    // (The rank fills what it's in.)
                    new Container
                    {
                        Position = new Vector2(22, 18),
                        Size = new Vector2(64, 32),
                        Child = new DrawableRank(score.Rank),
                    },
                    new OsuSpriteText
                    {
                        Position = new Vector2(92, 0),
                        Text = score.GetDisplayScore(ScoringMode.Standardised).ToString("N0", CultureInfo.CurrentCulture),
                        Font = OsuFont.Torus.With(size: 64, weight: FontWeight.Light),
                        Colour = ModernResultsScreen.NUMBER_GRADIENT,
                    },
                    // The score against the most there is (1,000,000), and under it the play's judgements in their colours.
                    new Container
                    {
                        Position = new Vector2(385, 28),
                        Size = new Vector2(617, 26),
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 16,
                                Masking = true,
                                CornerRadius = 8,
                                Children = new Drawable[]
                                {
                                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.5f },
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Width = (float)Math.Clamp(total / 1_000_000.0, 0, 1),
                                        Colour = ColourInfo.GradientHorizontal(Color4Extensions.FromHex("7cf6ff"), Color4Extensions.FromHex("b2ff66")),
                                    },
                                }
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                RelativeSizeAxes = Axes.X,
                                Height = 4,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(2, 0),
                                ChildrenEnumerable = counts.Where(c => c.Count > 0).Select(c => new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Width = (float)c.Count / objects - 0.004f,
                                    Colour = colours.ForHitResult(c.Result),
                                }),
                            },
                        }
                    },
                    new FillFlowContainer
                    {
                        Position = new Vector2(1028, 4),
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(24, 0),
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText { Text = "MODS", Font = OsuFont.Torus.With(size: 16, weight: FontWeight.Bold) },
                                    new FillFlowContainer<ModIcon>
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(-6, 0),
                                        ChildrenEnumerable = score.Mods.AsOrdered().Select(m => new ModIcon(m) { Scale = new Vector2(0.45f) }),
                                    },
                                }
                            },
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText { Text = "MULTIPLIER", Font = OsuFont.Torus.With(size: 16, weight: FontWeight.Bold) },
                                    new OsuSpriteText
                                    {
                                        Text = $"{multiplier.ToString("0.0#", CultureInfo.CurrentCulture)}x",
                                        Font = OsuFont.Torus.With(size: 36),
                                        Colour = Color4Extensions.FromHex("ff66ab"),
                                    },
                                }
                            },
                        }
                    },
                    // The player's best before and the beatmap's plays.
                    new Container
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        X = -20,
                        Size = new Vector2(420, 80),
                        Masking = true,
                        CornerRadius = 10,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = ModernResultsScreen.BACKGROUND_4 },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 40,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(70, 0),
                                Children = new Drawable[]
                                {
                                    labelled("PERSONAL BEST", previousBest),
                                    labelled("TOTAL PLAYS", plays),
                                }
                            },
                        }
                    },
                }
            };
        }

        private static Drawable labelled(LocalisableString label, Drawable value) => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Children = new[]
            {
                new OsuSpriteText { Text = label, Font = OsuFont.Torus.With(size: 16, weight: FontWeight.Bold) },
                value,
            }
        };

        // ── the play's numbers, against the last best ───────────────────────────────────────

        private Drawable createStatistics()
        {
            var statistics = score.GetStatisticsForDisplay().ToList();
            var basic = statistics.Where(s => s.MaxCount == null && s.Result.IsBasic()).ToList();
            var extra = statistics.Where(s => s.MaxCount is > 0).Take(3).ToList();

            var accuracyDelta = new Delta();
            var comboDelta = new Delta();
            var ppDelta = new Delta();
            var pp = new OsuSpriteText { Font = OsuFont.Torus.With(size: 24), Colour = ModernResultsScreen.NUMBER_GRADIENT };

            details.Performance.BindValueChanged(p =>
            {
                double? value = p.NewValue ?? score.PP;
                pp.Text = value is double v ? Math.Round(v).ToString("N0", CultureInfo.CurrentCulture) : "-";
                ppDelta.Set(value - details.PreviousBest.Value?.PP, "N0");
            }, true);
            details.PreviousBest.BindValueChanged(b =>
            {
                accuracyDelta.Set(b.NewValue != null ? (score.Accuracy - b.NewValue.Accuracy) * 100 : null, "0.00");
                comboDelta.Set(b.NewValue != null ? score.MaxCombo - b.NewValue.MaxCombo : null, "N0");
                ppDelta.Set((details.Performance.Value ?? score.PP) - b.NewValue?.PP, "N0");
            }, true);

            var hitEvents = score.HitEvents.Where(HitEventExtensions.AffectsUnstableRate).ToList();
            double? ur = hitEvents.Count > 0 ? hitEvents.CalculateUnstableRate()?.Result : null;
            double? average = hitEvents.CalculateAverageHitError();
            double? median = hitEvents.CalculateMedianHitError();
            double? early = hitEvents.Where(e => e.TimeOffset < 0).Select(e => (double?)e.TimeOffset).Average();
            double? late = hitEvents.Where(e => e.TimeOffset > 0).Select(e => (double?)e.TimeOffset).Average();
            double? furthest = hitEvents.Count > 0 ? hitEvents.Max(e => Math.Abs(e.TimeOffset)) : null;

            static string ms(double? value) => value is double v ? $"{v.ToString("+0.##;-0.##;0", CultureInfo.CurrentCulture)}ms" : "-";

            return new Container
            {
                Position = new Vector2(16, 208),
                Size = new Vector2(275, 452),
                Children = new Drawable[]
                {
                    // Accuracy, combo and pp, with what they gained (or lost) on the last best.
                    new PanelBox
                    {
                        Size = new Vector2(150, 282),
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding(12),
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 8),
                            Children = new Drawable[]
                            {
                                valueWithDelta("ACCURACY", new OsuSpriteText { Text = score.Accuracy.FormatAccuracy(), Font = OsuFont.Torus.With(size: 24), Colour = ModernResultsScreen.NUMBER_GRADIENT }, accuracyDelta),
                                valueWithDelta("COMBO", new OsuSpriteText { Text = $"{score.MaxCombo:N0}x", Font = OsuFont.Torus.With(size: 24), Colour = ModernResultsScreen.NUMBER_GRADIENT }, comboDelta),
                                valueWithDelta("PP", pp, ppDelta),
                            }
                        }
                    },
                    // The judgements, each under its name in its colour.
                    new PanelBox
                    {
                        X = 158,
                        Size = new Vector2(117, 282),
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding(12),
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 6),
                            ChildrenEnumerable = basic.Select(s => new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    new JudgementLabel(judgementName(s), colours.ForHitResult(s.Result)),
                                    new OsuSpriteText { Text = s.Count.ToString("N0", CultureInfo.CurrentCulture), Font = OsuFont.Torus.With(size: 24), Colour = ModernResultsScreen.NUMBER_GRADIENT },
                                }
                            }),
                        }
                    },
                    // Slider ends, spinner bonus and the like.
                    new PanelBox
                    {
                        Y = 290,
                        Size = new Vector2(150, 162),
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding(12),
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 6),
                            ChildrenEnumerable = extra.Select(s => labelled(s.DisplayName.ToUpper(), new OsuSpriteText
                            {
                                Text = s.Count.ToString("N0", CultureInfo.CurrentCulture),
                                Font = OsuFont.Torus.With(size: 20),
                                Colour = ModernResultsScreen.NUMBER_GRADIENT,
                            })),
                        }
                    },
                    // The play's timing.
                    new PanelBox
                    {
                        Position = new Vector2(158, 290),
                        Size = new Vector2(117, 162),
                        Child = new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding(10),
                            Content = new[]
                            {
                                new Drawable[] { timing("UR", ur?.ToString("N0", CultureInfo.CurrentCulture) ?? "-"), timing("AVG", ms(average)) },
                                new Drawable[] { timing("MEDIAN", ms(median)), timing("FURTHEST", furthest is double f ? $"{f:0}ms" : "-") },
                                new Drawable[] { timing("EARLY", ms(early)), timing("LATE", ms(late)) },
                            }
                        }
                    },
                }
            };

            static Drawable timing(string label, string value) => new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new OsuSpriteText { Text = label, Font = OsuFont.Torus.With(size: 10, weight: FontWeight.Bold), Colour = ModernResultsScreen.SUB_CONTENT },
                    new OsuSpriteText { Text = value, Font = OsuFont.Torus.With(size: 14) },
                }
            };
        }

        private static Drawable valueWithDelta(LocalisableString label, OsuSpriteText value, Delta delta) => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Children = new Drawable[]
            {
                new OsuSpriteText { Text = label, Font = OsuFont.Torus.With(size: 16, weight: FontWeight.Bold) },
                value,
                delta,
            }
        };

        private LocalisableString judgementName(HitResultDisplayStatistic statistic)
        {
            if (score.Ruleset.OnlineID == 0)
            {
                switch (statistic.Result)
                {
                    case HitResult.Great: return "300";
                    case HitResult.Ok: return "100";
                    case HitResult.Meh: return "50";
                    case HitResult.Miss: return "MISS";
                }
            }

            return statistic.DisplayName.ToUpper();
        }

        // ── parts ────────────────────────────────────────────────────────────────────────────

        /// <summary>A darker box in the panel.</summary>
        private partial class PanelBox : Container
        {
            protected override Container<Drawable> Content { get; } = new Container { RelativeSizeAxes = Axes.Both };

            public PanelBox()
            {
                Masking = true;
                CornerRadius = 10;
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.35f },
                    Content,
                };
            }
        }

        /// <summary>A titled box (a graph, the tags, the ruleset's statistics), its title after the design's small bar.</summary>
        private partial class GraphPanel : Container
        {
            public GraphPanel(LocalisableString title, Drawable content)
            {
                Child = new PanelBox
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box { Position = new Vector2(14, 15), Size = new Vector2(3, 14), Colour = Color4Extensions.FromHex("b2ff66") },
                        new OsuSpriteText
                        {
                            Position = new Vector2(22, 12),
                            Text = title,
                            Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold),
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 38, Horizontal = 16, Bottom = 14 },
                            Child = content,
                        },
                    }
                };
            }
        }

        /// <summary>What's said instead of a graph.</summary>
        private partial class EmptyNote : OsuSpriteText
        {
            public EmptyNote(string text)
            {
                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;
                Text = text;
                Font = OsuFont.Torus.With(size: 16);
                Colour = ModernResultsScreen.SUB_CONTENT;
            }
        }

        /// <summary>A judgement's name in its colour, as the design's small tags.</summary>
        private partial class JudgementLabel : CircularContainer
        {
            public JudgementLabel(LocalisableString name, Color4 colour)
            {
                AutoSizeAxes = Axes.Both;
                Masking = true;
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
                    new OsuSpriteText
                    {
                        Margin = new MarginPadding { Horizontal = 6, Vertical = 1 },
                        Text = name,
                        Font = OsuFont.Torus.With(size: 12, weight: FontWeight.Bold),
                        Colour = ModernResultsScreen.BACKGROUND_5,
                    },
                };
            }
        }

        /// <summary>A beatmap number in the difficulty's card: its name, the value, a small bar over them.</summary>
        private partial class BeatmapNumber : FillFlowContainer
        {
            public BeatmapNumber(LocalisableString name, string value)
            {
                AutoSizeAxes = Axes.Both;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(0, 2);
                Children = new Drawable[]
                {
                    new Box { Size = new Vector2(70, 2), Colour = Color4Extensions.FromHex("ff66ab").Opacity(0.7f) },
                    new OsuSpriteText { Text = name, Font = OsuFont.Torus.With(size: 11, weight: FontWeight.SemiBold), Colour = ModernResultsScreen.SUB_CONTENT },
                    new OsuSpriteText { Text = value, Font = OsuFont.Torus.With(size: 14) },
                };
            }
        }

        /// <summary>The difference to the last best: green when gained, red when lost, nothing without one.</summary>
        private partial class Delta : OsuSpriteText
        {
            public Delta()
            {
                Font = OsuFont.Torus.With(size: 14);
            }

            public void Set(double? difference, string format)
            {
                if (difference is not double d || Math.Abs(d) < 1e-9)
                {
                    Text = string.Empty;
                    return;
                }

                Text = d.ToString(format.StartsWith('0') ? $"+{format};-{format}" : "+#,0;-#,0", CultureInfo.CurrentCulture);
                Colour = d > 0 ? Color4Extensions.FromHex("b2ff66") : Color4Extensions.FromHex("ff6666");
            }
        }

        /// <summary>
        /// Each hit's timing over the play: a dot per hit at its time (across) and how early or late it was (up and down), in its
        /// judgement's colour; lines at on time and at the edges of the judgements' windows.
        /// </summary>
        private partial class HitErrorOverTime : CompositeDrawable
        {
            private readonly IReadOnlyList<HitEvent> hitEvents;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public HitErrorOverTime(IReadOnlyList<HitEvent> hitEvents)
            {
                this.hitEvents = hitEvents.Where(e => e.HitObject.HitWindows != HitWindows.Empty && e.Result.IsBasic()).ToList();
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                if (hitEvents.Count == 0)
                    return;

                double start = hitEvents.Min(e => e.HitObject.StartTime);
                double end = Math.Max(start + 1, hitEvents.Max(e => e.HitObject.StartTime));
                var windows = hitEvents[0].HitObject.HitWindows;
                double range = Math.Max(1, hitEvents.Where(e => e.Result.IsHit()).Select(e => Math.Abs(e.TimeOffset)).DefaultIfEmpty(0).Max());
                range = Math.Max(range, windows.WindowFor(HitResult.Great));

                float y(double offset) => (float)(0.5 + Math.Clamp(offset, -range, range) / (2 * range));

                // On time, and the judgements' windows that fit.
                AddInternal(line(0.5f, Color4.White.Opacity(0.5f)));
                foreach (var result in new[] { HitResult.Great, HitResult.Ok, HitResult.Meh })
                {
                    double window = windows.WindowFor(result);
                    if (window <= 0 || window > range)
                        continue;
                    AddInternal(line(y(-window), colours.ForHitResult(result).Opacity(0.3f)));
                    AddInternal(line(y(window), colours.ForHitResult(result).Opacity(0.3f)));
                }

                foreach (var e in hitEvents)
                {
                    bool miss = !e.Result.IsHit();
                    AddInternal(new Circle
                    {
                        RelativePositionAxes = Axes.Both,
                        Origin = Anchor.Centre,
                        Position = new Vector2((float)((e.HitObject.StartTime - start) / (end - start)), miss ? 0.98f : y(e.TimeOffset)),
                        Size = new Vector2(miss ? 4 : 3),
                        Colour = colours.ForHitResult(e.Result),
                    });
                }
            }

            private static Drawable line(float y, Color4 colour) => new Box
            {
                RelativeSizeAxes = Axes.X,
                RelativePositionAxes = Axes.Y,
                Height = 1,
                Y = y,
                Colour = colour,
            };
        }

        /// <summary>The accuracy as it went over the play (what it was after each judgement), from 0% to 100%.</summary>
        private partial class AccuracyOverTime : CompositeDrawable
        {
            private readonly List<(double Time, double Accuracy)> points = new List<(double, double)>();
            private Path path = null!;
            private Vector2 lastSize;

            public AccuracyOverTime(ScoreInfo score)
            {
                var processor = score.Ruleset.CreateInstance().CreateScoreProcessor();
                double scored = 0, possible = 0;

                foreach (var e in score.HitEvents.Where(e => e.Result.AffectsAccuracy()))
                {
                    scored += processor.GetBaseScoreForResult(e.Result);
                    possible += processor.GetBaseScoreForResult(e.HitObject.Judgement.MaxResult);
                    if (possible > 0)
                        points.Add((e.HitObject.StartTime, scored / possible));
                }
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.X, Height = 1, Colour = Color4.White.Opacity(0.15f) },
                    new Box { RelativeSizeAxes = Axes.X, RelativePositionAxes = Axes.Y, Y = 0.5f, Height = 1, Colour = Color4.White.Opacity(0.15f) },
                    path = new Path { PathRadius = 1.5f, Colour = Color4Extensions.FromHex("b2ff66") },
                };
            }

            protected override void Update()
            {
                base.Update();

                if (points.Count < 2 || DrawSize == lastSize || DrawWidth <= 0)
                    return;

                lastSize = DrawSize;
                double start = points[0].Time;
                double end = Math.Max(start + 1, points[^1].Time);
                // From 50% up (where plays are): the top is 100%.
                path.Vertices = points.Select(p => new Vector2(
                    (float)((p.Time - start) / (end - start)) * DrawWidth,
                    (float)(1 - Math.Clamp((p.Accuracy - 0.5) / 0.5, 0, 1)) * DrawHeight)).ToList();
            }
        }
    }
}

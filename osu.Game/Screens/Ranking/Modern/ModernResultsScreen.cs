// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Ranking.Expanded;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Skinning.Ranking;
using osu.Game.Users.Drawables;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Modern
{
    /// <summary>
    /// The redesigned results screen's main score screen (osu!'s design team's "Client / Result Screen", revision 11): the player
    /// and their place up top, the beatmap, the score with its accuracy, combo, pp and judgements, the mods, the grade in its ring,
    /// and the actions along the bottom. Laid out in the design's 1920x1080, scaled to fit; things on the right stay on the right
    /// on wider screens. Shown over <see cref="ResultsScreen"/> with <see cref="OsuSetting.NewResultsScreen"/>.
    /// </summary>
    public partial class ModernResultsScreen : CompositeDrawable
    {
        /// <summary>The design's slant: its boxes lean 10 to the right over 80 of height.</summary>
        public const float SHEAR = 0.125f;

        // The design's colours (its b5/b4/b3 backgrounds and c2 sub-content).
        public static readonly Color4 BACKGROUND_5 = Color4Extensions.FromHex("222a27");
        public static readonly Color4 BACKGROUND_4 = Color4Extensions.FromHex("2e3835");
        public static readonly Color4 BACKGROUND_3 = Color4Extensions.FromHex("394642");
        public static readonly Color4 SUB_CONTENT = Color4Extensions.FromHex("dbf0e9");

        /// <summary>The design's light-blue fade on its big numbers.</summary>
        public static readonly ColourInfo NUMBER_GRADIENT = ColourInfo.GradientVertical(Color4.White, Color4Extensions.FromHex("b2e5fe"));

        private readonly ScoreInfo score;
        private readonly Action? retry;
        private readonly Action? watchReplay;
        private readonly LegacyRankingDetails details;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private ScreenshotManager? screenshots { get; set; }

        private readonly Bindable<int?> countryRank = new Bindable<int?>();
        private readonly Bindable<int?> teamRank = new Bindable<int?>();
        private readonly Bindable<double?> performance = new Bindable<double?>();

        private Container personalBest = null!;
        private Container mainPage = null!;
        private Container pages = null!;
        private AdvancedInfoPage? advancedPage;
        private RankingPage? rankingPage;
        private Page currentPage;

        private enum Page
        {
            Main,
            AdvancedInfo,
            Ranking,
        }
        private CountingText ppText = null!;
        private ScoreCounter scoreCounter = null!;
        private IBindable<ScoringMode> scoringMode = null!;

        // The numbers that count up (as lazer's results do), with where they end.
        private readonly List<(CountingText Text, double Target)> countUps = new List<(CountingText, double)>();
        private bool countedUp;

        public ModernResultsScreen(ScoreInfo score, Action? retry, Action? watchReplay, LegacyRankingDetails? details = null)
        {
            this.score = score;
            this.retry = retry;
            this.watchReplay = watchReplay;
            this.details = details ?? new LegacyRankingDetails();

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, RealmAccess realm, BeatmapDifficultyCache difficultyCache)
        {
            var beatmap = score.BeatmapInfo!;
            var metadata = beatmap.BeatmapSet?.Metadata ?? beatmap.Metadata;

            StarDifficulty stars = new StarDifficulty(beatmap.StarRating, 0);
            if (realm.Run(r => r.Find<BeatmapInfo>(beatmap.ID)) != null)
                stars = difficultyCache.GetDifficultyAsync(beatmap, score.Ruleset, score.Mods).GetResultSafely() ?? stars;

            scoringMode = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode);

            InternalChild = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = new Vector2(1920, 1080),
                Strategy = DrawSizePreservationStrategy.Minimum,
                Children = new Drawable[]
                {
                    // The beatmap's background shows through, darkened a little (the design's 25% black).
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.25f },
                    pages = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = mainPage = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new[]
                            {
                                createBeatmapInfo(metadata, beatmap, stars),
                                createScoreArea(),
                                createGrade(),
                                createHeader(false),
                            }
                        },
                    },
                    createBottomBar(),
                }
            };

            // The pp counts up too, from wherever it is once it's known (the server's can come in late).
            performance.BindValueChanged(pp =>
            {
                if (pp.NewValue is not double value)
                    ppText.Text = "-";
                else if (countedUp)
                    ppText.RollTo(value, 1000);
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The play's pp: the server's (Reprise's rules), else what the results screen worked out, else the score's own.
            performance.Value = details.Performance.Value ?? score.PP;
            details.Performance.BindValueChanged(pp =>
            {
                if (pp.NewValue != null)
                    performance.Value = pp.NewValue;
            });

            // "Personal best" once it's known this play beat the last one (a play just set).
            if (details.HasSecondPage)
            {
                details.PreviousBest.BindValueChanged(_ => updatePersonalBest());
                details.StatisticsUpdate.BindValueChanged(_ => updatePersonalBest(), true);
            }

            fetchRanks();

            // As lazer's results: once the ring has started filling, the score rolls up (ticking), and the other numbers with it (each time
            // the screen's shown). Scheduled so the counters don't jump to where they end straight away.
            ScheduleAfterChildren(() =>
            {
                using (BeginDelayedSequence(AccuracyCircle.ACCURACY_TRANSFORM_DELAY))
                {
                    scoreCounter.FadeIn();
                    scoreCounter.Current.Value = score.GetDisplayScore(scoringMode.Value);

                    foreach (var (text, target) in countUps)
                        text.RollTo(target, AccuracyCircle.ACCURACY_TRANSFORM_DURATION);
                    if (performance.Value is double pp)
                        ppText.RollTo(pp, AccuracyCircle.ACCURACY_TRANSFORM_DURATION);
                }

                countedUp = true;
                scoringMode.BindValueChanged(mode => scoreCounter.Current.Value = score.GetDisplayScore(mode.NewValue));
            });
        }

        /// <summary>A number that counts up to where it ends.</summary>
        private CountingText countUp(double target, Func<double, string> format)
        {
            var text = new CountingText(format);
            countUps.Add((text, target));
            return text;
        }

        private void updatePersonalBest()
        {
            var previous = details.PreviousBest.Value;
            // No previous best: known once the server's update for this play arrived.
            bool beaten = previous != null ? score.TotalScore > previous.TotalScore : details.StatisticsUpdate.Value != null;
            personalBest.FadeTo(beaten ? 1 : 0, 300, Easing.OutQuint);
        }

        /// <summary>Over to a page (the bottom bar's tabs), or back to the main one from it.</summary>
        private void togglePage(Page page) => showPage(currentPage == page ? Page.Main : page);

        /// <summary>A page (the advanced info and ranking made the first time they're shown).</summary>
        private void showPage(Page page)
        {
            currentPage = page;

            if (page == Page.AdvancedInfo && advancedPage == null)
                pages.Add(advancedPage = new AdvancedInfoPage(score, details, () => createHeader(true)) { Alpha = 0 });
            if (page == Page.Ranking && rankingPage == null)
                pages.Add(rankingPage = new RankingPage(score) { Alpha = 0 });

            mainPage.FadeTo(page == Page.Main ? 1 : 0, 200, Easing.OutQuint);
            advancedPage?.FadeTo(page == Page.AdvancedInfo ? 1 : 0, 200, Easing.OutQuint);
            rankingPage?.FadeTo(page == Page.Ranking ? 1 : 0, 200, Easing.OutQuint);
        }

        /// <summary>The play's place among the player's country and team on this beatmap.</summary>
        private void fetchRanks()
        {
            if (!api.IsLoggedIn || score.BeatmapInfo?.OnlineID is not > 0 || score.UserID != api.LocalUser.Value.OnlineID)
                return;

            foreach (var (scope, target) in new[] { (BeatmapLeaderboardScope.Country, countryRank), (BeatmapLeaderboardScope.Team, teamRank) })
            {
                // (A Relax or Autopilot play is on that mode's leaderboard.)
                var request = new GetScoresRequest(score.BeatmapInfo, score.Ruleset.ForMods(score.Mods), scope);
                request.Success += scores => Schedule(() => target.Value = scores.UserScore?.Position);
                api.Queue(request);
            }
        }

        // ── the header: the player and their place ───────────────────────────────────────────

        /// <param name="advanced">The advanced info's: "← Basic Info" before it, nothing after.</param>
        private Drawable createHeader(bool advanced)
        {
            var user = score.User;
            int? position = details.UserBestPosition.Value ?? score.Position;

            var header = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 60,
                Y = 73,
                Padding = advanced ? new MarginPadding { Left = 176, Right = 24 } : new MarginPadding { Left = 72, Right = 236 },
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 20,
                    BorderThickness = 2,
                    BorderColour = Color4Extensions.FromHex("ffd966"),
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = BACKGROUND_5 },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(10, 0),
                            Margin = new MarginPadding { Left = 15 },
                            Children = new Drawable[]
                            {
                                new HeaderPillButton("Ranking", FontAwesome.Solid.ArrowLeft, () => showPage(Page.Ranking))
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Margin = new MarginPadding { Left = 28, Right = 14 },
                                    Text = position is int place ? $"#{place:N0}" : string.Empty,
                                    Font = OsuFont.Torus.With(size: 32),
                                },
                                new UpdateableAvatar(user, isInteractive: false)
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(52),
                                    Masking = true,
                                    CornerRadius = 8,
                                },
                                new UpdateableFlag(user.CountryCode)
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(30, 20),
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = user.Username,
                                    Font = OsuFont.Torus.With(size: 24, weight: FontWeight.SemiBold),
                                },
                            }
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8, 0),
                            Margin = new MarginPadding { Right = 20 },
                            Children = new Drawable[]
                            {
                                new HeaderValue("Country Rank", countryRank),
                                new HeaderValue("Team Rank", teamRank),
                                new HeaderValue("Achieved on", score.Date.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture), 131),
                            }
                        },
                    }
                }
            };

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    header,
                    // "Advanced Info", at the right of the header; on the advanced info, "Basic Info" at its left.
                    advanced
                        ? new PageButton("Basic Info", true, () => showPage(Page.Main))
                        {
                            Position = new Vector2(-100, 73),
                        }
                        : new PageButton("Advanced Info", false, () => showPage(Page.AdvancedInfo))
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            Position = new Vector2(41, 73),
                        },
                }
            };
        }

        // ── the beatmap ──────────────────────────────────────────────────────────────────────

        private Drawable createBeatmapInfo(IBeatmapMetadataInfo metadata, BeatmapInfo beatmap, StarDifficulty stars)
        {
            var favourites = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold),
                Colour = SUB_CONTENT,
            };
            var heart = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(18),
                Icon = FontAwesome.Regular.Heart,
            };

            // The count as fetched, then one up or down as the heart's clicked here (the fetched count doesn't follow it).
            int favouriteChange = 0;

            void updateFavourites() => favourites.Text = details.OnlineBeatmap.Value?.BeatmapSet is { } set
                ? Math.Max(0, set.FavouriteCount + favouriteChange).ToString("N0", CultureInfo.CurrentCulture)
                : "-";

            details.OnlineBeatmap.BindValueChanged(_ =>
            {
                favouriteChange = 0;
                updateFavourites();
            }, true);
            details.Favourited.BindValueChanged(f =>
            {
                heart.Icon = f.NewValue ? FontAwesome.Solid.Heart : FontAwesome.Regular.Heart;

                if (f.OldValue != f.NewValue && details.OnlineBeatmap.Value != null)
                {
                    favouriteChange += f.NewValue ? 1 : -1;
                    updateFavourites();
                }
            }, true);

            Color4 difficultyColour = DifficultyNameColour(colours, stars.Stars);

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // The dark band behind the title, slanting off to the left edge.
                    new SlantedBox(BACKGROUND_3, 0.9f)
                    {
                        Position = new Vector2(-563, -41),
                        Size = new Vector2(1279, 347),
                    },
                    new FillFlowContainer
                    {
                        Position = new Vector2(74, 157),
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 6),
                        Children = new Drawable[]
                        {
                            new BeatmapSetOnlineStatusPill
                            {
                                Status = beatmap.Status,
                                TextSize = 12,
                            },
                            new OsuSpriteText
                            {
                                Text = new RomanisableString(metadata.TitleUnicode, metadata.Title),
                                Font = OsuFont.TorusAlternate.With(size: 36, weight: FontWeight.SemiBold),
                                Shadow = true,
                            },
                            new OsuSpriteText
                            {
                                Text = new RomanisableString(metadata.ArtistUnicode, metadata.Artist),
                                Font = OsuFont.Torus.With(size: 24, weight: FontWeight.SemiBold),
                                Shadow = true,
                            },
                            new ClickableContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Action = details.ToggleFavourite,
                                Masking = true,
                                CornerRadius = 6,
                                Children = new Drawable[]
                                {
                                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.2f },
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(6, 0),
                                        Padding = new MarginPadding { Horizontal = 12, Vertical = 7 },
                                        Children = new Drawable[] { heart, favourites },
                                    },
                                }
                            },
                        }
                    },
                    // The difficulty: ruleset, stars, name and mapper, on a dark band from the left edge.
                    new Container
                    {
                        Position = new Vector2(-530, 286),
                        Size = new Vector2(1145, 40),
                        Children = new Drawable[]
                        {
                            new SlantedBox(BACKGROUND_5) { RelativeSizeAxes = Axes.Both },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 603,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
                                Children = new Drawable[]
                                {
                                    new ConstrainedIconContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Size = new Vector2(24),
                                        Icon = score.Ruleset.CreateInstance().CreateIcon(),
                                    },
                                    new StarRatingDisplay(stars)
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                    },
                                    // (Within the band: a long difficulty name is cut short.)
                                    new DifficultyLine(beatmap.DifficultyName, metadata.Author.Username, difficultyColour, 390)
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                    },
                                }
                            },
                        }
                    },
                }
            };
        }

        // ── the score ────────────────────────────────────────────────────────────────────────

        private Drawable createScoreArea()
        {
            // Silent, as the ring is: the game's own results underneath (hidden) play their sounds (ticks, dinks, applause) as they animate.
            scoreCounter = new ScoreCounter(false)
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                // Up a little, to sit in the band's middle: the font has room under its digits (they'd sit 12 low).
                Position = new Vector2(60, -12),
                Current = { Value = 0 },
                Alpha = 0,
                AlwaysPresent = true,
            };

            var statistics = score.GetStatisticsForDisplay().ToList();
            var basic = statistics.Where(s => s.MaxCount == null && s.Result.IsBasic()).ToList();
            var extra = statistics.Where(s => s.MaxCount is > 0).ToList();

            ppText = new CountingText(v => Math.Round(v).ToString("N0", CultureInfo.CurrentCulture));

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // The score, on its band.
                    new Container
                    {
                        Position = new Vector2(-45, 333),
                        Size = new Vector2(890, 180),
                        Children = new Drawable[]
                        {
                            new SlantedBox(Color4Extensions.FromHex("171c1a"), 0.75f) { RelativeSizeAxes = Axes.Both },
                            scoreCounter,
                        }
                    },
                    personalBest = new Container
                    {
                        Position = new Vector2(707, 313),
                        Size = new Vector2(265, 40),
                        Alpha = 0,
                        Children = new Drawable[]
                        {
                            new SlantedBox(ColourInfo.GradientVertical(Color4Extensions.FromHex("ffe7a8"), Color4Extensions.FromHex("ffb800"))) { RelativeSizeAxes = Axes.Both },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Text = "PERSONAL BEST",
                                Font = OsuFont.Torus.With(size: 24, weight: FontWeight.Bold),
                                Spacing = new Vector2(2.4f, 0),
                                Colour = BACKGROUND_5,
                            },
                        }
                    },
                    // Accuracy, combo and pp.
                    new StatisticBox("ACCURACY", NUMBER_GRADIENT, BACKGROUND_5, countUp(score.Accuracy, v => v.FormatAccuracy().ToString()))
                    {
                        Position = new Vector2(125, 520),
                    },
                    new StatisticBox("COMBO", NUMBER_GRADIENT, BACKGROUND_5, countUp(score.MaxCombo, v => $"{Math.Round(v):N0}x"))
                    {
                        Position = new Vector2(366, 520),
                        Maximum = $"/{score.GetMaximumAchievableCombo():N0}",
                    },
                    new StatisticBox("PP", NUMBER_GRADIENT, BACKGROUND_5, ppText) { Position = new Vector2(607, 520) },
                    // The judgements, each in its colour.
                    new FillFlowContainer
                    {
                        Position = new Vector2(110, 605),
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(-9, 0),
                        ChildrenEnumerable = basic.Select((s, i) => createJudgement(s, i == 0 ? 250 : 180)),
                    },
                    // Slider ends, spinner bonus and the like: out of how many there were.
                    new FillFlowContainer
                    {
                        Position = new Vector2(96, 710),
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(-6, 0),
                        ChildrenEnumerable = extra.Select(s => new StatisticBox(s.DisplayName.ToUpper(), NUMBER_GRADIENT, BACKGROUND_4, countUp(s.Count, count), width: 165, height: 70,
                            valueSize: 28)
                        {
                            Maximum = $"/{s.MaxCount:N0}",
                        }),
                    },
                    createMods(),
                }
            };
        }

        /// <summary>A judgement's box: its count in its colour, on its colour (the first, the best one, wider).</summary>
        private Drawable createJudgement(HitResultDisplayStatistic statistic, float width)
        {
            ColourInfo colour = statistic.Result switch
            {
                HitResult.Perfect or HitResult.Great => ColourInfo.GradientHorizontal(Color4Extensions.FromHex("00ffaa"), Color4Extensions.FromHex("ff9ad7")),
                HitResult.Good or HitResult.Ok => Color4Extensions.FromHex("69ee00"),
                HitResult.Meh => Color4Extensions.FromHex("ffb800"),
                HitResult.Miss => Color4Extensions.FromHex("ff0000"),
                _ => colours.ForHitResult(statistic.Result),
            };
            Color4 edge = statistic.Result switch
            {
                HitResult.Perfect or HitResult.Great => Color4Extensions.FromHex("7cf6ff"),
                HitResult.Good or HitResult.Ok => Color4Extensions.FromHex("69ee00"),
                HitResult.Meh => Color4Extensions.FromHex("ffb800"),
                HitResult.Miss => Color4Extensions.FromHex("ff0000"),
                _ => colours.ForHitResult(statistic.Result),
            };

            return new StatisticBox(judgementName(statistic), colour, Color4.Black, countUp(statistic.Count, count), width: width, fillAlpha: 0.75f, edge: edge);
        }

        private static string count(double value) => Math.Round(value).ToString("N0", CultureInfo.CurrentCulture);

        /// <summary>osu!'s judgements by their old names (300, 100, 50), as the design has them; other rulesets' as they're called.</summary>
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

        private Drawable createMods() => new Container
        {
            Position = new Vector2(82, 785),
            // As wide as the mods need (at least the design's 315), with room on the right for the slant.
            AutoSizeAxes = Axes.X,
            Height = 88,
            Alpha = score.Mods.Length > 0 ? 1 : 0,
            Children = new Drawable[]
            {
                new SlantedBox(BACKGROUND_4) { RelativeSizeAxes = Axes.Both },
                new Container { Width = 315 },
                new OsuSpriteText
                {
                    Position = new Vector2(27, 8),
                    Text = "MODS",
                    Font = OsuFont.Torus.With(size: 16, weight: FontWeight.Bold),
                },
                // Side by side, apart (a rate mod's plate clear of the next one).
                new FillFlowContainer<ModIcon>
                {
                    Position = new Vector2(21, 24),
                    Margin = new MarginPadding { Right = 30 },
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4, 0),
                    ChildrenEnumerable = score.Mods.AsOrdered().Select(m => new ModIcon(m) { Scale = new Vector2(0.75f) }),
                },
            }
        };

        // ── the grade ────────────────────────────────────────────────────────────────────────

        private Drawable createGrade() => new Container
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopLeft,
            // From the design's right edge, so it stays to the right on wider screens.
            Position = new Vector2(1162 - 1920, 183),
            Size = new Vector2(1140, 714),
            Children = new Drawable[]
            {
                // The dark backing: a half circle around the ring, to the right edge.
                new Circle { Size = new Vector2(714), Colour = Color4.Black, Alpha = 0.4f },
                new Box { X = 357, Size = new Vector2(1140 - 357, 714), Colour = Color4.Black, Alpha = 0.4f },
                new Circle
                {
                    Position = new Vector2(48, 46),
                    Size = new Vector2(623),
                    Colour = Color4Extensions.FromHex("333333"),
                },
                // Lazer's ring, scaled up whole (its grade drawn at its own size inside it, so it grows with it). Without its flair: the game's own
                // results underneath play the sounds (two rings asking for applause crashed the game).
                new Container
                {
                    Position = new Vector2(37, 35),
                    Size = new Vector2(230),
                    Scale = new Vector2(645 / 230f),
                    Child = new AccuracyCircle(score)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                },
                new ArrowButton(() => showPage(Page.AdvancedInfo))
                {
                    Position = new Vector2(692, 335),
                },
            }
        };

        // ── the bottom bar ───────────────────────────────────────────────────────────────────

        private Drawable createBottomBar() => new Container
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
            RelativeSizeAxes = Axes.X,
            Height = 80,
            Children = new Drawable[]
            {
                new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 51,
                    Colour = BACKGROUND_5,
                },
                new FillFlowContainer
                {
                    X = 47,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(-8, 0),
                    Children = new Drawable[]
                    {
                        new BigButton("Back", FontAwesome.Solid.ArrowCircleLeft, Color4Extensions.FromHex("de31ae"), Color4Extensions.FromHex("ff86dd"), details.Back, iconFirst: true)
                        {
                            Margin = new MarginPadding { Top = 0, Right = 38 },
                        },
                        new TabButton("Save Replay", FontAwesome.Solid.Download, Color4Extensions.FromHex("66ccff"), details.ExportReplay),
                        new TabButton("Screenshot", FontAwesome.Solid.Camera, Color4Extensions.FromHex("ff66ab"), () => screenshots?.TakeScreenshotAsync()),
                        new TabButton("Ranking", FontAwesome.Solid.Sitemap, Color4Extensions.FromHex("8c66ff"), () => togglePage(Page.Ranking)),
                        new TabButton("More Info", FontAwesome.Solid.InfoCircle, Color4Extensions.FromHex("8c66ff"), () => togglePage(Page.AdvancedInfo)),
                    }
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    X = -40,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(-6, 0),
                    Children = new Drawable[]
                    {
                        new BigButton("Watch Replay", FontAwesome.Solid.PlayCircle, Color4Extensions.FromHex("7fcc33"), Color4Extensions.FromHex("b2ff66"), watchReplay),
                        new BigButton("Play Again", FontAwesome.Solid.Redo, Color4Extensions.FromHex("3399cc"), Color4Extensions.FromHex("66ccff"), retry, width: 338),
                    }
                },
            }
        };

        // ── parts ────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// One of the design's boxes: leaning right (<see cref="SHEAR"/>, its top further right than its bottom), with rounded corners,
        /// within its own size (as the design's shapes are: the slant doesn't reach past it, so boxes side by side keep a gap).
        /// </summary>
        public partial class SlantedBox : CompositeDrawable
        {
            private readonly Container shape;

            public SlantedBox(ColourInfo colour, float alpha = 1, Color4? edge = null, float cornerRadius = 8)
            {
                InternalChild = shape = new Container
                {
                    Shear = new Vector2(SHEAR, 0),
                    Masking = true,
                    CornerRadius = cornerRadius,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colour, Alpha = alpha },
                };

                if (edge is Color4 border)
                {
                    shape.BorderThickness = 2;
                    shape.BorderColour = border;
                }
            }

            protected override void Update()
            {
                base.Update();

                // Sheared about its top: the top stays, the bottom moves left by the slant; so it starts that far in.
                float slant = SHEAR * DrawHeight;
                shape.X = slant;
                shape.Size = new Vector2(Math.Max(0, DrawWidth - slant), DrawHeight);
            }
        }

        /// <summary>
        /// A difficulty's name in its stars' colour, as the design has it; from 6.5 stars (where those turn dark, unreadable on the dark band)
        /// in the light orange lazer writes those stars in.
        /// </summary>
        public static Color4 DifficultyNameColour(OsuColour colours, double stars) => stars >= 6.5 ? colours.Orange1 : colours.ForStarDifficulty(stars);

        /// <summary>A number that rolls (eased as lazer's results' counters) to where it ends.</summary>
        public partial class CountingText : OsuSpriteText
        {
            private readonly Func<double, string> format;
            private double count;

            public double Count
            {
                get => count;
                set
                {
                    count = value;
                    Text = format(value);
                }
            }

            public CountingText(Func<double, string> format)
            {
                this.format = format;
                Count = 0;
            }

            public void RollTo(double target, double duration) => this.TransformTo(nameof(Count), target, duration, AccuracyCircle.ACCURACY_TRANSFORM_EASING);
        }

        /// <summary>Lazer's score counter (its roll and ticks) in the design's big light numbers.</summary>
        private partial class ScoreCounter : TotalScoreCounter
        {
            public ScoreCounter(bool playSamples)
                : base(playSamples)
            {
                RelativeSizeAxes = Axes.None;
                AutoSizeAxes = Axes.Both;
            }

            protected override OsuSpriteText CreateSpriteText() => base.CreateSpriteText().With(s =>
            {
                s.Anchor = Anchor.CentreLeft;
                s.Origin = Anchor.CentreLeft;
                // Fixed width, so the digits don't jostle as they roll.
                s.Font = OsuFont.Torus.With(size: 160, weight: FontWeight.Light, fixedWidth: true);
                s.Spacing = new Vector2(-14, 0);
                s.Colour = NUMBER_GRADIENT;
            });
        }

        /// <summary>A labelled number on a slanted box: a label, the value, and what it's out of (on the right).</summary>
        private partial class StatisticBox : Container
        {
            public readonly OsuSpriteText Value;
            private readonly OsuSpriteText maximum;

            public string Maximum
            {
                set => maximum.Text = value;
            }

            public StatisticBox(LocalisableString label, ColourInfo valueColour, Color4 fill, OsuSpriteText? value = null, float width = 250, float height = 80, float valueSize = 40,
                                float fillAlpha = 1, Color4? edge = null)
            {
                Size = new Vector2(width, height);
                Value = value ?? new OsuSpriteText();
                Value.Font = OsuFont.Torus.With(size: valueSize);
                Value.Colour = valueColour;
                Value.Anchor = Anchor.BottomLeft;
                Value.Origin = Anchor.BottomLeft;
                Value.Position = new Vector2(22, -6);

                Children = new Drawable[]
                {
                    new SlantedBox(fill, fillAlpha, edge) { RelativeSizeAxes = Axes.Both },
                    new OsuSpriteText
                    {
                        Position = new Vector2(24, 12),
                        Text = label,
                        Font = OsuFont.Torus.With(size: 16, weight: FontWeight.Bold),
                    },
                    Value,
                    maximum = new OsuSpriteText
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.BottomRight,
                        Position = new Vector2(-24, -10),
                        Font = OsuFont.Torus.With(size: valueSize / 2),
                        Colour = SUB_CONTENT,
                    },
                };
            }
        }

        /// <summary>A value in the header: its label over it (blank until known).</summary>
        private partial class HeaderValue : FillFlowContainer
        {
            public HeaderValue(string label, IBindable<int?> rank)
                : this(label, string.Empty)
            {
                var value = (OsuSpriteText)Children[1];
                rank.BindValueChanged(r => value.Text = r.NewValue is int place ? $"#{place:N0}" : "-", true);
            }

            public HeaderValue(string label, string value, float width = 88)
            {
                Width = width;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(0, 2);
                Anchor = Anchor.CentreLeft;
                Origin = Anchor.CentreLeft;
                Children = new Drawable[]
                {
                    new OsuSpriteText { Text = label, Font = OsuFont.Torus.With(size: 12, weight: FontWeight.SemiBold), Colour = SUB_CONTENT },
                    new OsuSpriteText { Text = value, Font = OsuFont.Torus.With(size: 16) },
                };
            }
        }

        /// <summary>The header's outlined pill ("← Ranking").</summary>
        private partial class HeaderPillButton : OsuClickableContainer
        {
            public HeaderPillButton(string text, IconUsage icon, Action action)
            {
                Action = action;
                Size = new Vector2(109, 36);
                Masking = true;
                CornerRadius = 18;
                BorderThickness = 3;
                BorderColour = Color4.White;
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(6, 0),
                        Children = new Drawable[]
                        {
                            new SpriteIcon { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Size = new Vector2(14), Icon = icon },
                            new OsuSpriteText { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Text = text, Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold) },
                        }
                    },
                };
            }
        }

        /// <summary>"Advanced Info →" at the header's right, or "← Basic Info" at its left: a dark box off the screen's edge.</summary>
        private partial class PageButton : OsuClickableContainer
        {
            public PageButton(string text, bool back, Action action)
            {
                Action = action;
                Size = new Vector2(268, 60);
                Masking = true;
                CornerRadius = 20;
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.8f },
                    new OsuSpriteText
                    {
                        Anchor = back ? Anchor.CentreRight : Anchor.CentreLeft,
                        Origin = back ? Anchor.CentreRight : Anchor.CentreLeft,
                        X = back ? -24 : 42,
                        Text = text,
                        Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold),
                    },
                    back
                        ? new CircledArrow(FontAwesome.Solid.ArrowLeft) { Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, X = -118 }
                        : new CircledArrow { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, X = 161 },
                };
            }
        }

        /// <summary>The arrow at the grade's right: on to the advanced info.</summary>
        private partial class ArrowButton : OsuClickableContainer
        {
            public ArrowButton(Action action)
            {
                Action = action;
                Size = new Vector2(42);
                Child = new CircledArrow();
            }
        }

        private partial class CircledArrow : CircularContainer
        {
            public CircledArrow(IconUsage? icon = null)
            {
                Size = new Vector2(42);
                Masking = true;
                BorderThickness = 3;
                BorderColour = Color4.White;
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                    new SpriteIcon { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(18), Icon = icon ?? FontAwesome.Solid.ArrowRight },
                };
            }
        }

        /// <summary>The bottom bar's big slanted buttons (Back, Watch Replay, Play Again): a colour fading to a lighter edge.</summary>
        private partial class BigButton : OsuClickableContainer
        {
            private readonly Container body;

            public BigButton(string text, IconUsage icon, Color4 colour, Color4 light, Action? action, bool iconFirst = false, float width = 238)
            {
                Action = action;
                Size = new Vector2(width, 70);
                Alpha = action != null ? 1 : 0;

                var label = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    // Off the middle, away from the icon.
                    X = iconFirst ? 20 : -24,
                    Text = text,
                    Font = OsuFont.TorusAlternate.With(size: 20, weight: FontWeight.SemiBold),
                };
                var symbol = new SpriteIcon
                {
                    Anchor = iconFirst ? Anchor.CentreLeft : Anchor.CentreRight,
                    Origin = Anchor.Centre,
                    X = iconFirst ? 48 : -40,
                    Size = new Vector2(30),
                    Icon = icon,
                };

                Child = body = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new SlantedBox(ColourInfo.GradientVertical(colour, colour), edge: light, cornerRadius: 10) { RelativeSizeAxes = Axes.Both },
                        symbol,
                        label,
                    }
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                body.FadeColour(Color4.White.Opacity(0.85f), 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                body.FadeColour(Color4.White, 100);
                base.OnHoverLost(e);
            }
        }

        /// <summary>The bottom bar's tabs (Save Replay, Screenshot, Ranking, More Info): an icon over a label, a coloured line under.</summary>
        private partial class TabButton : OsuClickableContainer
        {
            private readonly Box glow;

            public TabButton(string text, IconUsage icon, Color4 colour, Action? action)
            {
                Action = action;
                Size = new Vector2(150, 80);
                Alpha = action != null ? 1 : 0.4f;

                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Shear = new Vector2(SHEAR, 0),
                        Masking = true,
                        CornerRadius = 10,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = BACKGROUND_3 },
                            glow = new Box { RelativeSizeAxes = Axes.Both, Colour = ColourInfo.GradientVertical(colour.Opacity(0), colour.Opacity(0.25f)) },
                        }
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Y = 11,
                        Size = new Vector2(22),
                        Icon = icon,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Y = 44,
                        Text = text,
                        Font = OsuFont.TorusAlternate.With(size: 16, weight: FontWeight.SemiBold),
                    },
                    new Container
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Size = new Vector2(121, 3),
                        Masking = true,
                        CornerRadius = 1.5f,
                        Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                glow.FadeTo(2, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                glow.FadeTo(1, 100);
                base.OnHoverLost(e);
            }
        }
    }
}

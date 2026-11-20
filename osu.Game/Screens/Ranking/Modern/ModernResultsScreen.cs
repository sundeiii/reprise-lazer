// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays;
using osu.Framework.Threading;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.RankingV2.Argon;
using osu.Game.Skinning;
using osu.Game.Skinning.Ranking;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Modern
{
    /// <summary>
    /// The redesigned results screen (osu!'s design team's "Client / Result Screen"). The main page is osu!'s own pieces of it (its new
    /// results screen in the making, osu.Game.Screens.RankingV2.Argon): the beatmap, the player, the total score and the statistics
    /// down the left and the grade ring on the right. Ours, in the design's 1920x1080 (revision 11): the actions along the bottom, and
    /// the advanced info and ranking pages. Shown over <see cref="ResultsScreen"/> with
    /// <see cref="Configuration.OsuSetting.NewResultsScreen"/>.
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
        private IBindable<double?> performance = null!;

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

        // The main page is osu!'s own new results screen's pieces (osu.Game.Screens.RankingV2.Argon, the same design), placed as
        // osu!'s Argon skin places them; they read the score from this. The statistics have their own, with the pp, which can come
        // in late (the server's).
        private readonly Bindable<IScoreInfo> shownScore = new Bindable<IScoreInfo>();

        private Container statisticsHolder = null!;
        private ArgonScoreStatisticsGrid statisticsGrid = null!;
        private double? statisticsPP;

        private readonly List<IAnimatableSkinnable> animatables = new List<IAnimatableSkinnable>();
        private bool animationStarted;
        private RankChangePanel rankChangePanel = null!;

        // Whether the play just set is the player's new best on the beatmap, by the server's scores (null until it's known).
        private readonly Bindable<bool?> personalBest = new Bindable<bool?>();

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        // Whether the grade ring plays the results' sounds (score ticks, the rank, applause): for the play just finished (the game's
        // own results underneath are silent under this screen, so they come in time with this reveal, not the unseen one's).
        private readonly bool sounds;

        // The results screen's applause, for the play's grade.
        private readonly Action<ScoreRank>? applause;
        private ScheduledDelegate? pendingApplause;

        // As long after the grade lands as the game's own grade ring has it (AccuracyCircle).
        private const double applause_delay = 545;

        private ArgonScoreRankDisplay rankDisplay = null!;

        public ModernResultsScreen(ScoreInfo score, Action? retry, Action? watchReplay, LegacyRankingDetails? details = null, bool sounds = false,
                                   Action<ScoreRank>? applause = null)
        {
            this.sounds = sounds;
            this.applause = applause;
            this.score = score;
            this.retry = retry;
            this.watchReplay = watchReplay;
            this.details = details ?? new LegacyRankingDetails();

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            shownScore.Value = withDetails(details.Performance.Value ?? score.PP);

            ArgonBeatmapInfoPanel beatmapPanel;
            ArgonPlayerInfoPanel playerPanel;
            ArgonTotalScorePanel totalScorePanel;

            // As osu!'s Argon skin places them (ArgonSkin, GlobalSkinnableContainers.Results): the wedges down the left, each further
            // left by the slant the lower it is.
            var leftWedgeBase = new Vector2(-ShearedButton.CORNER_RADIUS);
            static Vector2 leftWedgeOffset(float y) => new Vector2(-y * OsuGame.SHEAR.X, y);

            InternalChildren = new Drawable[]
            {
                // The beatmap's background shows through, darkened a little (the design's 25% black).
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = 0.25f },
                pages = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    // At the game's own scale, as osu!'s screen has them.
                    Child = mainPage = new ScoreProvider(shownScore)
                    {
                        RelativeSizeAxes = Axes.Both,
                        // Hidden until the animation starts (the pieces would show finished for a frame before), but updated meanwhile:
                        // the pieces only set themselves up (LoadComplete) when it is.
                        Alpha = 0,
                        AlwaysPresent = true,
                        Children = new Drawable[]
                        {
                            // Silent unless this screen plays the results' sounds (see sounds).
                            new AudioContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Volume = { Value = sounds ? 1 : 0 },
                                Child = rankDisplay = new ArgonScoreRankDisplay(),
                            },
                            beatmapPanel = new ArgonBeatmapInfoPanel { Position = leftWedgeBase },
                            playerPanel = new ArgonPlayerInfoPanel { Position = leftWedgeBase + leftWedgeOffset(161) },
                            // Reprise: what the play did to the player's rank and pp, next to them (once the server says).
                            rankChangePanel = new RankChangePanel { Position = leftWedgeBase + leftWedgeOffset(161) + new Vector2(ArgonBeatmapInfoPanel.WIDTH + 24, 0) },
                            totalScorePanel = new ArgonTotalScorePanel
                            {
                                Position = leftWedgeBase + leftWedgeOffset(233),
                                // "Personal best" for a play just set by the server's scores (osu!'s goes by the scores stored here,
                                // which missed it); a score looked at later keeps osu!'s.
                                PersonalBest = this.details.HasSecondPage ? personalBest : null,
                                // And by how much it beat the last best.
                                PreviousBest = this.details.HasSecondPage ? this.details.PreviousBest : null,
                            },
                            statisticsHolder = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = createStatistics(details.Performance.Value ?? score.PP),
                            },
                        }
                    },
                },
                designScale(createBottomBar()),
            };

            animatables.AddRange(new IAnimatableSkinnable[] { rankDisplay, beatmapPanel, playerPanel, totalScorePanel, statisticsGrid });
        }

        /// <summary>The statistics (accuracy, combo, pp, the judgements and mods), with the given pp, reading a score of their own.</summary>
        private Drawable createStatistics(double? pp)
        {
            statisticsPP = pp;

            return new ScoreProvider(new Bindable<IScoreInfo>(withDetails(pp)))
            {
                RelativeSizeAxes = Axes.Both,
                Child = statisticsGrid = new ArgonScoreStatisticsGrid { Position = new Vector2(20, 354) },
            };
        }

        /// <summary>
        /// Laid out in the design's 1920x1080, scaled to fit; things on the right stay on the right on wider screens.
        /// </summary>
        private static Drawable designScale(Drawable content) => new DrawSizePreservingFillContainer
        {
            TargetDrawSize = new Vector2(1920, 1080),
            Strategy = DrawSizePreservationStrategy.Minimum,
            Child = content,
        };

        /// <summary>
        /// The score with what the results screen knows of it beyond the score itself: its pp (the server's, by Reprise's rules, else
        /// what the results screen worked out) and the player's place on the beatmap.
        /// </summary>
        private ScoreInfo withDetails(double? pp)
        {
            var shown = score.DeepClone();
            shown.PP = pp;
            shown.Position = details.UserBestPosition.Value ?? score.Position;
            return shown;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The play's pp: the server's (Reprise's rules), else what the results screen worked out, else the score's own.
            performance = details.Performance.GetBoundCopy();
            performance.BindValueChanged(pp =>
            {
                if (pp.NewValue == null || pp.NewValue == statisticsPP)
                    return;

                // The statistics made again with it (as they end up, if they've already animated): a score bindable won't take a copy of
                // the same score with another pp (realm objects' equality), and changing the others' would start their animations over.
                animatables.Remove(statisticsGrid);
                statisticsHolder.Child = createStatistics(pp.NewValue);
                animatables.Add(statisticsGrid);
            }, true);

            if (details.HasSecondPage)
            {
                details.PreviousBest.BindValueChanged(_ => updatePersonalBest());
                details.StatisticsUpdate.BindValueChanged(_ => updatePersonalBest(), true);
                details.StatisticsUpdate.BindValueChanged(_ => showRankChange());
            }

            fetchRanks();
        }

        // Set a moment into the reveal (once the wedges are in).
        private bool rankChangeReady;

        // Reprise: the rank and pp change, once the reveal has got going and the server's update for the play is in (whichever is later).
        private void showRankChange()
        {
            if (rankChangeReady && details.StatisticsUpdate.Value is ScoreBasedUserStatisticsUpdate update)
                rankChangePanel.Show(update);
        }

        private void updatePersonalBest()
        {
            var previous = details.PreviousBest.Value;

            // Beat the last best; with none, the first play there (known once the server's update for this play arrived).
            if (previous != null)
                personalBest.Value = score.TotalScore > previous.TotalScore;
            else if (details.StatisticsUpdate.Value != null)
                personalBest.Value = true;
        }

        protected override void Update()
        {
            base.Update();

            // Once the pieces have set themselves up (their LoadComplete runs after this screen's).
            if (!animationStarted && animatables.All(a => ((Drawable)a).IsLoaded))
            {
                animationStarted = true;

                // Reprise: a full combo just played gets its moment first (the storyboard's), the results after it.
                if (sounds && FullComboIntro.IsFullCombo(score))
                {
                    AddInternal(fullComboIntro = new FullComboIntro { Finished = beginReveal });
                    return;
                }

                beginReveal();
            }
        }

        private FullComboIntro? fullComboIntro;

        /// <summary>The results' own reveal (after the full combo moment, when there's one).</summary>
        private void beginReveal()
        {
            startAnimation();
            mainPage.Show();

            // The rank and pp change once the wedges are in.
            if (details.HasSecondPage)
            {
                Scheduler.AddDelayed(() =>
                {
                    rankChangeReady = true;
                    showRankChange();
                }, 800);
            }
        }

        /// <summary>
        /// The pieces' reveal, in the order of their groups as osu!'s skinnable results screen sequences them (the wedges, then the
        /// statistics, then the grade ring filling up).
        /// </summary>
        private void startAnimation()
        {
            foreach (var a in animatables)
                a.FinishAnimating();

            double endTime = Time.Current;

            foreach (var group in animatables.GroupBy(a => a.GroupNumber).OrderBy(g => g.Key))
            {
                double groupStart = endTime;

                foreach (var a in group)
                {
                    endTime = Math.Max(endTime, a.StartAnimating(groupStart));

                    // The applause once the grade has landed (osu!'s ring lands it when the total score has finished rolling).
                    if (a == rankDisplay && sounds && applause != null)
                    {
                        pendingApplause?.Cancel();
                        pendingApplause = Scheduler.AddDelayed(playApplause,
                            groupStart + ArgonTotalScorePanel.TotalScoreCounter.ROLLING_DURATION + applause_delay - Time.Current);
                    }
                }
            }
        }

        private void playApplause()
        {
            pendingApplause = null;
            applause?.Invoke(score.Rank);
        }

        /// <summary>
        /// Ends the reveal at once (everything as it ends up).
        /// </summary>
        public void FinishAnimation()
        {
            // Past the full combo moment too (the reveal starts from its end, then finishes).
            fullComboIntro?.Skip();

            foreach (var a in animatables)
                a.FinishAnimating();

            // Skipped to the end: the applause now rather than after the reveal that's no longer running.
            if (pendingApplause != null)
            {
                pendingApplause.Cancel();
                playApplause();
            }
        }

        /// <summary>The score the pieces inside read (osu!'s results pieces resolve it).</summary>
        private partial class ScoreProvider : Container
        {
            [Cached(typeof(IBindable<IScoreInfo>))]
            private readonly Bindable<IScoreInfo> score;

            public ScoreProvider(Bindable<IScoreInfo> score)
            {
                this.score = score;
            }
        }

        /// <summary>Over to a page (the bottom bar's tabs), or back to the main one from it.</summary>
        private void togglePage(Page page) => showPage(currentPage == page ? Page.Main : page);

        /// <summary>A page (the advanced info and ranking made the first time they're shown).</summary>
        private void showPage(Page page)
        {
            currentPage = page;

            // These two are in the design's 1920x1080, as the bottom bar.
            if (page == Page.AdvancedInfo && advancedPage == null)
                pages.Add(designScale(advancedPage = new AdvancedInfoPage(score, details, createHeader) { Alpha = 0 }));
            if (page == Page.Ranking && rankingPage == null)
                pages.Add(designScale(rankingPage = new RankingPage(score) { Alpha = 0 }));

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

        /// <summary>The advanced info's header: "← Basic Info" before it.</summary>
        private Drawable createHeader()
        {
            var user = score.User;
            int? position = details.UserBestPosition.Value ?? score.Position;

            var header = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 60,
                Y = 73,
                Padding = new MarginPadding { Left = 176, Right = 24 },
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
                    // "Basic Info", at the header's left: back to the main page.
                    new PageButton("Basic Info", () => showPage(Page.Main))
                    {
                        Position = new Vector2(-100, 73),
                    },
                }
            };
        }

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
                    // An even 4px between the tabs' slanted edges (each tab's shape stays within its width).
                    Spacing = new Vector2(-6, 0),
                    Children = new Drawable[]
                    {
                        new BigButton("Back", FontAwesome.Solid.ArrowCircleLeft, Color4Extensions.FromHex("de31ae"), Color4Extensions.FromHex("ff86dd"), details.Back, iconFirst: true)
                        {
                            Margin = new MarginPadding { Top = 0, Right = 26 },
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

        /// <summary>"← Basic Info" at the header's left: a dark box off the screen's edge.</summary>
        private partial class PageButton : OsuClickableContainer
        {
            public PageButton(string text, Action action)
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
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -24,
                        Text = text,
                        Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold),
                    },
                    new CircledArrow(FontAwesome.Solid.ArrowLeft) { Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight, X = -118 },
                };
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

            private const float width = 150;
            private const float height = 80;

            // Sheared about its top, as SlantedBox is: the top starts this far in, the bottom at the left edge, so it stays within its
            // own width (it reached under the tab to its left).
            private const float slant = SHEAR * height;

            /// <summary>How far right of the button's middle the slanted shape's middle is, at <paramref name="y"/> from its top.</summary>
            private static float across(float y) => slant * (0.5f - y / height);

            public TabButton(string text, IconUsage icon, Color4 colour, Action? action)
            {
                Action = action;
                Size = new Vector2(width, height);
                Alpha = action != null ? 1 : 0.4f;

                Children = new Drawable[]
                {
                    new Container
                    {
                        X = slant,
                        Size = new Vector2(width - slant, height),
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
                        // Each piece centred on the slant at its own height.
                        X = across(11 + 22 / 2f),
                        Y = 11,
                        Size = new Vector2(22),
                        Icon = icon,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        X = across(44 + 8),
                        Y = 44,
                        Text = text,
                        Font = OsuFont.TorusAlternate.With(size: 16, weight: FontWeight.SemiBold),
                    },
                    new Container
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        X = across(height - 1.5f),
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

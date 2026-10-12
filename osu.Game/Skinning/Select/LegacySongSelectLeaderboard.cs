// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Select;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Select
{
    /// <summary>
    /// The rankings osu!stable's song select can show.
    /// </summary>
    public enum LegacyRankingType
    {
        Local,
        Country,
        Global,
        SelectedMods,
        Friends,
    }

    /// <summary>
    /// osu!stable's song select leaderboard on the left: the ranking choice, the scores (scrolling) and, for online rankings,
    /// the player's best at the bottom.
    /// </summary>
    /// <remarks>
    /// Laid out like stable's (its 640x480 positions times 1.6, in the 1024x768 layout).
    /// </remarks>
    public partial class LegacySongSelectLeaderboard : CompositeDrawable
    {
        private const float row_spacing = 33 * 1.6f;

        // Stable's choices, in its order.
        private static readonly (string Text, LegacyRankingType Value)[] ranking_choices =
        {
            ("Local Ranking", LegacyRankingType.Local),
            ("Country Ranking", LegacyRankingType.Country),
            ("Global Ranking", LegacyRankingType.Global),
            ("Global Ranking (Selected Mods)", LegacyRankingType.SelectedMods),
            ("Friend Ranking", LegacyRankingType.Friends),
        };

        /// <summary>
        /// The ranking choice box and the skin's "rank-forum" button beside it, to go above everything else
        /// (like stable's, above the skin's footer art; song select's top would take their clicks otherwise).
        /// </summary>
        public readonly Container Controls;

        public readonly Bindable<LegacyRankingType> RankingType = new Bindable<LegacyRankingType>();

        [Resolved]
        private LeaderboardManager leaderboardManager { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        private readonly IBindable<LeaderboardScores?> scores = new Bindable<LeaderboardScores?>();

        private Bindable<BeatmapDetailTab> configTab = null!;
        private Bindable<bool> configModsFilter = null!;
        private Bindable<ScoringMode> scoringMode = null!;

        private OsuSpriteText selectedModsText = null!;
        private OsuSpriteText scoreMultiplierText = null!;

        // Stable's names for the mods, in its order (others after, by acronym).
        private static readonly (string Acronym, string Name)[] stable_mod_names =
        {
            ("CN", "Cinema"), ("AT", "Auto"), ("TP", "TargetPractice"), ("SO", "SpunOut"), ("EZ", "Easy"), ("NF", "NoFail"), ("HD", "Hidden"),
            ("FI", "FadeIn"), ("HR", "HardRock"), ("PF", "Perfect"), ("SD", "SuddenDeath"), ("NC", "Nightcore"), ("DT", "DoubleTime"),
            ("HT", "HalfTime"), ("FL", "Flashlight"), ("RX", "Relax"), ("AP", "AutoPilot"), ("RD", "Random"), ("DS", "Co-op"), ("MR", "Mirror"),
        };

        private OsuScrollContainer scroll = null!;
        private Container<LegacyLeaderboardRow> rows = null!;
        private Container personalBest = null!;
        private Container message = null!;

        private LeaderboardCriteria? requested;
        private ScheduledDelegate? pendingFetch;
        private bool fetchedOnce;

        public LegacySongSelectLeaderboard()
        {
            RelativeSizeAxes = Axes.Both;

            Controls = new Container
            {
                RelativeSizeAxes = Axes.Both,
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, ISkinSource skin)
        {
            // Stable 2015's has a blue border and size 14.
            Controls.Add(new LegacySelectBox<LegacyRankingType>(RankingType, ranking_choices, 190 * 1.6f)
            {
                Position = new Vector2(5, 73.5f) * 1.6f,
                // Stable's rounded box (2015's and the later one's alike).
                Stable2015 = true,
                BorderColour2015 = new Color4(31, 184, 255, 180),
                TextSize2015 = 14,
            });

            configTab = config.GetBindable<BeatmapDetailTab>(OsuSetting.BeatmapDetailTab);
            configModsFilter = config.GetBindable<bool>(OsuSetting.BeatmapDetailModsFilter);
            scoringMode = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode);

            // Song select's own leaderboard keeps its choice in these; this one shares it.
            RankingType.Value = configTab.Value switch
            {
                BeatmapDetailTab.Local => LegacyRankingType.Local,
                BeatmapDetailTab.Country => LegacyRankingType.Country,
                BeatmapDetailTab.Friends => LegacyRankingType.Friends,
                _ => configModsFilter.Value ? LegacyRankingType.SelectedMods : LegacyRankingType.Global,
            };

            // Stable's link to the beatmap's page, beside the ranking choice (only when the skin has one).
            var forumTexture = skin.GetTexture(@"rank-forum");

            if (forumTexture != null)
                Controls.Add(new ForumButton(forumTexture, showBeatmapPage) { Position = new Vector2(214, 81) * 1.6f });

            InternalChildren = new Drawable[]
            {
                // Stable's: the selected mods written out, faint, low on the left (60,397 in its 640x480), under the scores.
                selectedModsText = new OsuSpriteText
                {
                    Position = new Vector2(60, 397) * 1.6f,
                    Font = OsuFont.GetFont(size: 48, weight: FontWeight.Bold),
                    Colour = new Color4(255, 255, 255, 128),
                },
                // Not stable's: the mods' score multiplier under them (stable shows it only in its mod selection).
                scoreMultiplierText = new OsuSpriteText
                {
                    Position = new Vector2(60, 397) * 1.6f + new Vector2(2, 56),
                    Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold),
                    Colour = new Color4(255, 255, 255, 128),
                },
                // Stable's: from 92 down (its 640x480), 290 by 267. Measured against stable: the rows start 5 in.
                scroll = new OsuScrollContainer
                {
                    Position = new Vector2(5, 92 * 1.6f),
                    Size = new Vector2(290, 267) * 1.6f,
                    ScrollbarVisible = false,
                    // Not a flow: the rows slide in from the side.
                    Child = rows = new Container<LegacyLeaderboardRow>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Top = 4 * 1.6f },
                    },
                },
                personalBest = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
                message = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            scores.BindTo(leaderboardManager.Scores);
            scores.BindValueChanged(_ => Schedule(showScores));

            RankingType.BindValueChanged(t =>
            {
                configTab.Value = t.NewValue switch
                {
                    LegacyRankingType.Local => BeatmapDetailTab.Local,
                    LegacyRankingType.Country => BeatmapDetailTab.Country,
                    LegacyRankingType.Friends => BeatmapDetailTab.Friends,
                    _ => BeatmapDetailTab.Global,
                };
                configModsFilter.Value = t.NewValue == LegacyRankingType.SelectedMods;

                Refetch();
            });

            // The multiplier depends on the map and its ruleset too.
            beatmap.BindValueChanged(_ =>
            {
                showSelectedMods(mods.Value);
                Refetch();
            });
            ruleset.BindValueChanged(_ =>
            {
                showSelectedMods(mods.Value);
                Refetch();
            });
            mods.BindValueChanged(m =>
            {
                showSelectedMods(m.NewValue);

                if (RankingType.Value == LegacyRankingType.SelectedMods)
                    Refetch();
            });
            showSelectedMods(mods.Value);
            scoringMode.BindValueChanged(_ => showScores());

            Refetch();
        }

        private void showSelectedMods(IReadOnlyList<Mod> selected)
        {
            selectedModsText.Text = stableModNames(selected);
            scoreMultiplierText.Text = selected.Count == 0
                ? string.Empty
                : LegacyModSelect.ScoreMultiplierText(ruleset.Value, beatmap.Value.BeatmapInfo, selected);
        }

        private void showBeatmapPage()
        {
            int id = beatmap.Value.BeatmapInfo.OnlineID;

            if (id > 0)
                game?.ShowBeatmap(id);
        }

        // Stable's: "Hidden,DoubleTime" (Nightcore without its Double Time, Perfect without its Sudden Death).
        private static string stableModNames(IReadOnlyList<Mod> selected)
        {
            var acronyms = selected.Select(m => m.Acronym).ToHashSet();

            if (acronyms.Contains("NC"))
                acronyms.Remove("DT");
            if (acronyms.Contains("PF"))
                acronyms.Remove("SD");

            var names = stable_mod_names.Where(n => acronyms.Remove(n.Acronym)).Select(n => n.Name)
                                        .Concat(acronyms.OrderBy(a => a));

            return string.Join(",", names);
        }

        /// <summary>
        /// Gets the scores again (after a play, for one).
        /// </summary>
        public void Refetch()
        {
            pendingFetch?.Cancel();
            requested = null;
            clear();

            if (beatmap.IsDefault || beatmap.Value is DummyWorkingBeatmap)
                return;

            var scope = RankingType.Value switch
            {
                LegacyRankingType.Local => BeatmapLeaderboardScope.Local,
                LegacyRankingType.Country => BeatmapLeaderboardScope.Country,
                LegacyRankingType.Friends => BeatmapLeaderboardScope.Friend,
                _ => BeatmapLeaderboardScope.Global,
            };

            // Online lookups wait for the selection to settle, like song select's own leaderboard.
            pendingFetch = Scheduler.AddDelayed(() =>
            {
                var criteria = new LeaderboardCriteria(beatmap.Value.BeatmapInfo, ruleset.Value ?? beatmap.Value.BeatmapInfo.Ruleset, scope,
                    RankingType.Value == LegacyRankingType.SelectedMods ? mods.Value.ToArray() : null);

                requested = criteria;
                leaderboardManager.FetchWithCriteria(criteria, forceRefresh: true);
            }, fetchedOnce && scope != BeatmapLeaderboardScope.Local ? 300 : 0);

            fetchedOnce = true;
        }

        private void clear()
        {
            rows.Clear();
            personalBest.Clear();
            message.Clear();
        }

        private void showScores()
        {
            var result = scores.Value;

            // Only what this asked for (song select's own leaderboard asks the same manager).
            if (result == null || requested == null || leaderboardManager.CurrentCriteria != requested)
                return;

            clear();

            if (result.FailState != null)
            {
                showMessage(result.FailState.Value);
                return;
            }

            var top = result.TopScores.ToList();

            if (top.Count == 0)
            {
                showNoRecords();
                return;
            }

            long[] displayScores = top.Select(s => s.GetDisplayScore(scoringMode.Value)).ToArray();

            for (int i = 0; i < top.Count; i++)
            {
                long next = i < top.Count - 1 ? displayScores[i + 1] : displayScores[i];

                // Stable shows avatars on online rankings only.
                var row = new LegacyLeaderboardRow(top[i], displayScores[i], i + 1, displayScores[i] - next, top[i].User.Username,
                    showAvatar: requested.Scope != BeatmapLeaderboardScope.Local)
                {
                    Y = i * row_spacing,
                };
                rows.Add(row);
                row.AnimateIn(i * 50);
            }

            scroll.ScrollToStart(false);

            if (requested.Scope != BeatmapLeaderboardScope.Local)
                showPersonalBest(result);
        }

        // Stable's: "Personal Best" over the player's best, below the list (online rankings only).
        private void showPersonalBest(LeaderboardScores result)
        {
            personalBest.Add(new OsuSpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopCentre,
                // Measured against stable (its code from 2015 has these 10-20 lower).
                Position = new Vector2(120 * 1.6f, 570),
                Text = "Personal Best",
                Font = OsuFont.GetFont(size: 21, weight: FontWeight.Bold),
            });

            var userScore = result.UserScore;

            if (userScore == null)
            {
                personalBest.Add(new OsuSpriteText
                {
                    Position = new Vector2((58 + 3) * 1.6f, 591 + 6 * 1.6f),
                    Text = "No personal record set",
                    Font = OsuFont.GetFont(size: 24),
                });
                return;
            }

            int position = userScore.Position ?? 0;
            string text = result.TotalScores > position && position > 0
                ? $"#{position:#,0} of {result.TotalScores:#,0}"
                : $"#{position:#,0} {userScore.User.Username}";

            var row = new LegacyLeaderboardRow(userScore, userScore.GetDisplayScore(scoringMode.Value), position, 0, text)
            {
                Position = new Vector2(3 * 1.6f, 591),
                RelativeSizeAxes = Axes.None,
                Width = 290 * 1.6f,
            };

            personalBest.Add(row);
            row.AnimateIn(400);
        }

        private void showNoRecords()
        {
            var texture = skin.GetTexture(@"selection-norecords");

            Drawable content = texture != null
                ? new Sprite { Texture = texture }
                : createNoRecordsBox();

            content.Anchor = Anchor.TopLeft;
            content.Origin = Anchor.Centre;
            content.Position = new Vector2(118, 170) * 1.6f;
            message.Add(content);

            // Stable's: in and down a little.
            content.FadeOut().FadeIn(400).MoveToOffset(new Vector2(0, 30 * 1.6f), 400, Easing.Out);
        }

        // Like stable's default "selection-norecords": a white-bordered black box, a trophy, and the words in light blue.
        private static Drawable createNoRecordsBox() => new Container
        {
            Size = new Vector2(333, 38),
            Masking = true,
            BorderColour = Color4.White,
            BorderThickness = 3,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    X = 24,
                    Size = new Vector2(22),
                    Icon = FontAwesome.Solid.Trophy,
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    X = 16,
                    Text = "No records set!",
                    Font = OsuFont.GetFont(size: 28, weight: FontWeight.Light),
                    Colour = new Color4(90, 190, 255, 255),
                },
            }
        };

        private void showMessage(LeaderboardFailState state)
        {
            string text = state switch
            {
                LeaderboardFailState.NotLoggedIn => "Please sign in to view online rankings!",
                LeaderboardFailState.NotSupporter => "You need to be a supporter to use this ranking.",
                LeaderboardFailState.NoTeam => "You need to be in a team to use this ranking.",
                LeaderboardFailState.NetworkFailure => "Couldn't get the rankings.",
                LeaderboardFailState.BeatmapUnavailable or LeaderboardFailState.RulesetUnavailable => "No online rankings for this beatmap.",
                _ => string.Empty,
            };

            message.Add(new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold))
            {
                Position = new Vector2(118, 170) * 1.6f,
                Origin = Anchor.Centre,
                Width = 340,
                AutoSizeAxes = Axes.Y,
                TextAnchor = Anchor.TopCentre,
                Text = text,
            });
        }

        /// <summary>
        /// The skin's "rank-forum": grows a little on hover, opens the beatmap's page.
        /// </summary>
        private partial class ForumButton : ClickableContainer
        {
            private readonly Sprite sprite;

            public ForumButton(Texture texture, Action action)
            {
                Action = action;
                Origin = Anchor.Centre;
                AutoSizeAxes = Axes.Both;

                Child = sprite = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Texture = texture,
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                sprite.ScaleTo(1.1f, 50);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                sprite.ScaleTo(1, 50);
                base.OnHoverLost(e);
            }
        }

        /// <summary>
        /// One of stable's leaderboard rows: the faint bar (pulsing to the beat), the avatar, grade, name, "Score: x (combo)",
        /// and on the right the mods, accuracy and how far ahead of the next score it is. Clicking it shows the score.
        /// </summary>
        public partial class LegacyLeaderboardRow : BeatSyncedContainer, IHasTooltip
        {
            private readonly ScoreInfo score;
            private readonly long displayScore;
            private readonly int rank;
            private readonly long lead;
            private readonly string name;
            private readonly bool showAvatar;

            private Drawable background = null!;
            private OsuSpriteText rankText = null!;
            private UpdateableAvatar avatar = null!;
            private SkinnableSound hoverSound = null!;

            [Resolved]
            private ISongSelect? songSelect { get; set; }

            public LegacyLeaderboardRow(ScoreInfo score, long displayScore, int rank, long lead, string name, bool showAvatar = true)
            {
                this.showAvatar = showAvatar;
                this.score = score;
                this.displayScore = displayScore;
                this.rank = rank;
                this.lead = lead;
                this.name = name;

                RelativeSizeAxes = Axes.X;
                Height = row_spacing;
            }

            [BackgroundDependencyLoader]
            private void load(ISkinSource skin)
            {
                var barTexture = skin.GetTexture(@"menu-button-background");
                const float centre_y = 15 * 1.6f;

                Children = new[]
                {
                    background = barTexture != null
                        ? new Sprite
                        {
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.CentreLeft,
                            Y = centre_y,
                            Texture = barTexture,
                            Scale = new Vector2(0.55f),
                            Colour = Color4.Black,
                            Alpha = 0.3f,
                        }
                        : new Box
                        {
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.CentreLeft,
                            Y = centre_y,
                            Size = new Vector2(240 * 1.6f, 56),
                            Colour = Color4.Black,
                            Alpha = 0.3f,
                        },
                    avatar = new UpdateableAvatar(showAvatar ? score.User : null, isInteractive: false, showUserPanelOnHover: false)
                    {
                        Alpha = showAvatar ? 1 : 0,
                        Origin = Anchor.Centre,
                        Position = new Vector2(18 * 1.6f, centre_y),
                        Size = new Vector2(46),
                    },
                    // Shown on hover, over the avatar.
                    rankText = new OsuSpriteText
                    {
                        Origin = Anchor.Centre,
                        Position = new Vector2(18 * 1.6f, centre_y),
                        Text = rank > 0 ? rank.ToString(CultureInfo.InvariantCulture) : string.Empty,
                        Font = OsuFont.GetFont(size: 28, weight: FontWeight.Bold),
                        Alpha = 0,
                    },
                    new Sprite
                    {
                        Origin = Anchor.Centre,
                        Position = new Vector2(46, 14.6f) * 1.6f,
                        Texture = skin.GetTexture($"{DrawableRank.GetLegacyRankTextureName(score.Rank)}-small"),
                    },
                    new OsuSpriteText
                    {
                        X = 58 * 1.6f,
                        Text = name,
                        Font = OsuFont.GetFont(size: 24),
                    },
                    new OsuSpriteText
                    {
                        Position = new Vector2(58, 15) * 1.6f,
                        Text = string.Create(CultureInfo.InvariantCulture, $"Score: {displayScore:#,0} ({score.MaxCombo:#,0}x)"),
                        Font = OsuFont.GetFont(size: 18),
                    },
                    // Mods, accuracy, and how far ahead of the next score.
                    new FillFlowContainer
                    {
                        Origin = Anchor.TopRight,
                        X = 236 * 1.6f,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            rightLine(string.Join(",", score.Mods.Select(m => m.Acronym))),
                            rightLine(score.Accuracy.ToString("0.00%", CultureInfo.InvariantCulture)),
                            rightLine(lead > 0 ? string.Create(CultureInfo.InvariantCulture, $"+{lead:#,0}") : "-"),
                        }
                    },
                    hoverSound = new SkinnableSound(new SampleInfo("click-short")),
                };

                // Recent scores: when (stable shows it for four days).
                double hours = (DateTimeOffset.Now - score.Date).TotalHours;

                if (hours < 96)
                {
                    Add(new SpriteIcon
                    {
                        Origin = Anchor.Centre,
                        Position = new Vector2(248, 15) * 1.6f,
                        Size = new Vector2(18),
                        Icon = FontAwesome.Regular.ArrowAltCircleUp,
                    });
                    Add(new OsuSpriteText
                    {
                        Origin = Anchor.CentreLeft,
                        Position = new Vector2(258, 15) * 1.6f,
                        Text = timeAgo(hours),
                        Font = OsuFont.GetFont(size: 15),
                    });
                }
            }

            // Each line keeps its height when empty (no mods): the accuracy stays in the middle, like stable's.
            private static Drawable rightLine(string text) => new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                AutoSizeAxes = Axes.X,
                Height = 16,
                Child = new OsuSpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Text = text,
                    Font = OsuFont.GetFont(size: 15),
                },
            };

            // Stable's: days from two days, then hours, minutes, seconds.
            private static string timeAgo(double hours)
            {
                if (hours > 47)
                    return $"{(int)(hours / 24)}d";
                if (hours >= 1)
                    return $"{(int)hours}h";
                if (hours > 1.0 / 60)
                    return $"{(int)(hours * 60)}m";

                return $"{(int)(hours * 3600)}s";
            }

            /// <summary>
            /// Slides in from the left after <paramref name="delay"/>, like stable's.
            /// </summary>
            public void AnimateIn(double delay)
            {
                this.FadeOut().MoveToX(-200 * 1.6f)
                    .Delay(delay)
                    .FadeIn(200)
                    .MoveToX(0, 400, Easing.OutQuint);
            }

            protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
            {
                base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);

                // Stable's bars pulse grey on the beat.
                double back = Math.Max(100, timingPoint.BeatLength - 150);
                background.FadeColour(new Color4(100, 100, 100, 255), 150).Then().FadeColour(Color4.Black, back);
            }

            /// <summary>
            /// Stable's tooltip: when it was set, its judgements (osu!mania's as MAX/300/200/Bad), accuracy and mods.
            /// </summary>
            public LocalisableString TooltipText
            {
                get
                {
                    int count(HitResult result) => score.Statistics.GetValueOrDefault(result);

                    string date = score.Date.ToLocalTime().DateTime.ToString(CultureInfo.CurrentCulture);
                    string judgements = score.Ruleset.OnlineID == 3
                        ? $"MAX:{count(HitResult.Perfect)} 300:{count(HitResult.Great)} 200:{count(HitResult.Good)} Bad:{count(HitResult.Ok) + count(HitResult.Meh) + count(HitResult.Miss)}"
                        : $"300:{count(HitResult.Great)} 100:{count(HitResult.Ok)} 50:{count(HitResult.Meh)} Miss:{count(HitResult.Miss)}";
                    string mods = score.Mods.Length == 0 ? "None" : string.Join(",", score.Mods.Select(m => m.Name.Replace(" ", string.Empty)));

                    return string.Create(CultureInfo.InvariantCulture, $"Achieved on {date}\n{judgements}\nAccuracy:{score.Accuracy * 100:0.00}%\nMods:{mods}");
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeTo(0.6f, 200);
                rankText.FadeIn(100);
                avatar.FadeColour(Color4.DarkGray, 100);
                hoverSound.Play();
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeTo(0.3f, 200);
                rankText.FadeOut(100);
                avatar.FadeColour(Color4.White, 100);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (songSelect?.CanPresentScore != true)
                    return false;

                songSelect.PresentScore(score);
                return true;
            }
        }
    }
}

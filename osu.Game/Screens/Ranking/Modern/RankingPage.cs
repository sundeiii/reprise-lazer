// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Users.Drawables;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Modern
{
    /// <summary>
    /// The redesigned results screen's ranking (revision 11's "Ranking Screen"): the beatmap's leaderboard (global, country, team, friends
    /// or local), filtered to exact mods when any are picked, with the player's own best pinned over it. Laid out in the design's 1920x1080
    /// (inside <see cref="ModernResultsScreen"/>).
    /// </summary>
    public partial class RankingPage : CompositeDrawable
    {
        private readonly ScoreInfo score;

        // Its own: the game's is song select's (changing it here would change that one's).
        private readonly LeaderboardManager leaderboard = new LeaderboardManager();

        private readonly Bindable<BeatmapLeaderboardScope> scope = new Bindable<BeatmapLeaderboardScope>(BeatmapLeaderboardScope.Global);
        private readonly List<ModIcon> modFilter = new List<ModIcon>();

        // The play's judgements (300, 100, 50, miss in osu!): the table's columns.
        private readonly HitResult[] judgements;

        private Container pinned = null!;
        private FillFlowContainer rows = null!;
        private OsuSpriteText status = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        public RankingPage(ScoreInfo score)
        {
            this.score = score;
            judgements = score.GetStatisticsForDisplay().Where(s => s.MaxCount == null && s.Result.IsBasic()).Select(s => s.Result).Take(4).ToArray();
            RelativeSizeAxes = Axes.Both;
        }

        // The leaderboard's mods are the play's (Relax and Autopilot have their own leaderboards), not song select's picked ones.
        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IBindable<IReadOnlyList<Mod>>>(new Bindable<IReadOnlyList<Mod>>(score.Mods));
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var ruleset = score.Ruleset.CreateInstance();

            AddInternal(leaderboard);
            AddInternal(new Container
            {
                Position = new Vector2(57, 85),
                RelativeSizeAxes = Axes.X,
                Height = 885,
                Padding = new MarginPadding { Right = 57 * 2 },
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 20,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = ModernResultsScreen.BACKGROUND_5, Alpha = 0.95f },
                        new BeatmapStrip(score, beatmaps.GetWorkingBeatmap(score.BeatmapInfo)),
                        // Global, Country, Team, Friends, Local.
                        new FillFlowContainer<ScopeTab>
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Y = 112,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Children = new[]
                            {
                                new ScopeTab("Global", BeatmapLeaderboardScope.Global, scope),
                                new ScopeTab("Country", BeatmapLeaderboardScope.Country, scope),
                                new ScopeTab("Team", BeatmapLeaderboardScope.Team, scope),
                                new ScopeTab("Friends", BeatmapLeaderboardScope.Friend, scope),
                                new ScopeTab("Local", BeatmapLeaderboardScope.Local, scope),
                            }
                        },
                        // The mods to filter by (exactly those, when any are picked).
                        new FillFlowContainer
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Y = 166,
                            Width = 1200,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Full,
                            Spacing = new Vector2(2),
                            ChildrenEnumerable = ruleset.CreateAllMods()
                                                        .Where(m => m.UserPlayable && m.Type != ModType.System)
                                                        .GroupBy(m => m.Type)
                                                        .Select(g => new FillFlowContainer
                                                        {
                                                            Anchor = Anchor.TopCentre,
                                                            Origin = Anchor.TopCentre,
                                                            AutoSizeAxes = Axes.Both,
                                                            Direction = FillDirection.Horizontal,
                                                            Spacing = new Vector2(2, 0),
                                                            ChildrenEnumerable = g.Select(createModToggle),
                                                        }),
                        },
                        // The scores.
                        new Container
                        {
                            Y = 281,
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Bottom = 281 },
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, Colour = ModernResultsScreen.BACKGROUND_4, Alpha = 0.5f },
                                pinned = new Container
                                {
                                    Position = new Vector2(24, 48),
                                    RelativeSizeAxes = Axes.X,
                                    Height = 80,
                                    Padding = new MarginPadding { Right = 48 },
                                    Alpha = 0,
                                },
                                new ScoreRow.Header(judgements, score.Ruleset.OnlineID) { Position = new Vector2(24, 146) },
                                new OsuScrollContainer
                                {
                                    Position = new Vector2(24, 174),
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Right = 48, Bottom = 184 },
                                    Child = rows = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 4),
                                    },
                                },
                                status = new OsuSpriteText
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Y = 240,
                                    Font = OsuFont.Torus.With(size: 20),
                                    Colour = ModernResultsScreen.SUB_CONTENT,
                                },
                            }
                        },
                    }
                }
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            scope.BindValueChanged(_ => fetch());
            leaderboard.Scores.BindValueChanged(s => Schedule(() => show(s.NewValue)));
            fetch();
        }

        private Drawable createModToggle(Mod mod)
        {
            var icon = new ModIcon(mod, showExtendedInformation: false) { Scale = new Vector2(0.5f) };
            modFilter.Add(icon);
            return new ModToggle(icon, fetch);
        }

        private void fetch()
        {
            var mods = modFilter.Where(m => m.Selected.Value).Select(m => ((Mod)m.Mod).DeepClone()).ToArray();
            leaderboard.FetchWithCriteria(new LeaderboardCriteria(score.BeatmapInfo, score.Ruleset, scope.Value, mods.Length > 0 ? mods : null));
        }

        private void show(LeaderboardScores? scores)
        {
            rows.Clear();
            pinned.Clear();
            pinned.Alpha = 0;

            if (scores == null)
            {
                status.Text = "Loading...";
                return;
            }

            if (scores.FailState != null)
            {
                status.Text = scores.FailState switch
                {
                    LeaderboardFailState.NotLoggedIn => "Sign in to see the leaderboard.",
                    LeaderboardFailState.NotSupporter => "This leaderboard isn't open to you.",
                    LeaderboardFailState.NoTeam => "You aren't in a team.",
                    LeaderboardFailState.BeatmapUnavailable => "This beatmap has no leaderboard.",
                    _ => "The leaderboard couldn't be loaded.",
                };
                return;
            }

            var top = scores.TopScores.ToList();
            status.Text = top.Count == 0 ? "No scores yet." : string.Empty;

            for (int i = 0; i < top.Count; i++)
                rows.Add(new ScoreRow(top[i], top[i].Position ?? i + 1, judgements));

            if (scores.UserScore is ScoreInfo own)
            {
                pinned.Child = new ScoreRow(own, own.Position, judgements, pinned: true);
                pinned.Alpha = 1;
            }
        }

        // ── parts ────────────────────────────────────────────────────────────────────────────

        /// <summary>A mod to filter by: picked and unpicked by clicking it.</summary>
        private partial class ModToggle : OsuClickableContainer
        {
            public ModToggle(ModIcon icon, Action changed)
            {
                AutoSizeAxes = Axes.Both;
                Child = icon;
                Action = () => icon.Selected.Toggle();
                icon.Alpha = 0.5f;
                icon.Selected.BindValueChanged(s =>
                {
                    icon.FadeTo(s.NewValue ? 1 : 0.5f, 100);
                    changed();
                });
            }
        }

        /// <summary>A scope's tab: its name, the design's green line under the picked one.</summary>
        private partial class ScopeTab : OsuClickableContainer
        {
            private readonly BeatmapLeaderboardScope value;
            private readonly Bindable<BeatmapLeaderboardScope> current = new Bindable<BeatmapLeaderboardScope>();
            private readonly OsuSpriteText text;
            private readonly Box line;

            public ScopeTab(LocalisableString name, BeatmapLeaderboardScope value, Bindable<BeatmapLeaderboardScope> current)
            {
                this.value = value;
                this.current.BindTo(current);

                AutoSizeAxes = Axes.X;
                Height = 40;
                Action = () => current.Value = value;

                Children = new Drawable[]
                {
                    new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 2,
                        Colour = Color4Extensions.FromHex("66ffcc").Opacity(0.6f),
                    },
                    line = new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 4,
                        Colour = Color4Extensions.FromHex("66ffcc"),
                    },
                    text = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Y = -4,
                        Margin = new MarginPadding { Horizontal = 15 },
                        Text = name,
                        Font = OsuFont.Torus.With(size: 18),
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                current.BindValueChanged(_ => updateState(), true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateState();
                base.OnHoverLost(e);
            }

            private void updateState()
            {
                bool picked = current.Value == value;
                text.Font = OsuFont.Torus.With(size: 18, weight: picked ? FontWeight.Bold : FontWeight.Regular);
                text.FadeColour(picked || IsHovered ? Color4.White : ModernResultsScreen.SUB_CONTENT, 100);
                line.FadeTo(picked ? 1 : 0, 100);
            }
        }

        /// <summary>A score in the table: its place, grade, score, accuracy, player, combo, judgements, pp, how long ago and mods.</summary>
        private partial class ScoreRow : CompositeDrawable
        {
            // Where each column starts, from the row's left (the design's).
            private const float score_x = 162, accuracy_x = 268, player_x = 387, combo_x = 749, pp_x = 1281, time_x = 1387, mods_x = 1534;
            private static readonly float[] judgement_x = { 864, 967, 1071, 1177 };

            public ScoreRow(ScoreInfo score, int? position, HitResult[] judgements, bool pinned = false)
            {
                RelativeSizeAxes = Axes.X;
                Height = pinned ? 80 : 48;

                float size = 24;
                var columns = new List<Drawable>
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreRight,
                        X = 97,
                        Text = position is int p ? $"#{p:N0}" : "-",
                        Font = OsuFont.Torus.With(size: size, weight: FontWeight.SemiBold),
                    },
                    new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 114,
                        Size = new Vector2(32, 16),
                        Child = new DrawableRank(score.Rank),
                    },
                    text(score_x, score.GetDisplayScore(ScoringMode.Standardised).ToString("N0", CultureInfo.CurrentCulture), size),
                    text(accuracy_x, score.Accuracy.FormatAccuracy().ToString(), size, Color4Extensions.FromHex("b2ff66")),
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = player_x,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(4, 0),
                        Children = new Drawable[]
                        {
                            new UpdateableAvatar(score.User, isInteractive: false)
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Size = new Vector2(40),
                                Masking = true,
                                CornerRadius = 20,
                            },
                            new UpdateableFlag(score.User.CountryCode)
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Size = new Vector2(36, 24),
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = score.User.Username,
                                Font = OsuFont.Torus.With(size: size, weight: FontWeight.SemiBold),
                            },
                        }
                    },
                    text(combo_x, $"{score.MaxCombo:N0}x", size),
                    text(pp_x, score.PP is double pp ? Math.Round(pp).ToString("N0", CultureInfo.CurrentCulture) : "-", size),
                    text(time_x, ago(score.Date), size),
                    new FillFlowContainer<ModSwitchTiny>
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = mods_x,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(2, 0),
                        ChildrenEnumerable = score.Mods.AsOrdered().Select(m => new ModSwitchTiny(m) { Active = { Value = true }, Scale = new Vector2(0.55f) }),
                    },
                };

                for (int i = 0; i < judgements.Length; i++)
                {
                    int count = score.Statistics.GetValueOrDefault(judgements[i]);
                    columns.Add(text(judgement_x[i], count.ToString("N0", CultureInfo.CurrentCulture), size, count == 0 ? ModernResultsScreen.SUB_CONTENT.Opacity(0.6f) : null));
                }

                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = pinned ? 16 : 6,
                        Child = new Box { RelativeSizeAxes = Axes.Both, Colour = pinned ? ModernResultsScreen.BACKGROUND_3 : ModernResultsScreen.BACKGROUND_4 },
                    },
                    new Container { RelativeSizeAxes = Axes.Both, Children = columns },
                };
            }

            private static OsuSpriteText text(float x, string value, float size, Color4? colour = null) => new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = x,
                Text = value,
                Font = OsuFont.Torus.With(size: size, weight: FontWeight.SemiBold),
                Colour = colour ?? Color4.White,
            };

            /// <summary>How long ago, as the design has it ("4y", "3mo", "12d", "5h", "20m", "now").</summary>
            private static string ago(DateTimeOffset date)
            {
                var span = DateTimeOffset.Now - date;

                if (span.TotalDays >= 365) return $"{(int)(span.TotalDays / 365)}y";
                if (span.TotalDays >= 30) return $"{(int)(span.TotalDays / 30)}mo";
                if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d";
                if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h";
                if (span.TotalMinutes >= 1) return $"{(int)span.TotalMinutes}m";

                return "now";
            }

            /// <summary>The table's column names; the judgements' as their coloured tags.</summary>
            public partial class Header : CompositeDrawable
            {
                [Resolved]
                private OsuColour colours { get; set; } = null!;

                private readonly HitResult[] judgements;
                private readonly int rulesetId;

                public Header(HitResult[] judgements, int rulesetId)
                {
                    this.judgements = judgements;
                    this.rulesetId = rulesetId;
                    RelativeSizeAxes = Axes.X;
                    Height = 24;
                    Padding = new MarginPadding { Right = 48 };
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    AddRangeInternal(new[]
                    {
                        label(9, "RANK"),
                        label(score_x, "SCORE"),
                        label(accuracy_x, "ACCURACY"),
                        label(player_x, "PLAYER"),
                        label(combo_x, "MAX COMBO"),
                        label(pp_x, "PP"),
                        label(time_x, "TIME"),
                        label(mods_x, "MODS"),
                    });

                    for (int i = 0; i < judgements.Length; i++)
                    {
                        AddInternal(new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = judgement_x[i],
                            AutoSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 3,
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.ForHitResult(judgements[i]) },
                                new OsuSpriteText
                                {
                                    Margin = new MarginPadding { Horizontal = 4 },
                                    Text = judgementName(judgements[i]),
                                    Font = OsuFont.Torus.With(size: 16, weight: FontWeight.Bold),
                                    Colour = ModernResultsScreen.BACKGROUND_5,
                                },
                            }
                        });
                    }
                }

                private LocalisableString judgementName(HitResult result)
                {
                    if (rulesetId == 0)
                    {
                        switch (result)
                        {
                            case HitResult.Great: return "300";
                            case HitResult.Ok: return "100";
                            case HitResult.Meh: return "50";
                            case HitResult.Miss: return "MISS";
                        }
                    }

                    return result.GetDescription().ToUpper();
                }

                private static Drawable label(float x, string text) => new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = x,
                    Text = text,
                    Font = OsuFont.Torus.With(size: 16, weight: FontWeight.Bold),
                };
            }
        }
    }
}

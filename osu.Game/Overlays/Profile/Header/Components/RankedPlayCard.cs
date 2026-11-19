// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.RankedPlay;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Components;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Profile.Header.Components
{
    /// <summary>
    /// Reprise: the profile's "Ranked Play" card, as the website's (detail-stats-v2/matchmaking.tsx): the player's best active pool's rank,
    /// rating and tier (its tier badge in the corner), the matches played, won and lost; the latest matches in the middle (or that there
    /// isn't enough to go on yet, while the rating is provisional); the rating's history at the bottom.
    /// </summary>
    public partial class RankedPlayCard : SectionedProfileCard
    {
        private static readonly RankingTier[] tiers = { RankingTier.Bronze, RankingTier.Silver, RankingTier.Gold, RankingTier.Platinum, RankingTier.Rhodium, RankingTier.Radiant };

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        private CardValueDisplay rank = null!;
        private CardValueDisplay rating = null!;
        private CardValueDisplay tier = null!;
        private CardValueDisplay plays = null!;
        private CardValueDisplay wins = null!;
        private CardValueDisplay losses = null!;

        public RankedPlayCard()
            : base(FontAwesome.Solid.Users, UsersStrings.ShowMatchmakingTitle)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            TopContent.Children = new Drawable[]
            {
                // profile-detail-stats-card__values--matchmaking-rating: 1fr 1fr (the rank alone on the first row).
                valueGrid(new[] { new Dimension(), new Dimension() }, new Drawable[][]
                {
                    new Drawable[] { rank = new CardValueDisplay(CardValueSize.Rank) { Label = UsersStrings.ShowMatchmakingRank }, Empty() },
                    new Drawable[]
                    {
                        rating = new CardValueDisplay(CardValueSize.RankSmall) { Label = UsersStrings.ShowMatchmakingRating },
                        tier = new CardValueDisplay(CardValueSize.RankSmall) { Label = UsersStrings.ShowMatchmakingTier },
                    },
                }),
                // profile-detail-stats-card__values--matchmaking-matches: 2fr 1fr 1fr.
                valueGrid(new[] { new Dimension(GridSizeMode.Relative, 0.5f), new Dimension(), new Dimension() }, new Drawable[][]
                {
                    new Drawable[]
                    {
                        plays = new CardValueDisplay(CardValueSize.Plain) { Label = UsersStrings.ShowMatchmakingPlays },
                        wins = new CardValueDisplay(CardValueSize.Plain) { Label = UsersStrings.ShowMatchmakingWins },
                        losses = new CardValueDisplay(CardValueSize.Plain) { Label = UsersStrings.ShowMatchmakingLosses },
                    }
                }),
            };
        }

        private static GridContainer valueGrid(Dimension[] columns, Drawable[][] content) => new GridContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            RowDimensions = content.Select(_ => new Dimension(GridSizeMode.AutoSize)).ToArray(),
            ColumnDimensions = columns,
            Content = content,
        };

        /// <summary>The website's tier for a rating ("Gold II", "Lustrous" from 2400).</summary>
        public static string TierFor(int rating) => DivisionFor(rating).DisplayName;

        /// <summary>The website's tier for a rating, as the game's ranked play divisions (whose badges the game has).</summary>
        public static APIRankedPlayDivision DivisionFor(int rating)
        {
            if (rating >= 2400)
                return new APIRankedPlayDivision { Tier = RankingTier.Lustrous };

            int tierBase = Math.Clamp(rating, 600, 2399) / 100 - 6;

            return new APIRankedPlayDivision
            {
                Tier = tiers[tierBase / 3],
                Division = (RankedPlayDivision)(tierBase % 3 + 1),
            };
        }

        /// <summary>The pool the website shows first: the player's best ranked in an active pool.</summary>
        public static APIUserMatchmakingStatistics? BestStats(APIUser? user) =>
            user?.MatchmakingStatistics.Where(s => s.Pool.Active && s.Rank > 0).OrderBy(s => s.Rank).FirstOrDefault()
            ?? user?.MatchmakingStatistics.FirstOrDefault(s => s.Pool.Active);

        protected override void UpdateDisplay(UserProfileData? data)
        {
            var stats = BestStats(data?.User);
            bool provisional = stats == null || stats.IsRatingProvisional;
            bool ranked = !provisional && stats!.Rank > 0;
            var division = ranked ? DivisionFor(stats!.Rating) : null;

            rank.Value.Text = ranked ? stats!.Rank!.Value.ToLocalisableString("\\##,##0") : "-";
            rank.Value.Colour = division != null ? OsuColour.ForRankingTier(division.Tier) : colourProvider.Content2;
            rank.Value.Font = rank.Value.Font.With(weight: division != null ? FontWeight.Bold : FontWeight.Regular);

            int ratingValue = stats?.Rating ?? 0;
            rating.Value.Text = provisional ? $"{ratingValue.ToLocalisableString("N0")}*" : ratingValue.ToLocalisableString("N0");
            rating.Value.TooltipText = provisional ? "Provisional rating" : default;
            rating.Value.Alpha = provisional ? 0.6f : 1;
            tier.Value.Text = division?.DisplayName ?? "-";

            int playCount = stats?.Plays ?? 0, firsts = stats?.FirstPlacements ?? 0;
            plays.Value.Text = playCount.ToLocalisableString("N0");
            wins.Value.Text = firsts.ToLocalisableString("N0");
            losses.Value.Text = (playCount - firsts).ToLocalisableString("N0");

            TopRightContent.Clear();

            if (division != null && rulesets.GetRuleset(stats!.Pool.RulesetId) is RulesetInfo ruleset)
            {
                // matchmaking-tier-badge: 80px wide.
                TopRightContent.Child = new DivisionBadge(division, ruleset, stats.Rank)
                {
                    Scale = new Vector2(80f / 120),
                };
            }

            MiddleContent.Child = provisional ? provisionalNote() : recentHistory(stats!);

            BottomContent.Clear();

            if (stats == null || stats.RecentHistory.Length == 0)
                BottomContent.Child = EmptyChartText(UsersStrings.ShowExtraUnranked);
            else if (provisional)
                BottomContent.Child = EmptyChartText("-", true);
            else
            {
                BottomContent.Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Right = 15 },
                    Child = new RatingGraph(stats.PoolId, stats.UserId, stats.RecentHistory) { RelativeSizeAxes = Axes.Both },
                };
            }
        }

        // matchmaking-result--provisional: a ghost, then the note.
        private Drawable provisionalNote() => new FillFlowContainer
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(10),
            Padding = new MarginPadding { Horizontal = 5 },
            Colour = colourProvider.Content2,
            Children = new Drawable[]
            {
                new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(16),
                    Icon = FontAwesome.Solid.Ghost,
                },
                new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 14))
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Width = 0.85f,
                    Text = UsersStrings.ShowMatchmakingRecentHistoryProvisional,
                },
            }
        };

        // matchmaking-result: "Latest Match History", then a pill a match (W, L or D), fading out to the right.
        private Drawable recentHistory(APIUserMatchmakingStatistics stats) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(5),
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = UsersStrings.ShowMatchmakingRecentHistory,
                    Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 16,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(5),
                            ChildrenEnumerable = stats.RecentHistory.Select(resultPill),
                        },
                        // ::after: the strip's colour (the card's, under black at 0.2) over the last 120px.
                        new Box
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            RelativeSizeAxes = Axes.Y,
                            Width = 120,
                            Colour = ColourInfo.GradientHorizontal(middleColour.Opacity(0), middleColour),
                        },
                    }
                },
            }
        };

        private Color4 middleColour
        {
            get
            {
                var bg = colourProvider.Background3;
                return new Color4(bg.R * 0.8f, bg.G * 0.8f, bg.B * 0.8f, 1);
            }
        }

        private Drawable resultPill(APIMatchmakingEloHistory entry)
        {
            (Color4 light, Color4 dark, Color4 text, string label) = entry.Result switch
            {
                "win" => (colours.Green1, colours.Green3, colours.Green4, "W"),
                "loss" => (colours.Red1, colours.Red3, colours.Red4, "L"),
                _ => (colours.Orange1, colours.Orange3, colours.Orange4, "D"),
            };

            return new ResultPill(entry.CreatedAt)
            {
                Size = new Vector2(24, 16),
                Masking = true,
                CornerRadius = 8,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(light, dark),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = label,
                        Colour = text,
                        Font = OsuFont.Default.With(size: 10, weight: FontWeight.Black),
                    },
                }
            };
        }

        private partial class ResultPill : Container, IHasTooltip
        {
            private readonly DateTimeOffset at;

            public ResultPill(DateTimeOffset at)
            {
                this.at = at;
            }

            public LocalisableString TooltipText => at.ToLocalTime().ToString("d MMM yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The rating's history (matchmaking-chart.tsx): its rating at the end of each day the player played, from the website's user chart.
        /// </summary>
        private partial class RatingGraph : UserGraph<int, int>
        {
            private readonly int poolId;
            private readonly int userId;

            private DateTimeOffset[] dates = Array.Empty<DateTimeOffset>();

            [Resolved]
            private IAPIProvider api { get; set; } = null!;

            private readonly APIMatchmakingEloHistory[] recentHistory;

            /// <param name="poolId">The pool.</param>
            /// <param name="userId">The player.</param>
            /// <param name="recentHistory">Their latest matches: the chart from these when the website's can't be had.</param>
            public RatingGraph(int poolId, int userId, APIMatchmakingEloHistory[] recentHistory)
            {
                this.poolId = poolId;
                this.userId = userId;
                this.recentHistory = recentHistory;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                var request = new OsuJsonWebRequest<UserChartResponse>($@"{api.Endpoints.WebsiteUrl}/matchmaking-pools/{poolId}/user-chart/{userId}");

                request.Finished += () => show(request.ResponseObject?.History is { Length: > 0 } history ? history : recentHistory);
                request.Failed += _ => show(recentHistory);
                // Its failure is reported through Failed; observed here so it isn't left as an unobserved exception.
                request.PerformAsync().ContinueWith(t => _ = t.Exception, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
            }

            private void show(APIMatchmakingEloHistory[] history)
            {
                // Newest first: each day's latest rating, oldest day first (a single day gets the day before too, as the website's).
                var byDay = history.GroupBy(h => h.CreatedAt.UtcDateTime.Date)
                                   .Select(g => (day: g.Key, rating: g.OrderByDescending(h => h.Id).First().EloAfter))
                                   .OrderBy(d => d.day)
                                   .ToList();

                if (byDay.Count == 1)
                    byDay.Insert(0, (byDay[0].day.AddDays(-1), byDay[0].rating));

                Schedule(() =>
                {
                    dates = byDay.Select(d => new DateTimeOffset(d.day, TimeSpan.Zero)).ToArray();
                    Data = byDay.Select((d, i) => new KeyValuePair<int, int>(i, d.rating)).ToArray();
                });
            }

            protected override float GetDataPointHeight(int rating) => MathF.Log(Math.Max(1, rating));

            protected override UserGraphTooltipContent GetTooltipContent(int index, int rating) => new UserGraphTooltipContent
            {
                Name = UsersStrings.ShowMatchmakingRating,
                Count = rating.ToLocalisableString("N0"),
                Time = index < dates.Length ? dates[index].ToString("yyyy/M/d", System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
            };

            private class UserChartResponse
            {
                [Newtonsoft.Json.JsonProperty("user_elo_history")]
                public APIMatchmakingEloHistory[] History { get; set; } = Array.Empty<APIMatchmakingEloHistory>();
            }
        }
    }
}

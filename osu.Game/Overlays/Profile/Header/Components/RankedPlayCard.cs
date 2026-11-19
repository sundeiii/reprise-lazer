// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osuTK;

namespace osu.Game.Overlays.Profile.Header.Components
{
    /// <summary>
    /// Reprise: the profile's "Ranked Play" card, as the website's: the player's best ranked play pool (its rank, rating and tier, the
    /// matches played, won and lost), or that there isn't enough to go on yet while the rating is provisional.
    /// </summary>
    public partial class RankedPlayCard : ProfileStatsCard
    {
        private static readonly string[] tiers = { "Bronze", "Silver", "Gold", "Platinum", "Rhodium", "Radiant" };
        private static readonly string[] tier_levels = { "I", "II", "III" };

        private ProfileValueDisplay rank = null!;
        private ProfileValueDisplay rating = null!;
        private ProfileValueDisplay tier = null!;
        private ProfileValueDisplay matches = null!;
        private ProfileValueDisplay wins = null!;
        private ProfileValueDisplay losses = null!;
        private OsuTextFlowContainer provisionalText = null!;

        public RankedPlayCard()
            : base(FontAwesome.Solid.Users, "Ranked Play")
        {
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            Body.Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(20, 0),
                    Children = new Drawable[]
                    {
                        rank = new ProfileValueDisplay(true) { Title = "Rank" },
                        rating = new ProfileValueDisplay { Title = "Rating" },
                        tier = new ProfileValueDisplay { Title = "Tier" },
                    }
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(20, 0),
                    Children = new Drawable[]
                    {
                        matches = new ProfileValueDisplay { Title = "Total Matches" },
                        wins = new ProfileValueDisplay { Title = "Wins" },
                        losses = new ProfileValueDisplay { Title = "Losses" },
                    }
                },
                provisionalText = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 12))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Colour = colourProvider.Content2,
                    Text = "Not enough data to display accurate stats. Play more games!",
                },
            };
        }

        /// <summary>The website's tier for a rating ("Gold II", "Lustrous" from 2400).</summary>
        public static string TierFor(int rating)
        {
            if (rating >= 2400)
                return "Lustrous";

            int tierBase = Math.Clamp(rating, 600, 2399) / 100 - 6;
            return $"{tiers[tierBase / 3]} {tier_levels[tierBase % 3]}";
        }

        /// <summary>The pool the website shows first: the player's best ranked in an active pool.</summary>
        public static APIUserMatchmakingStatistics? BestStats(APIUser? user) =>
            user?.MatchmakingStatistics.Where(s => s.Pool.Active && s.Rank != null).OrderBy(s => s.Rank).FirstOrDefault()
            ?? user?.MatchmakingStatistics.FirstOrDefault(s => s.Pool.Active);

        protected override void UpdateDisplay(UserProfileData? data)
        {
            var stats = BestStats(data?.User);
            bool provisional = stats == null || stats.IsRatingProvisional;
            bool ranked = !provisional && stats!.Rank is int r && r > 0;

            rank.Content.Text = ranked ? stats!.Rank!.Value.ToLocalisableString("\\##,##0") : "-";
            rating.Content.Text = stats == null ? "0*" : provisional ? $"{stats.Rating.ToLocalisableString("N0")}*" : stats.Rating.ToLocalisableString("N0");
            tier.Content.Text = ranked ? TierFor(stats!.Rating) : "-";

            int plays = stats?.Plays ?? 0, firsts = stats?.FirstPlacements ?? 0;
            matches.Content.Text = plays.ToLocalisableString("N0");
            wins.Content.Text = firsts.ToLocalisableString("N0");
            losses.Content.Text = (plays - firsts).ToLocalisableString("N0");

            provisionalText.Alpha = provisional ? 1 : 0;
        }
    }
}

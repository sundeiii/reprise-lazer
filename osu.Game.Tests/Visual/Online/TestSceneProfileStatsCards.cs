// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Header;
using osu.Game.Overlays.Profile.Header.Components;
using osu.Game.Rulesets.Osu;
using osu.Game.Users;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>
    /// Reprise: the profile's three statistics cards, as the website has them (Solo Play, Ranked Play, the pinned badge with the statistics),
    /// for a ranked player with a badge pinned and a ranked play history, and for a new one.
    /// </summary>
    public partial class TestSceneProfileStatsCards : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);

        private DetailHeaderContainer cards = null!;

        [SetUp]
        public void SetUp() => Schedule(() => Child = cards = new DetailHeaderContainer
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        });

        [Test]
        public void TestRankedPlayer()
        {
            AddStep("show a ranked player", () => cards.User.Value = new UserProfileData(rankedPlayer(), new OsuRuleset().RulesetInfo));
            AddUntilStep("cards as tall as each other", () => cards.ChildrenOfType<ProfileStatsCard>().Select(c => c.DrawHeight).Distinct().Count() == 1);
            AddUntilStep("pinned badge shown, by its name", () => cards.ChildrenOfType<DrawableBadge>().Count() == 1
                                                               && cards.ChildrenOfType<TruncatingSpriteText>().Any(t => t.Text.ToString() == "Community Contributor"));
        }

        [Test]
        public void TestNewPlayer()
        {
            AddStep("show a new player", () => cards.User.Value = new UserProfileData(newPlayer(), new OsuRuleset().RulesetInfo));
            AddUntilStep("no pinned badge", () => !cards.ChildrenOfType<DrawableBadge>().Any());
        }

        private static APIUser rankedPlayer() => new APIUser
        {
            Id = 4,
            Username = "Lilily",
            Statistics = new UserStatistics
            {
                IsRanked = true,
                GlobalRank = 48,
                GlobalRankPercent = 0.0004f,
                CountryRank = 3,
                PP = 6123.4m,
                RankedScore = 9_876_543_210,
                Accuracy = 98.76,
                PlayCount = 12345,
                PlayTime = 2_173_671,
                TotalScore = 98_765_432_101,
                TotalHits = 7_654_321,
                MaxCombo = 2727,
                ReplaysWatched = 321,
                GradesCount = new UserStatistics.Grades { SSPlus = 12, SS = 120, SPlus = 345, S = 1234, A = 2345 },
                Level = new UserStatistics.LevelInfo { Current = 101, Progress = 40 },
                RankHistory = new APIRankHistory
                {
                    Mode = @"osu",
                    Data = Enumerable.Range(0, 90).Select(i => 120 - i * 72 / 90).ToArray(),
                },
            },
            RankHighest = new APIUser.UserRankHighest { Rank = 41, UpdatedAt = DateTimeOffset.Now.AddMonths(-2) },
            Achievements = Enumerable.Range(0, 87).Select(i => new APIUserAchievement { ID = i, AchievedAt = DateTimeOffset.Now }).ToArray(),
            Badges = new[]
            {
                new Badge
                {
                    Image = "contributor.png",
                    Description = "Outstanding help by being a voluntary test subject.",
                    ImageUrl = "https://assets.ppy.sh/profile-badges/contributor@2x.png",
                    ImageUrlLowRes = "https://assets.ppy.sh/profile-badges/contributor.png",
                    AwardedAt = DateTimeOffset.Now.AddYears(-3),
                },
                new Badge
                {
                    Image = "contributor-new.png",
                    Name = "Community Contributor",
                    Description = "Outstanding contribution to the community.",
                    ImageUrl = "https://assets.ppy.sh/profile-badges/contributor-new@2x.png",
                    ImageUrlLowRes = "https://assets.ppy.sh/profile-badges/contributor-new.png",
                    AwardedAt = new DateTimeOffset(2023, 8, 1, 0, 0, 0, TimeSpan.Zero),
                },
            },
            ProfilePinnedBadge = "contributor-new.png",
            DailyChallengeStatistics = new APIUserDailyChallengeStatistics
            {
                UserID = 4,
                PlayCount = 213,
                DailyStreakCurrent = 21,
                DailyStreakBest = 70,
                WeeklyStreakCurrent = 3,
                WeeklyStreakBest = 11,
                LastUpdate = DateTimeOffset.UtcNow,
            },
            MatchmakingStatistics =
            [
                new APIUserMatchmakingStatistics
                {
                    UserId = 4,
                    PoolId = 1,
                    Pool = new APIMatchmakingPool { Id = 1, Active = true, Name = "osu!", RulesetId = 0 },
                    Rating = 1634,
                    Rank = 27,
                    Plays = 64,
                    FirstPlacements = 39,
                    RecentHistory = Enumerable.Range(0, 10).Select(i => new APIMatchmakingEloHistory
                    {
                        Id = 100 - i,
                        RoomId = 500 - i,
                        Result = i % 3 == 1 ? "loss" : i == 4 ? "draw" : "win",
                        EloAfter = 1634 - i * 9,
                        CreatedAt = DateTimeOffset.UtcNow.AddHours(-i * 7),
                    }).ToArray(),
                }
            ],
        };

        private static APIUser newPlayer() => new APIUser
        {
            Id = 1001,
            Username = "newcomer",
            Statistics = new UserStatistics
            {
                IsRanked = false,
                PlayCount = 3,
                Accuracy = 91.2,
            },
            Achievements = Array.Empty<APIUserAchievement>(),
            Badges = Array.Empty<Badge>(),
            MatchmakingStatistics =
            [
                new APIUserMatchmakingStatistics
                {
                    UserId = 1001,
                    PoolId = 1,
                    Pool = new APIMatchmakingPool { Id = 1, Active = true, Name = "osu!", RulesetId = 0 },
                    Rating = 1500,
                    Rank = -1,
                    Plays = 2,
                    FirstPlacements = 1,
                    IsRatingProvisional = true,
                    RecentHistory = new[]
                    {
                        new APIMatchmakingEloHistory { Id = 2, RoomId = 2, Result = "win", EloAfter = 1520, CreatedAt = DateTimeOffset.UtcNow },
                    },
                }
            ],
        };
    }
}

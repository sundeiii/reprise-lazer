// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Header.Components;
using osu.Game.Rulesets;
using osu.Game.Users;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>Reprise: the profile cards' rules, as the website's.</summary>
    [TestFixture]
    public class ProfileCardsTest
    {
        [TestCase(0, "Bronze I")]
        [TestCase(699, "Bronze I")]
        [TestCase(700, "Bronze II")]
        [TestCase(900, "Silver I")]
        [TestCase(1550, "Platinum I")]
        [TestCase(2399, "Radiant III")]
        [TestCase(2400, "Lustrous")]
        public void TestTier(int rating, string tier) => Assert.That(RankedPlayCard.TierFor(rating), Is.EqualTo(tier));

        [Test]
        public void TestPinnedBadge()
        {
            // As the API sends them: profile_pinned_badge is the badge's image filename.
            var fad = new Badge { Image = "fad-2023.png", Description = "French Aim Duo 2023", ImageUrl = "https://x/fad-2023@2x.png", ImageUrlLowRes = "https://x/fad-2023.png", AwardedAt = DateTimeOffset.Now };
            var user = new APIUser { Badges = new[] { new Badge { Image = "noms200.png", Description = "200 nominations", ImageUrl = "https://x/noms200@2x.png" }, fad } };

            Assert.That(BadgeStatsCard.PinnedBadge(new UserProfileData(user, new RulesetInfo())), Is.Null, "nothing pinned");

            user.ProfilePinnedBadge = "fad-2023.png";
            Assert.That(BadgeStatsCard.PinnedBadge(new UserProfileData(user, new RulesetInfo())), Is.SameAs(fad), "the one pinned, not the first");

            user.ProfilePinnedBadge = "gone.png";
            Assert.That(BadgeStatsCard.PinnedBadge(new UserProfileData(user, new RulesetInfo())), Is.Null, "a badge no longer held");
        }

        [Test]
        public void TestBadgeFromApi()
        {
            var badge = Newtonsoft.Json.JsonConvert.DeserializeObject<Badge>(
                "{\"awarded_at\":\"2023-08-01T00:00:00+00:00\",\"description\":\"French Aim Duo\",\"image\":\"fad-2023.png\",\"image@2x_url\":\"https://x/fad-2023@2x.png\",\"image_url\":\"https://x/fad-2023.png\",\"name\":null,\"url\":\"\"}")!;

            Assert.That(badge.Image, Is.EqualTo("fad-2023.png"));
            Assert.That(badge.Name, Is.Null);
        }

        [Test]
        public void TestBestRankedPool()
        {
            var inactive = new APIUserMatchmakingStatistics { PoolId = 1, Rank = 1, Pool = new APIMatchmakingPool { Active = false } };
            var unranked = new APIUserMatchmakingStatistics { PoolId = 2, Rank = -1, Pool = new APIMatchmakingPool { Active = true } };
            var ranked = new APIUserMatchmakingStatistics { PoolId = 3, Rank = 40, Pool = new APIMatchmakingPool { Active = true } };
            var user = new APIUser { MatchmakingStatistics = new[] { inactive, unranked, ranked } };

            Assert.That(RankedPlayCard.BestStats(user), Is.SameAs(ranked));

            user.MatchmakingStatistics = new[] { inactive, unranked };
            Assert.That(RankedPlayCard.BestStats(user), Is.SameAs(unranked), "an active pool, unranked still");
        }

        [TestCase("osu", "osu", ProfileVariantSelector.Variant.Vanilla)]
        [TestCase("osurx", "osu", ProfileVariantSelector.Variant.Relax)]
        [TestCase("osuap", "osu", ProfileVariantSelector.Variant.Autopilot)]
        [TestCase("fruitsrx", "fruits", ProfileVariantSelector.Variant.Relax)]
        [TestCase("mania", "mania", ProfileVariantSelector.Variant.Vanilla)]
        public void TestVariantOfShownRuleset(string shortName, string baseName, ProfileVariantSelector.Variant variant) =>
            Assert.That(ProfileVariantSelector.Split(shortName), Is.EqualTo((baseName, variant)));
    }
}

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
            var fad = new Badge { Description = "French Aim Duo 2023", ImageUrl = "https://x/fad@2x.png", ImageUrlLowRes = "https://x/fad.png", AwardedAt = DateTimeOffset.Now };
            var user = new APIUser { Badges = new[] { new Badge { Description = "200 nominations", ImageUrl = "https://x/noms@2x.png" }, fad } };

            Assert.That(BadgeStatsCard.PinnedBadge(new UserProfileData(user, new RulesetInfo())), Is.Null, "nothing pinned");

            user.ProfilePinnedBadge = "https://x/fad.png";
            Assert.That(BadgeStatsCard.PinnedBadge(new UserProfileData(user, new RulesetInfo())), Is.SameAs(fad), "the one pinned, not the first");
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

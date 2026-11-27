// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>
    /// Reprise: the approach rate override changes only the map being played; stars and pp are worked out from the map's
    /// own approach rate, as the server works the play's pp out.
    /// </summary>
    [TestFixture]
    public class ApproachRateOverrideTest
    {
        [TearDown]
        public void TearDown() => GameplayOverrides.ApproachRateEnabled = false;

        [Test]
        public void TheOverride_AppliesToTheMapBeingPlayedOnly()
        {
            var ruleset = new OsuRuleset();
            var working = new TestWorkingBeatmap(new TestBeatmap(ruleset.RulesetInfo));
            float own = working.GetPlayableBeatmap(ruleset.RulesetInfo, Array.Empty<Mod>()).Difficulty.ApproachRate;

            GameplayOverrides.ApproachRateEnabled = true;
            GameplayOverrides.ApproachRate = own < 10 ? own + 1 : own - 1;

            Assert.That(working.GetPlayableBeatmap(ruleset.RulesetInfo, Array.Empty<Mod>()).Difficulty.ApproachRate, Is.EqualTo(own));

            using (GameplayOverrides.BuildingForGameplay())
                Assert.That(working.GetPlayableBeatmap(ruleset.RulesetInfo, Array.Empty<Mod>()).Difficulty.ApproachRate, Is.EqualTo((float)GameplayOverrides.ApproachRate));

            Assert.That(working.GetPlayableBeatmap(ruleset.RulesetInfo, Array.Empty<Mod>()).Difficulty.ApproachRate, Is.EqualTo(own));
        }

        [Test]
        public void StarRating_IsTheMapsOwnWithTheOverrideOn()
        {
            var ruleset = new OsuRuleset();
            var working = new TestWorkingBeatmap(new TestBeatmap(ruleset.RulesetInfo));
            double own = ruleset.CreateDifficultyCalculator(working).Calculate().StarRating;

            GameplayOverrides.ApproachRateEnabled = true;
            GameplayOverrides.ApproachRate = 11;

            Assert.That(ruleset.CreateDifficultyCalculator(working).Calculate().StarRating, Is.EqualTo(own));
        }
    }
}

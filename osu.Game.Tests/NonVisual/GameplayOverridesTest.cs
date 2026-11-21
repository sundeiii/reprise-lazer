// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class GameplayOverridesTest
    {
        [TearDown]
        public void TearDown()
        {
            GameplayOverrides.TimewarpEnabled = false;
            GameplayOverrides.TimewarpRate.Value = 1;
        }

        private static void enableTimewarp(double rate)
        {
            GameplayOverrides.TimewarpEnabled = true;
            GameplayOverrides.TimewarpRate.Value = rate;
        }

        [Test]
        public void TestWithoutTimewarpTheModsDecideTheRate()
        {
            Assert.That(GameplayOverrides.GetEffectiveRate(new Mod[] { new OsuModDoubleTime() }), Is.EqualTo(1.5));
            Assert.That(GameplayOverrides.GetEffectiveRate(new Mod[] { new OsuModHalfTime() }), Is.EqualTo(0.75));
            Assert.That(GameplayOverrides.GetEffectiveRate(new Mod[0]), Is.EqualTo(1));
        }

        [Test]
        public void TestTimewarpSetsTheRateWithoutMods()
        {
            enableTimewarp(1.25);

            Assert.That(GameplayOverrides.GetEffectiveRate(new Mod[0]), Is.EqualTo(1.25));
            Assert.That(GameplayOverrides.GetEffectiveRate(new Mod[] { new OsuModHardRock() }), Is.EqualTo(1.25));
        }

        [Test]
        public void TestTimewarpReplacesDoubleTimeAndHalfTimeInsteadOfStackingWithThem()
        {
            // gameplay ignores these mods' speed while Timewarp is on, so the display must too.
            enableTimewarp(1.25);

            Assert.That(GameplayOverrides.GetEffectiveRate(new Mod[] { new OsuModDoubleTime() }), Is.EqualTo(1.25));
            Assert.That(GameplayOverrides.GetEffectiveRate(new Mod[] { new OsuModHalfTime() }), Is.EqualTo(1.25));
        }

        [Test]
        public void TestTimewarpRateChangesAreReflectedImmediately()
        {
            enableTimewarp(1.5);
            Assert.That(GameplayOverrides.GetEffectiveRate(new Mod[0]), Is.EqualTo(1.5));

            GameplayOverrides.TimewarpRate.Value = 2;
            Assert.That(GameplayOverrides.GetEffectiveRate(new Mod[0]), Is.EqualTo(2));

            GameplayOverrides.TimewarpEnabled = false;
            Assert.That(GameplayOverrides.GetEffectiveRate(new Mod[0]), Is.EqualTo(1));
        }

        [Test]
        public void TestTogglingTimewarpNotifiesListeners()
        {
            int notifications = 0;
            GameplayOverrides.TimewarpEnabledBindable.ValueChanged += _ => notifications++;

            GameplayOverrides.TimewarpEnabled = true;
            GameplayOverrides.TimewarpEnabled = false;

            Assert.That(notifications, Is.EqualTo(2));
        }
    }
}

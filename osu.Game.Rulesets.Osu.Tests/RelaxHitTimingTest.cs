// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class RelaxHitTimingTest
    {
        private static OsuHitObject circle(double startTime) => new HitCircle { StartTime = startTime };

        [Test]
        public void TestClicksAtStartTimeByDefault()
        {
            var timing = new RelaxHitTiming(seed: 1);

            Assert.That(timing.GetHitTime(circle(1000), offset: 0, jitter: 0), Is.EqualTo(1000));
        }

        [TestCase(-5, 995)]
        [TestCase(7.5, 1007.5)]
        public void TestOffsetShiftsTheClick(double offset, double expected)
        {
            var timing = new RelaxHitTiming(seed: 1);

            Assert.That(timing.GetHitTime(circle(1000), offset, jitter: 0), Is.EqualTo(expected).Within(0.0001));
        }

        [Test]
        public void TestOffsetIsLimitedToWhereObjectsAreLookedAt()
        {
            var timing = new RelaxHitTiming(seed: 1);

            // Relax only considers an object from RELAX_LENIENCY before its start time, so clicking any earlier would never happen.
            Assert.That(timing.GetHitTime(circle(1000), offset: -500, jitter: 0), Is.EqualTo(1000 - OsuModRelax.RELAX_LENIENCY));
            Assert.That(timing.GetHitTime(circle(1000), offset: 500, jitter: 0), Is.EqualTo(1000 + RelaxHitTiming.MAX_LATE));
        }

        [Test]
        public void TestJitterStaysWithinRangeAndActuallyVaries()
        {
            var timing = new RelaxHitTiming(seed: 1234);

            double[] offsets = Enumerable.Range(0, 500).Select(i => timing.GetHitTime(circle(i * 100), offset: 2, jitter: 4) - i * 100).ToArray();

            Assert.That(offsets, Has.All.InRange(2 - 4, 2 + 4));
            Assert.That(offsets.Distinct().Count(), Is.GreaterThan(400));

            // the range is used, not just the middle of it.
            Assert.That(offsets.Min(), Is.LessThan(-1));
            Assert.That(offsets.Max(), Is.GreaterThan(5));
        }

        [Test]
        public void TestSameObjectAlwaysGetsTheSameTime()
        {
            var timing = new RelaxHitTiming(seed: 99);
            var hitObject = circle(1000);

            double first = timing.GetHitTime(hitObject, offset: 0, jitter: 8);

            for (int i = 0; i < 20; i++)
                Assert.That(timing.GetHitTime(hitObject, offset: 0, jitter: 8), Is.EqualTo(first));
        }

        [Test]
        public void TestForgettingAnObjectRollsAgain()
        {
            var timing = new RelaxHitTiming(seed: 99);
            var hitObject = circle(1000);

            double first = timing.GetHitTime(hitObject, offset: 0, jitter: 8);
            timing.Forget(hitObject);

            Assert.That(timing.GetHitTime(hitObject, offset: 0, jitter: 8), Is.Not.EqualTo(first));
        }

        [Test]
        public void TestJitterCannotPushTheClickBeforeItCanHappen()
        {
            var timing = new RelaxHitTiming(seed: 5);

            double[] offsets = Enumerable.Range(0, 300).Select(i => timing.GetHitTime(circle(i * 100), offset: -OsuModRelax.RELAX_LENIENCY, jitter: 10) - i * 100).ToArray();

            Assert.That(offsets, Has.All.GreaterThanOrEqualTo(-OsuModRelax.RELAX_LENIENCY));
        }

        [Test]
        public void TestChangingSettingsAffectsObjectsAlreadySeen()
        {
            var timing = new RelaxHitTiming(seed: 7);
            var hitObject = circle(1000);

            // the random part is remembered, but the configured values are read every time, so changing a setting takes effect immediately.
            double jittered = timing.GetHitTime(hitObject, offset: 0, jitter: 6);
            double withoutJitter = timing.GetHitTime(hitObject, offset: 0, jitter: 0);
            double shifted = timing.GetHitTime(hitObject, offset: 3, jitter: 6);

            Assert.That(withoutJitter, Is.EqualTo(1000));
            Assert.That(shifted - jittered, Is.EqualTo(3).Within(0.0001));
        }
    }
}

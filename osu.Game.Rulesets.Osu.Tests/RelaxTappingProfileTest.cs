// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>Reprise: the bundled Relax tapping profiles load, with their measures for every gap.</summary>
    [TestFixture]
    public class RelaxTappingProfileTest
    {
        [Test]
        public void TestBundledProfilesLoad()
        {
            Assert.That(RelaxTappingProfile.Available, Is.SupersetOf(new[] { "mrekk", "Ivaxa", "Akolibed", "NINERIK", "milosz", "lifeline", "xootynator" }));

            foreach (string name in RelaxTappingProfile.Available)
            {
                var profile = RelaxTappingProfile.Load(name);
                Assert.That(profile, Is.Not.Null, name);
                Assert.That(profile!.Buckets.Last().MaxGap, Is.Null, name);
                Assert.That(profile.For(60).Hold, Is.Not.Null, name);
                Assert.That(profile.For(5000).Offset, Is.Not.Null, name);
            }

            Assert.That(RelaxTappingProfile.Load(string.Empty), Is.Null);
            Assert.That(RelaxTappingProfile.Load("nobody"), Is.Null);
        }

        [Test]
        public void TestSampleStaysWithinTheDeciles()
        {
            var random = new Random(1);
            double[] deciles = { -10, -5, -2, -1, 0, 0, 1, 2, 4, 8, 20 };

            for (int i = 0; i < 1000; i++)
                Assert.That(RelaxTappingProfile.Sample(deciles, random, 99), Is.InRange(-10, 20));

            Assert.That(RelaxTappingProfile.Sample(null, random, 99), Is.EqualTo(99));
        }
    }
}

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
            Assert.That(RelaxTappingProfile.Available, Is.SupersetOf(new[]
            {
                "mrekk", "Ivaxa", "Akolibed", "NINERIK", "milosz", "lifeline", "xootynator",
                "FlyingTuna", "BTMC", "chocomint", "WhiteCat", "Zylice", "ASecretBox", "Abyssal", "cedru",
                "Vaxei", "Rafis", "RyuK", "Bubbleman", "rektygon", "decaten", "luciano", "Lilily",
            }));

            foreach (string name in RelaxTappingProfile.Available)
            {
                var profile = RelaxTappingProfile.Load(name);
                Assert.That(profile, Is.Not.Null, name);
                Assert.That(profile!.Buckets.Last().MaxGap, Is.Null, name);
                Assert.That(profile.For(60).Hold, Is.Not.Null, name);
                Assert.That(profile.For(5000).Offset, Is.Not.Null, name);

                // Which key comes next, by the key before: what keeps a player's favoured key (e.g. lifeline single-taps with K2).
                Assert.That(profile.For(60).SwitchFromLeft, Is.Not.Null, name);
                Assert.That(profile.For(60).SwitchFromRight, Is.Not.Null, name);
            }

            Assert.That(RelaxTappingProfile.Load(string.Empty), Is.Null);
            Assert.That(RelaxTappingProfile.Load("nobody"), Is.Null);
        }

        // Their timing drawn as the game draws it (each gap as often as in their plays) spreads as theirs did: the profiles once
        // kept only 11 points, the worst hit at each end, and came out twice as spread (lots of 100s).
        [Test]
        public void TestDrawnTimingSpreadsAsTheirs()
        {
            var random = new Random(1);

            foreach (string name in RelaxTappingProfile.Available)
            {
                var profile = RelaxTappingProfile.Load(name)!;
                var offsets = profile.Buckets.SelectMany(b => Enumerable.Range(0, Math.Max(1, b.Notes / 10)).Select(_ => RelaxTappingProfile.Sample(b.Offset, random, 0))).ToArray();
                double mean = offsets.Average();
                double ur = 10 * Math.Sqrt(offsets.Average(o => (o - mean) * (o - mean)));

                Assert.That(ur, Is.EqualTo(profile.Summary!.UnstableRate).Within(25).Percent, name);
            }
        }

        // How each plays, in the settings' words, as their replays (and osuconfigs.com's) have it.
        [TestCase("FlyingTuna", "full alternate")]
        [TestCase("Zylice", "full alternate")]
        [TestCase("mrekk", "alternates")]
        [TestCase("lifeline", "single-tapper")]
        [TestCase("chocomint", "single-tapper")]
        [TestCase("NINERIK", "single-taps jumps")]
        [TestCase("xootynator", "mixed")]
        public void TestStyle(string name, string style)
        {
            var profile = RelaxTappingProfile.Load(name)!;
            Assert.That(profile.Style, Is.EqualTo(style));
            Assert.That(profile.UserId, Is.GreaterThan(0));
            Assert.That(profile.Country, Has.Length.EqualTo(2));
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

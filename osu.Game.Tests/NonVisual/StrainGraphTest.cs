// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Taiko;
using osu.Game.Screens.Play.HUD.Counters;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>Reprise: BTMC's difficulty graph (tosu's strain graph, smoothed as the overlay does) and Chart.js's scale top.</summary>
    [TestFixture]
    public class StrainGraphTest
    {
        [TestCase(286.56, 300)]
        [TestCase(160, 200)]
        [TestCase(45, 60)]
        [TestCase(1234, 1500)]
        [TestCase(0.9, 1)]
        [TestCase(300, 300)]
        public void TestNiceMaximum(double highest, double expected)
            // As Chart.js scaled the overlay's graph for these (read from it in a browser).
            => Assert.That(StrainChart.NiceMaximum(highest), Is.EqualTo(expected).Within(1e-9));

        [Test]
        public void TestGraphCoversTheSong()
        {
            var ruleset = new OsuRuleset();
            var working = new TestWorkingBeatmap(new TestBeatmap(ruleset.RulesetInfo));
            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo);
            double last = playable.HitObjects.Max(h => h.StartTime);
            double songLength = last + 10_000;

            double[] graph = StrainGraph.Calculate(ruleset, working, playable, Array.Empty<Mod>(), songLength);

            // A value for each 400ms of the song (give or take the sections tosu rounds), zero before the first note.
            Assert.That(graph.Length, Is.EqualTo(songLength / 400).Within(3));
            Assert.That(graph.Max(), Is.GreaterThan(0));
            Assert.That(graph.Take((int)(playable.HitObjects[0].StartTime / 400) - 2), Is.All.EqualTo(0));

            // Double Time: the song at its rate, so fewer sections.
            double[] doubleTime = StrainGraph.Calculate(ruleset, working, playable, new Mod[] { new OsuModDoubleTime() }, songLength);
            Assert.That(doubleTime.Length, Is.EqualTo(songLength / 1.5 / 400).Within(3));
        }

        [Test]
        public void TestNoGraphWithoutAimAndSpeed()
        {
            // BTMC only graphs osu!'s aim and speed: other rulesets' skills are named otherwise.
            var ruleset = new TaikoRuleset();
            var working = new TestWorkingBeatmap(new TestBeatmap(ruleset.RulesetInfo));
            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo);

            Assert.That(StrainGraph.Calculate(ruleset, working, playable, Array.Empty<Mod>(), 60_000), Is.Empty);
        }

        [Test]
        public void TestSmoothing()
        {
            // fast-smooth.js's triangular smooth, as width rounds to 1 for few samples: unchanged, negatives clamped.
            Assert.That(StrainGraph.Smooth(new[] { -5.0, 10, 20 }), Is.EqualTo(new[] { 0.0, 10, 20 }));

            // Wider for a long map (every sample over a hundredth of the highest counts): a lone peak spreads.
            double[] data = new double[400];
            for (int i = 0; i < data.Length; i++)
                data[i] = 50;
            data[200] = 100;
            double[] smoothed = StrainGraph.Smooth(data);
            Assert.That(smoothed[200], Is.LessThan(100));
            Assert.That(smoothed[199], Is.GreaterThan(50));
        }
    }
}

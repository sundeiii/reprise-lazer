// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;
using ManagedBass;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.SongSwap;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>Reprise: swapping a map's song: which songs fit, how they're lined up, and the song made.</summary>
    [TestFixture]
    public class SongSwapTest
    {
        [TestCase(180, 180, 1, 1)]
        [TestCase(180, 90, 2, 1)]
        [TestCase(180, 360, 0.5, 1)]
        [TestCase(180, 200, 1, 0.9)]
        [TestCase(180, 100, 2, 0.9)]
        public void TestFits(double map, double song, double factor, double stretch)
        {
            var fit = SongSwapping.Fit(map, song);

            Assert.That(fit, Is.Not.Null);
            Assert.That(fit!.Value.Factor, Is.EqualTo(factor));
            Assert.That(fit.Value.Stretch, Is.EqualTo(stretch).Within(0.0001));
        }

        [TestCase(180, 130)]
        [TestCase(180, 0)]
        public void TestDoesntFit(double map, double song) => Assert.That(SongSwapping.Fit(map, song), Is.Null);

        [TestCase(60000, 1, 60000, true)]
        [TestCase(59999, 1, 60000, false)]
        [TestCase(70000, 1.25, 60000, false)]
        [TestCase(50000, 0.8, 60000, true)]
        public void TestLongEnough(double songLength, double stretch, double mapLength, bool expected) =>
            Assert.That(SongSwapping.LongEnough(songLength, stretch, mapLength), Is.EqualTo(expected));

        private static Beatmap map(double timingAt, double beatLength, params double[] objects)
        {
            var beatmap = new Beatmap();
            beatmap.ControlPointInfo.Add(timingAt, new TimingControlPoint { BeatLength = beatLength });

            foreach (double t in objects)
                beatmap.HitObjects.Add(new HitCircle { StartTime = t });

            return beatmap;
        }

        [Test]
        public void TestLinedUpOnTheBeat()
        {
            // Both 180 BPM: the song's first note goes on the map's (+740), then 10 later so its beats land on the map's.
            var mapBeatmap = map(1000, 60000 / 180.0, 1500, 1833);
            var songBeatmap = map(250, 60000 / 180.0, 760);

            double offset = SongSwapping.Offset(mapBeatmap, songBeatmap, 1);

            Assert.That(offset, Is.EqualTo(750).Within(0.01));
            Assert.That((250 + offset - 1000) % (60000 / 180.0), Is.EqualTo(0).Within(0.01), "its beat on the map's");
        }

        [Test]
        public void TestSpinnersSkipped()
        {
            var beatmap = map(0, 500);
            beatmap.HitObjects.Add(new Spinner { StartTime = 100, Duration = 1000 });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 1500 });

            Assert.That(SongSwapping.FirstObjectTime(beatmap), Is.EqualTo(1500));
        }

        private static Beatmap withKiai(Beatmap beatmap, double kiai)
        {
            beatmap.ControlPointInfo.Add(kiai, new EffectControlPoint { KiaiMode = true });
            return beatmap;
        }

        [Test]
        public void TestBestPartByKiai()
        {
            // The map's kiai at 30s, the song's at 60s (a 120s song): the song's kiai goes on the map's, and it covers the whole map.
            var mapBeatmap = withKiai(map(0, 500, 1000, 90000), 30000);
            var songBeatmap = withKiai(map(0, 500, 2000, 100000), 60000);

            var placement = SongSwapping.Place(mapBeatmap, songBeatmap, 1, 120000);

            Assert.That(placement.By, Is.EqualTo(SongSwapping.Anchor.Kiai));
            Assert.That(placement.Offset, Is.EqualTo(-30000).Within(1));
            Assert.That(placement.Coverage, Is.EqualTo(1).Within(0.01));
        }

        [Test]
        public void TestFirstNotesWhenKiaiWouldLeaveTheMapShort()
        {
            // Lining up the kiai would end before the map does; the first notes cover it without adjustment.
            var mapBeatmap = withKiai(map(0, 500, 1000, 90000), 10000);
            var songBeatmap = withKiai(map(0, 500, 1000, 60000), 55000);

            var placement = SongSwapping.Place(mapBeatmap, songBeatmap, 1, 92000);

            Assert.That(placement.By, Is.EqualTo(SongSwapping.Anchor.FirstObject));
            Assert.That(placement.Offset, Is.EqualTo(0).Within(1));
        }

        [TestCase(1)]
        [TestCase(1.25)]
        [TestCase(0.8)]
        public void TestPlacementShiftedToCoverWholeMap(double stretch)
        {
            var mapBeatmap = withKiai(map(0, 500, 1000, 90000), 10000);
            var songBeatmap = withKiai(map(0, 500, 10000 * stretch, 95000 * stretch), 55000 * stretch);

            var placement = SongSwapping.Place(mapBeatmap, songBeatmap, stretch, 90000 * stretch);

            Assert.That(placement.Offset, Is.InRange(0, 1000));
            Assert.That(placement.Offset + 90000, Is.GreaterThanOrEqualTo(90000));
            Assert.That(placement.Coverage, Is.EqualTo(1));
        }

        [Test]
        public void TestTooShortSongRefused()
        {
            Assert.Throws<InvalidOperationException>(() => SongSwapping.Place(map(0, 500, 1000, 90000), map(0, 500, 1000), 1, 62000));
        }

        [Test]
        public void TestPlacementCoversOpeningSpinner()
        {
            var mapBeatmap = map(0, 500, 1500, 90000);
            mapBeatmap.HitObjects.Insert(0, new Spinner { StartTime = 100, Duration = 1000 });

            var placement = SongSwapping.Place(mapBeatmap, map(0, 500, 1000), 1, 90000);

            Assert.That(placement.Offset, Is.LessThanOrEqualTo(100));
            Assert.That(placement.Offset + 90000, Is.GreaterThanOrEqualTo(90000));
        }

        [Test]
        public void TestCoverageTakesPriorityOverBeatSnap()
        {
            var placement = SongSwapping.Place(map(0, 500, 1000.25, 90000.25), map(0, 500, 0), 1, 89000);

            Assert.That(placement.Offset, Is.EqualTo(1000.25));
            Assert.That(placement.Coverage, Is.EqualTo(1));
        }

        [Test]
        public void TestRender()
        {
            if (!Bass.Init(0) && Bass.LastError != Errors.Already)
                Assert.Ignore($"BASS couldn't start here ({Bass.LastError}).");

            byte[] song = tone(seconds: 2);

            // A second later, at its own speed, for a map ending at 3s: silence after it to 2s past the map's end.
            byte[] placed = SongSwapping.Render(song, 1, 1000, 3000);
            Assert.That(durationMs(placed), Is.EqualTo(3000 + SongSwapping.TAIL).Within(50));
            Assert.That(peak(placed, fromMs: 0, toMs: 900), Is.EqualTo(0), "silent before it");
            Assert.That(peak(placed, fromMs: 1100, toMs: 2900), Is.GreaterThan(10000), "the song after, full");
            Assert.That(peak(placed, fromMs: 3100, toMs: 4900), Is.EqualTo(0), "silent after it");

            // Twice as fast: half as long (its pitch kept by the stretching).
            byte[] faster = SongSwapping.Render(song, 2, 0, 0);
            Assert.That(peak(faster, fromMs: 100, toMs: 900), Is.GreaterThan(10000));
            Assert.That(peak(faster, fromMs: 1100, toMs: 1900), Is.EqualTo(0), "over at 1s");

            // Started half a second in: what's left of it faded in.
            byte[] cut = SongSwapping.Render(song, 1, -500, 0);
            Assert.That(peak(cut, fromMs: 0, toMs: 100), Is.LessThan(peak(cut, fromMs: 1000, toMs: 1400) / 4), "faded in");
            Assert.That(peak(cut, fromMs: 1100, toMs: 1400), Is.GreaterThan(10000));
            Assert.That(peak(cut, fromMs: 1600, toMs: 1900), Is.EqualTo(0), "over at 1.5s");

            // Longer than the map (10s, the map over at 2s): full for 2s after it, then faded out over 3s, and cut there.
            byte[] longer = SongSwapping.Render(tone(seconds: 10), 1, 0, 2000);
            Assert.That(durationMs(longer), Is.EqualTo(2000 + SongSwapping.TAIL + SongSwapping.FADE_OUT).Within(50));
            Assert.That(peak(longer, fromMs: 3000, toMs: 3900), Is.GreaterThan(10000), "full after the map, for a while");
            Assert.That(peak(longer, fromMs: 5500, toMs: 5600), Is.LessThan(10000).And.GreaterThan(1000), "fading");
            Assert.That(peak(longer, fromMs: 6900, toMs: 7000), Is.LessThan(500), "faded out");
        }

        /// <summary>A 440Hz tone, 16-bit stereo 44.1kHz.</summary>
        private static byte[] tone(double seconds)
        {
            const int rate = 44100;
            int samples = (int)(rate * seconds);

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + samples * 4);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)2);
            writer.Write(rate);
            writer.Write(rate * 4);
            writer.Write((short)4);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(samples * 4);

            for (int i = 0; i < samples; i++)
            {
                short v = (short)(Math.Sin(2 * Math.PI * 440 * i / rate) * 12000);
                writer.Write(v);
                writer.Write(v);
            }

            writer.Flush();
            return stream.ToArray();
        }

        private static int rateOf(byte[] wave) => BitConverter.ToInt32(wave, 24);

        private static int channelsOf(byte[] wave) => BitConverter.ToInt16(wave, 22);

        private static double durationMs(byte[] wave) => BitConverter.ToInt32(wave, 40) / 2.0 / channelsOf(wave) / rateOf(wave) * 1000;

        private static int peak(byte[] wave, double fromMs, double toMs)
        {
            int rate = rateOf(wave), channels = channelsOf(wave);
            int from = 44 + (int)(fromMs / 1000 * rate) * channels * 2;
            int to = Math.Min(wave.Length - 1, 44 + (int)(toMs / 1000 * rate) * channels * 2);

            int max = 0;

            for (int i = from; i + 1 < to; i += 2)
                max = Math.Max(max, Math.Abs((int)BitConverter.ToInt16(wave, i)));

            return max;
        }
    }
}

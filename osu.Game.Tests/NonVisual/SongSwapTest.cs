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

        [Test]
        public void TestRender()
        {
            if (!Bass.Init(0) && Bass.LastError != Errors.Already)
                Assert.Ignore($"BASS couldn't start here ({Bass.LastError}).");

            byte[] song = tone(seconds: 2);

            // A second later, at its own speed, at least 5s long.
            byte[] placed = SongSwapping.Render(song, 1, 1000, 5000);
            Assert.That(durationMs(placed), Is.EqualTo(5000).Within(50));
            Assert.That(peak(placed, fromMs: 0, toMs: 900), Is.EqualTo(0), "silent before it");
            Assert.That(peak(placed, fromMs: 1100, toMs: 2900), Is.GreaterThan(1000), "the song after");

            // Twice as fast: half as long (its pitch kept by the stretching), no minimum.
            byte[] faster = SongSwapping.Render(song, 2, 0, 0);
            Assert.That(durationMs(faster), Is.EqualTo(1000).Within(100));

            // Started half a second in.
            byte[] cut = SongSwapping.Render(song, 1, -500, 0);
            Assert.That(durationMs(cut), Is.EqualTo(1500).Within(50));
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

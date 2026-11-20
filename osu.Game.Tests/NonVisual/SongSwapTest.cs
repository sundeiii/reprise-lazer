// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
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
        [TestCase(180, 45, 4, 1)]
        [TestCase(180, 720, 0.25, 1)]
        [TestCase(180, 130, 1, 1.3846153846)]
        public void TestFits(double map, double song, double factor, double stretch)
        {
            var fit = SongSwapping.Fit(map, song);

            Assert.That(fit, Is.Not.Null);
            Assert.That(fit!.Value.Factor, Is.EqualTo(factor));
            Assert.That(fit.Value.Stretch, Is.EqualTo(stretch).Within(0.0001));
        }

        [TestCase(180, double.NaN)]
        [TestCase(double.PositiveInfinity, 180)]
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

            double offset = SongSwapTiming.Create(mapBeatmap, songBeatmap, 10000).Start;

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
        public void TestMainBpmNoteUsedInsteadOfSlowIntro()
        {
            var beatmap = map(0, 1000, 1000, 5000, 20000);
            beatmap.ControlPointInfo.Add(4000, new TimingControlPoint { BeatLength = 500 });
            Assert.That(SongSwapTiming.SyncTime(beatmap), Is.EqualTo(5000));
        }

        [Test]
        public void TestTempoFollowsBothTimelinesWithoutDrift()
        {
            var target = map(0, 500, 0, 20000);
            target.ControlPointInfo.Add(4000, new TimingControlPoint { BeatLength = 400 });
            // The source's mapped notes are mostly at 120 BPM, followed by a short 100 BPM outro.
            var song = map(0, 500, 0, 10000);
            song.ControlPointInfo.Add(9000, new TimingControlPoint { BeatLength = 600 });

            var plan = SongSwapTiming.Create(target, song, 40000);
            Assert.That(plan.Sections.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(plan.Start, Is.LessThanOrEqualTo(0));
            Assert.That(plan.End, Is.GreaterThanOrEqualTo(20000));
            for (int i = 1; i < plan.Sections.Count; i++)
            {
                Assert.That(plan.Sections[i].SongStart, Is.EqualTo(plan.Sections[i - 1].SongEnd).Within(0.001));
                Assert.That(plan.Sections[i].MapStart, Is.EqualTo(plan.Sections[i - 1].MapEnd).Within(0.001));
            }
            Assert.That(plan.Sections.Any(s => Math.Abs(s.MapStart - 4000) < 0.001), Is.True);
            Assert.That(plan.Sections.Any(s => Math.Abs(s.SongStart - 9000) < 0.001), Is.True);
            Assert.That(plan.Sections[0].Stretch, Is.EqualTo(1));
            Assert.That(plan.Sections.Single(s => Math.Abs(s.MapStart - 4000) < 0.001).Stretch, Is.EqualTo(1.25));
            Assert.That(plan.Sections.Single(s => Math.Abs(s.SongStart - 9000) < 0.001).MapStart, Is.EqualTo(8000).Within(0.001));
            Assert.That(plan.Sections[^1].Stretch, Is.EqualTo(1.5));
        }

        [Test]
        public void TestPulseInterpretationStaysStableAcrossTempoChanges()
        {
            var target = map(0, 300, 0, 10000);
            var song = map(0, 300, 0, 30000);
            song.ControlPointInfo.Add(20000, new TimingControlPoint { BeatLength = 60000.0 / 130 });
            var plan = SongSwapTiming.Create(target, song, 40000);

            Assert.That(plan.Sections[0].Stretch, Is.EqualTo(1).Within(0.0001));
            Assert.That(plan.Sections[^1].Stretch, Is.EqualTo(200.0 / 130).Within(0.0001), "keep the 1:1 pulse instead of flipping to half time");
        }

        [Test]
        public void TestTimingResetsInDragonforceAndChinurareta()
        {
            // Measured red timing points from difficulties 1001682 (Myth) and 1082260 (Desolate).
            var target = map(1390, 300, 19390, 440166);
            target.ControlPointInfo.Add(242590, new TimingControlPoint { BeatLength = 60000.0 / 170 });
            target.ControlPointInfo.Add(287766, new TimingControlPoint { BeatLength = 300 });
            var song = map(457, 60000.0 / 130, 457, 49654, 461413);
            song.ControlPointInfo.Add(49654, new TimingControlPoint { BeatLength = 300 });
            song.ControlPointInfo.Add(252777, new TimingControlPoint { BeatLength = 375 });
            song.ControlPointInfo.Add(288613, new TimingControlPoint { BeatLength = 300 });

            var plan = SongSwapTiming.Create(target, song, 483604.898);
            double placedAt(double sourceTime)
            {
                var section = plan.Sections.First(s => sourceTime >= s.SongStart - 0.001 && sourceTime <= s.SongEnd + 0.001);
                return section.MapStart + (sourceTime - section.SongStart) / section.Stretch;
            }

            Assert.That(placedAt(49654), Is.EqualTo(19390).Within(0.001));
            Assert.That(placedAt(252777), Is.EqualTo(222490).Within(0.001));
            Assert.That(placedAt(288613), Is.EqualTo(252825.294117647).Within(0.001));
            Assert.That(placedAt(318313), Is.EqualTo(287766).Within(0.001), "the later 200 BPM section must land on beat, without the previous 108ms drift");
            Assert.That(plan.Start, Is.LessThanOrEqualTo(19390));
            Assert.That(plan.End, Is.GreaterThanOrEqualTo(440166));
        }

        [Test]
        public void TestTempoChangesBeforeSyncNote()
        {
            var target = map(0, 500, 1000, 20000);
            var song = map(0, 500, 10000, 30000);
            song.ControlPointInfo.Add(5000, new TimingControlPoint { BeatLength = 400 });
            var plan = SongSwapTiming.Create(target, song, 40000);

            Assert.That(plan.Sections[0].Stretch, Is.EqualTo(1));
            Assert.That(plan.Sections[0].SongEnd, Is.EqualTo(5000).Within(0.001));
            Assert.That(plan.Sections[1].Stretch, Is.EqualTo(0.8));
            Assert.That(plan.Sections[1].MapStart, Is.EqualTo(-5000).Within(0.001));
            Assert.That(plan.End, Is.GreaterThan(20000));
        }

        [Test]
        public void TestDynamicTempoCanMakeAnOtherwiseLongSongTooShort()
        {
            var target = map(0, 500, 0, 10000);
            target.ControlPointInfo.Add(1000, new TimingControlPoint { BeatLength = 400 });
            Assert.Throws<InvalidOperationException>(() => SongSwapTiming.Create(target, map(0, 500, 0), 11000));
        }

        [Test]
        public void TestTooShortSongRefused()
        {
            Assert.Throws<InvalidOperationException>(() => SongSwapTiming.Create(map(0, 500, 1000, 90000), map(0, 500, 1000), 62000));
        }

        [Test]
        public void TestPlacementCoversOpeningSpinner()
        {
            var target = map(0, 500, 1500, 90000);
            target.HitObjects.Insert(0, new Spinner { StartTime = 100, Duration = 1000 });
            var plan = SongSwapTiming.Create(target, map(0, 500, 1000), 90000);
            Assert.That(plan.Start, Is.LessThanOrEqualTo(100));
            Assert.That(plan.End, Is.GreaterThanOrEqualTo(90000));
        }

        [Test]
        public void TestCoverageTakesPriorityOverBeatSnap()
        {
            var plan = SongSwapTiming.Create(map(0, 500, 1000.25, 90000.25), map(0, 500, 0), 89000);
            Assert.That(plan.Start, Is.EqualTo(1000.25).Within(0.001));
            Assert.That(plan.End, Is.EqualTo(90000.25).Within(0.001));
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

        [Test]
        public void TestRenderTempoSectionsPreservesPitchAndTiming()
        {
            if (!Bass.Init(0) && Bass.LastError != Errors.Already)
                Assert.Ignore($"BASS couldn't start here ({Bass.LastError}).");

            // Markers in the source at 0.4-1s and 3-4s must land at 0.5-1.25s and 3.3-4.1s.
            byte[] source = tone(6, t => t is >= 0.4 and < 1 or >= 3 and < 4 ? 1 : 0);
            var plan = new SongSwapTiming.Plan(new[]
            {
                new SongSwapTiming.Section(0, 2000, 0, 0.8),
                new SongSwapTiming.Section(2000, 6000, 2500, 1.25),
            });
            byte[] rendered = SongSwapping.Render(source, plan, 5000);
            Assert.That(peak(rendered, 600, 1100), Is.GreaterThan(10000));
            Assert.That(peak(rendered, 1500, 2000), Is.LessThan(500));
            Assert.That(peak(rendered, 3450, 3950), Is.GreaterThan(10000));
            Assert.That(peak(rendered, 4300, 4800), Is.LessThan(500));
            Assert.That(frequency(rendered, 650, 1050), Is.EqualTo(440).Within(8));
            Assert.That(frequency(rendered, 3500, 3900), Is.EqualTo(440).Within(8));
        }

        private static double frequency(byte[] wave, double fromMs, double toMs)
        {
            int rate = rateOf(wave), channels = channelsOf(wave);
            int first = (int)(fromMs * rate / 1000), last = (int)(toMs * rate / 1000);
            int crossings = 0;
            for (int frame = first + 1; frame < last; frame++)
            {
                if (BitConverter.ToInt16(wave, 44 + (frame - 1) * channels * 2) <= 0
                    && BitConverter.ToInt16(wave, 44 + frame * channels * 2) > 0)
                    crossings++;
            }
            return crossings * 1000.0 / (toMs - fromMs);
        }

        /// <summary>A 440Hz tone, 16-bit stereo 44.1kHz.</summary>
        private static byte[] tone(double seconds, Func<double, double>? volume = null)
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
                short v = (short)(Math.Sin(2 * Math.PI * 440 * i / rate) * 12000 * (volume?.Invoke((double)i / rate) ?? 1));
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

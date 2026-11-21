// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
    /// <summary>
    /// Reprise: song swap's placement (the song's best part on the map's, moved by whole bars, looped when short) and its beats in the rendered
    /// audio, measured to the sample on click tracks.
    /// </summary>
    [TestFixture]
    public class SongSwapBeatTest
    {
        private static Beatmap map(double timingAt, double beatLength, params double[] objects)
        {
            var beatmap = new Beatmap();
            beatmap.ControlPointInfo.Add(timingAt, new TimingControlPoint { BeatLength = beatLength });
            foreach (double t in objects)
                beatmap.HitObjects.Add(new HitCircle { StartTime = t });
            return beatmap;
        }

        private static Beatmap withKiai(Beatmap beatmap, double from, double to)
        {
            beatmap.ControlPointInfo.Add(from, new EffectControlPoint { KiaiMode = true });
            beatmap.ControlPointInfo.Add(to, new EffectControlPoint { KiaiMode = false });
            return beatmap;
        }

        private static double placedAt(SongSwapTiming.Plan plan, double songTime)
        {
            var section = plan.Sections.First(s => songTime >= s.SongStart - 0.001 && songTime <= s.SongEnd + 0.001);
            return section.MapStart + (songTime - section.SongStart) / section.Stretch;
        }

        [Test]
        public void TestKiaiOnKiai()
        {
            // 120 BPM both. The map's chorus is at 30s, the song's at 50s: its chorus lands on the map's, not its first note on the map's.
            var target = withKiai(map(0, 500, 2000, 90000), 30000, 45000);
            var song = withKiai(map(0, 500, 1000, 170000), 50000, 70000);

            var plan = SongSwapTiming.Create(target, song, 180000);

            Assert.That(plan.By, Is.EqualTo(SongSwapTiming.Anchor.Kiai));
            Assert.That(placedAt(plan, 50000), Is.EqualTo(30000).Within(0.01));
            Assert.That(plan.Loops, Is.Zero);
        }

        [Test]
        public void TestShortKiaiFlashesSkipped()
        {
            // A one-beat kiai flash at 10s isn't the chorus; the long kiai at 30s is.
            var target = withKiai(withKiai(map(0, 500, 2000, 90000), 10000, 10500), 30000, 45000);
            Assert.That(SongSwapTiming.KiaiTime(target), Is.EqualTo(30000));
        }

        [Test]
        public void TestPreviewPointsWhenNoKiai()
        {
            var target = map(0, 500, 2000, 90000);
            target.Metadata.PreviewTime = 40000;
            var song = map(0, 500, 1000, 170000);
            song.Metadata.PreviewTime = 60000;

            var plan = SongSwapTiming.Create(target, song, 180000);

            Assert.That(plan.By, Is.EqualTo(SongSwapTiming.Anchor.Preview));
            Assert.That(placedAt(plan, 60000), Is.EqualTo(40000).Within(0.01));
        }

        [Test]
        public void TestExactFirstNotesBeatAMovedKiai()
        {
            // The song's chorus on the map's would start the song after the map's first note, so it'd have to be moved anyway; its first
            // note on the map's fits as it is, so that's used.
            var target = withKiai(map(0, 500, 2000, 90000), 60000, 75000);
            var song = withKiai(map(0, 500, 1000, 170000), 10000, 30000);

            var plan = SongSwapTiming.Create(target, song, 180000);

            Assert.That(plan.By, Is.EqualTo(SongSwapTiming.Anchor.FirstNotes));
            Assert.That(placedAt(plan, 1000), Is.EqualTo(2000).Within(0.01));
        }

        [Test]
        public void TestMovedByWholeBarsToCover()
        {
            // A 95s song whose first note (8s) on the map's (2s) would end it before the map does: moved later by a whole bar (2s at
            // 120 BPM in 4/4), keeping its phrases on the map's bars, rather than by the 1s that would just about cover it.
            var target = map(0, 500, 2000, 90000);
            var song = map(0, 500, 8000, 94000);

            var plan = SongSwapTiming.Create(target, song, 95000);

            Assert.That(placedAt(plan, 8000) - 2000, Is.EqualTo(2000).Within(0.01));
            Assert.That(plan.Start, Is.LessThanOrEqualTo(2000));
            Assert.That(plan.End, Is.GreaterThanOrEqualTo(90000));
        }

        [Test]
        public void TestShortSongLoopedOnBars()
        {
            // A 40s song for a 90s map: looped on whole bars from its first note's bar, its last pass played out to its end.
            var target = map(0, 500, 2000, 90000);
            var song = map(0, 500, 2000, 38000);

            Assert.Throws<InvalidOperationException>(() => SongSwapTiming.Create(target, song, 40000));

            var plan = SongSwapTiming.Create(target, song, 40000, loop: true);

            Assert.That(plan.Loops, Is.GreaterThan(0));
            Assert.That(plan.Start, Is.LessThanOrEqualTo(2000));
            Assert.That(plan.End, Is.GreaterThanOrEqualTo(90000 + SongSwapping.TAIL));
            Assert.That(plan.Sections[^1].SongEnd, Is.EqualTo(40000).Within(0.01), "the last pass plays to the song's end");

            var seams = plan.Sections.Where(s => s.Seam).ToList();
            Assert.That(seams.Count, Is.EqualTo(plan.Loops + 1));

            foreach (var seam in seams)
            {
                Assert.That(seam.SongStart % 2000, Is.EqualTo(0).Within(0.01), "loops start on a bar");
                Assert.That(seam.MapStart % 500, Is.EqualTo(0).Within(0.01), "and land on the map's beat");
                Assert.That(seam.Join, Is.EqualTo(SongSwapTiming.SEAM_JOIN));
            }

            // Each pass picks up exactly where the previous one ended on the map.
            for (int i = 1; i < plan.Sections.Count; i++)
                Assert.That(plan.Sections[i].MapStart, Is.EqualTo(plan.Sections[i - 1].MapEnd).Within(0.01));
        }

        [Test]
        public void TestLoopedFromTheChorus()
        {
            var target = map(0, 500, 2000, 120000);
            var song = withKiai(map(0, 500, 2000, 58000), 20000, 40000);

            var plan = SongSwapTiming.Create(target, song, 60000, loop: true);

            Assert.That(plan.Sections.Where(s => s.Seam).All(s => Math.Abs(s.SongStart - 20000) < 0.01), Is.True);
        }

        [TestCase(130, 156, true)]
        [TestCase(180, 175, true)]
        [TestCase(170, 140, true)]
        [TestCase(200, 100, true)]
        [TestCase(170, 140, false)]
        [TestCase(150, 210, false)]
        public void TestRenderedBeatsLandOnTheMap(double songBpm, double mapBpm, bool keepPitch)
        {
            initBass();

            var errors = renderClicks(songBpm, mapBpm, songSeconds: 120, keepPitch, loop: false);

            Assert.That(errors.Count, Is.GreaterThan(50));
            Assert.That(Math.Abs(errors.Average()), Is.LessThan(1.5), "on average, on the beat (not early or late)");
            Assert.That(errors.Max(Math.Abs), Is.LessThan(5), "and each beat within a few milliseconds");
            Assert.That(errors.Take(10).Average() - errors.TakeLast(10).Average(), Is.EqualTo(0).Within(2), "without drifting");
        }

        [Test]
        public void TestLoopedBeatsLandOnTheMap()
        {
            initBass();

            var errors = renderClicks(130, 156, songSeconds: 20, keepPitch: true, loop: true);

            Assert.That(errors.Count, Is.GreaterThan(100), "the whole minute is covered");
            Assert.That(Math.Abs(errors.Average()), Is.LessThan(1.5));
            Assert.That(errors.Max(Math.Abs), Is.LessThan(5));
        }

        [Test]
        public void TestPreviewWindowAndTicks()
        {
            initBass();

            byte[] song = clicks(60, new List<double>());
            var target = map(0, 500, 1000, 50000);
            var plan = SongSwapTiming.Create(target, map(0, 500, 1000, 55000), SongSwapping.LengthOf(song));

            byte[] preview = SongSwapping.Render(song, plan, 50000, new SongSwapRenderOptions(From: 20000, To: 30000, Ticks: new[] { 25000.0 }));

            Assert.That(durationMs(preview), Is.EqualTo(10000).Within(5));
            Assert.That(onsets(preview).Select(o => o + 20000), Is.EqualTo(new[] { 25000.0 }).Within(1), "a tick on the map's note");
        }

        [TestCase(12)]
        [TestCase(-18)]
        [TestCase(35)]
        public void TestBeatFeelMeasured(double late)
        {
            initBass();

            // Clicks every beat of a 150 BPM map, and the same 'late' ms after the timing points (as a mapper's offset might leave them).
            // Only the difference between two measurements is used (to match one song's feel to another's), so that's what's checked.
            var grid = map(500, 400, 500, 60000);
            byte[] clicksAt(double shift) => clicks(62, Enumerable.Range(0, 200).Select(i => 500 + shift + i * 400.0).Where(t => t > 0 && t < 61900).ToList());

            var onTime = SongSwapAnalysis.Measure(SongSwapAnalysis.OnsetStrength(clicksAt(0)), 0, grid, 500, 60000);
            var shifted = SongSwapAnalysis.Measure(SongSwapAnalysis.OnsetStrength(clicksAt(late)), 0, grid, 500, 60000);

            Assert.That(onTime.Trusted && shifted.Trusted, Is.True);
            Assert.That(shifted.Lag - onTime.Lag, Is.EqualTo(late).Within(0.5));

            // Measured on a window (a preview's), its strength starting 20s into the map: within a millisecond (a shorter excerpt reads a
            // little differently; nobody feels a millisecond).
            byte[] window = SongSwapping.Render(clicksAt(late), new SongSwapTiming.Plan(new[] { new SongSwapTiming.Section(0, 62000, 0, 1) }), 60000,
                new SongSwapRenderOptions(From: 20000, To: 40000));
            var part = SongSwapAnalysis.Measure(SongSwapAnalysis.OnsetStrength(window), 20000, grid, 20000, 40000);
            Assert.That(part.Lag - onTime.Lag, Is.EqualTo(late).Within(1));
        }

        [Test]
        public void TestFeelMatchedSectionBySection()
        {
            initBass();

            // A 120 BPM map whose own song is right on its beats, and a song at the same tempo whose mapper put the music 8ms after their first
            // timing point and, after resetting the beat at 60s, 6ms before it.
            var target = map(0, 500, 1000, 118000);
            byte[] own = clicks(122, Enumerable.Range(0, 240).Select(i => i * 500.0).ToList());
            var song = map(0, 500, 1000, 118000);
            song.ControlPointInfo.Add(60000, new TimingControlPoint { BeatLength = 500 });
            byte[] songAudio = clicks(122, Enumerable.Range(0, 240).Select(i => i * 500.0 + (i < 120 ? 8 : -6)).Where(t => t > 0).ToList());

            var plan = SongSwapTiming.Create(target, song, SongSwapping.LengthOf(songAudio));
            Assert.That(plan.Sections.Count, Is.GreaterThanOrEqualTo(2), "kept apart at the song's own timing point");

            byte[] first = SongSwapping.Render(songAudio, plan, 118000);
            var matched = SongSwapAnalysis.MatchFeel(plan, target, SongSwapAnalysis.OnsetStrength(own), SongSwapAnalysis.OnsetStrength(first), 0, out _);
            byte[] rendered = SongSwapping.Render(songAudio, matched, 118000);

            var ownPhase = SongSwapAnalysis.Measure(SongSwapAnalysis.OnsetStrength(own), 0, target, 1000, 118000);
            var strength = SongSwapAnalysis.OnsetStrength(rendered);
            var before = SongSwapAnalysis.Measure(strength, 0, target, 2000, 58000);
            var after = SongSwapAnalysis.Measure(strength, 0, target, 62000, 117000);

            Assert.That(before.Lag - ownPhase.Lag, Is.EqualTo(0).Within(1.5), "its first part on the map's beats as the map's own song is");
            Assert.That(after.Lag - ownPhase.Lag, Is.EqualTo(0).Within(1.5), "and its second, moved by its own amount");
        }

        [Test]
        public void TestNoSilenceWhereAPartMovesLater()
        {
            initBass();

            // The second section moved 20ms later than where the first ends: the first plays on until it takes over.
            byte[] tone = clicks(25, new List<double>());
            for (int i = 44; i + 1 < tone.Length; i += 2)
                BitConverter.GetBytes((short)(Math.Sin(2 * Math.PI * 440 * ((i - 44) / 4) / 44100.0) * 12000)).CopyTo(tone, i);

            var plan = new SongSwapTiming.Plan(new[]
            {
                new SongSwapTiming.Section(0, 10000, 0, 1),
                new SongSwapTiming.Section(10000, 20000, 10020, 1, SongSwapTiming.TEMPO_JOIN),
            });
            byte[] rendered = SongSwapping.Render(tone, plan, 19000);

            for (double ms = 9900; ms < 10200; ms += 2)
                Assert.That(peakBetween(rendered, ms, ms + 3), Is.GreaterThan(3000), $"sound at {ms}ms");
        }

        private static int peakBetween(byte[] wave, double fromMs, double toMs)
        {
            int rate = BitConverter.ToInt32(wave, 24), channels = BitConverter.ToInt16(wave, 22);
            int max = 0;
            for (int f = (int)(fromMs * rate / 1000); f < (int)(toMs * rate / 1000); f++)
                max = Math.Max(max, Math.Abs((int)BitConverter.ToInt16(wave, 44 + f * channels * 2)));
            return max;
        }

        [Test]
        public void TestNoBeatNotTrusted()
        {
            initBass();
            Assert.That(SongSwapAnalysis.Measure(SongSwapAnalysis.OnsetStrength(clicks(30, new List<double>())), 0, map(0, 400, 0, 29000), 0, 29000).Trusted, Is.False);
        }

        [Test]
        public void TestPlanShifted()
        {
            var plan = SongSwapTiming.Create(map(0, 500, 1000, 50000), map(0, 500, 1000, 55000), 60000).Shifted(-15);
            Assert.That(placedAt(plan, 1000), Is.EqualTo(985).Within(0.01));
        }

        [Test]
        public void TestLegacySpinnersSkipped()
        {
            // A map as read from its file: legacy objects (song swap reads maps this way), its spinner first.
            const string file = @"osu file format v14

[General]
AudioFilename: audio.mp3

[Difficulty]
SliderMultiplier:1.4
SliderTickRate:1

[TimingPoints]
0,500,4,2,0,60,1,0

[HitObjects]
256,192,100,12,0,1100,0:0:0:0:
256,192,1500,1,0,0:0:0:0:
";
            using var stream = new osu.Game.IO.LineBufferedReader(new MemoryStream(Encoding.UTF8.GetBytes(file)));
            var beatmap = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(stream).Decode(stream);

            Assert.That(SongSwapping.FirstObjectTime(beatmap), Is.EqualTo(1500));
        }

        private static List<double> renderClicks(double songBpm, double mapBpm, double songSeconds, bool keepPitch, bool loop)
        {
            double songBeat = 60000 / songBpm, mapBeat = 60000 / mapBpm;
            byte[] song = clicks(songSeconds, Enumerable.Range(0, 10000).Select(i => 300 + i * songBeat).Where(t => t < songSeconds * 1000 - 50).ToList());
            var plan = SongSwapTiming.Create(map(1000, mapBeat, 1000, 60000), map(300, songBeat, 300, songSeconds * 1000 - 1000), SongSwapping.LengthOf(song), loop);
            byte[] rendered = SongSwapping.Render(song, plan, 60000, new SongSwapRenderOptions(keepPitch));

            // Clicks on the song's beats (or half beats, for a song at double time) measured against the map's.
            return onsets(rendered).Where(o => o > 950 && o < 60000)
                                   .Select(o => o - (1000 + Math.Round((o - 1000) / mapBeat * 2) / 2 * mapBeat))
                                   .ToList();
        }

        private static void initBass()
        {
            if (!Bass.Init(0) && Bass.LastError != Errors.Already)
                Assert.Ignore($"BASS couldn't start here ({Bass.LastError}).");
        }

        private static List<double> onsets(byte[] wave)
        {
            int rate = BitConverter.ToInt32(wave, 24), channels = BitConverter.ToInt16(wave, 22);
            int frames = (wave.Length - 44) / 2 / channels;
            var result = new List<double>();
            int lastLoud = -rate;

            for (int f = 0; f < frames; f++)
            {
                if (Math.Abs((int)BitConverter.ToInt16(wave, 44 + f * channels * 2)) <= 6000)
                    continue;
                if (f - lastLoud > rate / 20)
                    result.Add(f * 1000.0 / rate);
                lastLoud = f;
            }

            return result;
        }

        private static double durationMs(byte[] wave) => BitConverter.ToInt32(wave, 40) / 2.0 / BitConverter.ToInt16(wave, 22) / BitConverter.ToInt32(wave, 24) * 1000;

        /// <summary>5ms 2kHz clicks (a sharp attack) at the given times, 16-bit stereo 44.1kHz.</summary>
        private static byte[] clicks(double seconds, List<double> at)
        {
            const int rate = 44100;
            int samples = (int)(rate * seconds);
            var data = new short[samples];

            foreach (double t in at)
            {
                int s = (int)Math.Round(t * rate / 1000);
                for (int i = 0; i < rate * 0.005 && s + i < samples; i++)
                    data[s + i] = (short)(Math.Sin(2 * Math.PI * 2000 * i / rate) * 20000 * (1 - i / (rate * 0.005)));
            }

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

            foreach (short v in data)
            {
                writer.Write(v);
                writer.Write(v);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}

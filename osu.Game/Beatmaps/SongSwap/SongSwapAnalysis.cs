// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ManagedBass;

namespace osu.Game.Beatmaps.SongSwap
{
    /// <summary>
    /// Where a song's beats sit in its audio, relative to a map's beat grid: what players feel as a map being on time. Mappers time their
    /// maps a little differently (often 10-20ms apart), so a song lined up by its own map's timing points can feel early or late on another
    /// map. Measuring the music itself lets the swapped song sit on the map exactly as the map's own song did.
    /// </summary>
    public static class SongSwapAnalysis
    {
        /// <summary>How far either side of a beat (ms) the music's beat is looked for.</summary>
        public const int RANGE = 60;

        /// <summary>How clearly a beat must stand out (its peak over the average around it) to be trusted.</summary>
        public const double MIN_CLARITY = 2.5;

        /// <param name="Lag">Where the music's beat is (ms) after the grid's: negative, before it.</param>
        /// <param name="Clarity">How clearly it stands out (its peak over the average within <see cref="RANGE"/>): around 1, no beat found.</param>
        public record Phase(double Lag, double Clarity)
        {
            public bool Trusted => Clarity >= MIN_CLARITY;
        }

        /// <summary>
        /// How strongly something starts (a drum hit, a note) at each millisecond of a song (wave or any format BASS reads; BASS must be
        /// set up): the rise in loudness of its high-emphasised signal.
        /// </summary>
        public static float[] OnsetStrength(byte[] audio)
        {
            int decode = Bass.CreateStream(audio, 0, audio.Length, BassFlags.Decode | BassFlags.Prescan | BassFlags.Float);
            if (decode == 0)
                throw new InvalidOperationException($"The song couldn't be read ({Bass.LastError}).");

            var energy = new List<double>();

            try
            {
                var info = Bass.ChannelGetInfo(decode);
                int channels = info.Channels;
                double framesPerMs = info.Frequency / 1000.0;
                var buffer = new float[65536 - 65536 % channels];
                var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                try
                {
                    // Energy of the first difference (which brings out attacks over sustained sound), per millisecond.
                    long frame = 0;
                    float previous = 0;
                    double sum = 0;
                    int bytes;

                    while ((bytes = Bass.ChannelGetData(decode, pinned.AddrOfPinnedObject(), buffer.Length * sizeof(float))) > 0)
                    {
                        for (int i = 0; i + channels <= bytes / sizeof(float); i += channels, frame++)
                        {
                            float mono = 0;
                            for (int c = 0; c < channels; c++)
                                mono += buffer[i + c];
                            mono /= channels;

                            double d = mono - previous;
                            previous = mono;
                            sum += d * d;

                            if (frame + 1 >= (long)Math.Round((energy.Count + 1) * framesPerMs))
                            {
                                energy.Add(sum);
                                sum = 0;
                            }
                        }
                    }
                }
                finally
                {
                    pinned.Free();
                }
            }
            finally
            {
                Bass.StreamFree(decode);
            }

            // Log energy over 5ms; its rise over 3ms is the onset strength.
            var smoothed = new double[energy.Count];
            double window = 0;
            for (int m = 0; m < energy.Count; m++)
            {
                window += energy[m] - (m >= 5 ? energy[m - 5] : 0);
                smoothed[m] = Math.Log(Math.Max(window, 1e-10));
            }

            var strength = new float[energy.Count];
            for (int m = 3; m < strength.Length; m++)
                strength[m] = (float)Math.Max(0, smoothed[m] - smoothed[m - 3]);

            return strength;
        }

        /// <summary>
        /// Where the music's beat sits around a map's beats between two of its times (ms), folding its onset strength onto the beats.
        /// </summary>
        /// <param name="strength">Its onset strength (see <see cref="OnsetStrength"/>).</param>
        /// <param name="strengthStart">The map's time (ms) the strength's first millisecond is at.</param>
        /// <param name="around">Where (ms from the beats) the beat's looked for (else at the beats themselves).</param>
        /// <param name="search">How far either side of <paramref name="around"/> (ms) it's looked for. Mappers put their beats within a few tens
        /// of milliseconds of the music's; further out are pickups (a sixteenth before the beat at 200 BPM is 75ms), which can be as loud as the
        /// beat itself and mustn't be taken for it.</param>
        public static Phase Measure(float[] strength, double strengthStart, IBeatmap grid, double from, double to, double around = 0, double search = SEARCH)
        {
            var folded = new double[RANGE * 2 + 1];
            int beats = 0;
            var points = grid.ControlPointInfo.TimingPoints;

            for (int p = 0; p < points.Count; p++)
            {
                double beatLength = points[p].BeatLength;
                if (!(beatLength > 20))
                    continue;

                double end = p + 1 < points.Count ? points[p + 1].Time - 1 : to;
                double first = points[p].Time + Math.Max(0, Math.Ceiling((from - points[p].Time) / beatLength)) * beatLength;

                for (double beat = first; beat <= Math.Min(end, to); beat += beatLength)
                {
                    int centre = (int)Math.Round(beat - strengthStart);
                    if (centre - RANGE < 0 || centre + RANGE >= strength.Length)
                        continue;

                    for (int lag = -RANGE; lag <= RANGE; lag++)
                        folded[lag + RANGE] += strength[centre + lag];
                    beats++;
                }
            }

            if (beats < MIN_BEATS)
                return new Phase(0, 0);

            int low = Math.Max(1, (int)Math.Ceiling(around - search) + RANGE);
            int high = Math.Min(folded.Length - 2, (int)Math.Floor(around + search) + RANGE);
            if (low > high)
                return new Phase(0, 0);

            double total = 0;
            for (int i = 0; i < folded.Length; i++)
                total += folded[i];

            int best = low;
            for (int i = low; i <= high; i++)
            {
                if (folded[i] > folded[best])
                    best = i;
            }

            double mean = total / folded.Length;
            if (mean <= 0)
                return new Phase(0, 0);

            // At the edge of where it's looked for: likely something stronger just outside it (not the beat), so not to be trusted.
            if (best == low || best == high)
                return new Phase(best - RANGE, 0);

            // Between milliseconds: the peak of a parabola through it and its neighbours.
            double offset = 0;
            double l = folded[best - 1], c = folded[best], r = folded[best + 1];
            double denominator = l - 2 * c + r;
            if (denominator < 0)
                offset = Math.Clamp(0.5 * (l - r) / denominator, -0.5, 0.5);

            return new Phase(best - RANGE + offset, folded[best] / mean);
        }

        /// <summary>How far either side of the beats (ms) a song's beat is looked for (see <see cref="Measure"/>).</summary>
        public const double SEARCH = 45;

        /// <summary>The fewest beats a measurement's made from.</summary>
        public const int MIN_BEATS = 16;

        /// <summary>How far (ms) a part of a song may sit from the whole song: further, and its measurement's off.</summary>
        public const double MAX_LOCAL_DIFFERENCE = 25;

        /// <summary>The most a swapped song's moved (ms) overall: more than this, and the measurement's off.</summary>
        public const double MAX_SHIFT = 50;

        /// <summary>The fewest of the map's beats a part of a swapped song is matched over (shorter tempo sections are matched with their neighbours).</summary>
        public const int PART_BEATS = 32;

        /// <summary>How clearly a part's beat must stand out for its own measurement to be used (higher than for the whole song: fewer beats).</summary>
        public const double MIN_PART_CLARITY = 3;

        /// <summary>
        /// A plan moved so the swapped song's beats sit on the map's as the map's own song's do, tempo section by tempo section: a song's own
        /// timing points can each be off by some milliseconds, and stretching moves beats a little, more so at big changes of speed, so each
        /// section (with its short neighbours, to at least <see cref="PART_BEATS"/> beats) is moved by how far its own beats sit from the map's
        /// own song's. Its beat is looked for near the whole song's, so a pickup can't be taken for it; a part whose beat isn't clear is moved
        /// with the whole song. (Measured on real songs, finer parts than tempo sections chased measuring noise more than they fixed.) Never
        /// so far it'd stop covering the map's notes.
        /// </summary>
        /// <param name="mapStrength">The map's own song's onset strength (from the start of the map).</param>
        /// <param name="rendered">The swapped song's onset strength, rendered with the plan, from <paramref name="renderedFrom"/> on the map.</param>
        /// <param name="overall">How far the whole of it was moved (ms).</param>
        public static SongSwapTiming.Plan MatchFeel(SongSwapTiming.Plan plan, IBeatmap map, float[] mapStrength, float[] rendered, double renderedFrom, out double overall)
        {
            overall = 0;

            double playStart = map.HitObjects.Count == 0 ? 0 : map.HitObjects.Min(h => h.StartTime);
            double playEnd = Math.Max(playStart + 1, SongSwapping.EndTime(map));
            double from = Math.Max(playStart, renderedFrom);
            double to = Math.Min(playEnd, renderedFrom + rendered.Length);

            var own = Measure(mapStrength, 0, map, playStart, playEnd);
            var whole = Measure(rendered, renderedFrom, map, from, to);
            if (!own.Trusted || !whole.Trusted || Math.Abs(own.Lag - whole.Lag) > MAX_SHIFT)
                return plan;

            overall = own.Lag - whole.Lag;

            var shifts = new double[plan.Sections.Count];
            int partStart = 0;

            for (int i = 0; i < plan.Sections.Count; i++)
            {
                double partFrom = Math.Max(from, plan.Sections[partStart].MapStart);
                double partTo = Math.Min(to, plan.Sections[i].MapEnd);
                bool last = i == plan.Sections.Count - 1;

                if (!last && beatsBetween(map, partFrom, partTo) < PART_BEATS)
                    continue;

                double shift = overall;

                if (partTo > partFrom)
                {
                    var part = Measure(rendered, renderedFrom, map, partFrom, partTo, whole.Lag, MAX_LOCAL_DIFFERENCE);
                    if (part.Clarity >= MIN_PART_CLARITY)
                        shift = own.Lag - part.Lag;
                }

                for (int j = partStart; j <= i; j++)
                    shifts[j] = shift;
                partStart = i + 1;
            }

            // Still starting by the first note and lasting to the last.
            shifts[0] = Math.Min(shifts[0], Math.Max(0, playStart - plan.Start));
            shifts[^1] = Math.Max(shifts[^1], Math.Min(0, playEnd - plan.End));

            return plan with { Sections = plan.Sections.Select((s, i) => s with { MapStart = s.MapStart + shifts[i] }).ToList() };
        }

        private static double beatsBetween(IBeatmap map, double from, double to)
        {
            if (to <= from)
                return 0;

            double beatLength = map.ControlPointInfo.TimingPointAt(from).BeatLength;
            return beatLength > 0 ? (to - from) / beatLength : 0;
        }
    }
}

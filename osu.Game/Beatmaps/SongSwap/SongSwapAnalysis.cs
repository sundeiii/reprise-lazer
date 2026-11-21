// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
        public static Phase Measure(float[] strength, double strengthStart, IBeatmap grid, double from, double to)
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

            if (beats < 16)
                return new Phase(0, 0);

            int best = 0;
            double total = 0;
            for (int i = 0; i < folded.Length; i++)
            {
                total += folded[i];
                if (folded[i] > folded[best])
                    best = i;
            }

            double mean = total / folded.Length;
            if (mean <= 0)
                return new Phase(0, 0);

            // Between milliseconds: the peak of a parabola through it and its neighbours.
            double offset = 0;
            if (best > 0 && best < folded.Length - 1)
            {
                double l = folded[best - 1], c = folded[best], r = folded[best + 1];
                double denominator = l - 2 * c + r;
                if (denominator < 0)
                    offset = Math.Clamp(0.5 * (l - r) / denominator, -0.5, 0.5);
            }

            return new Phase(best - RANGE + offset, folded[best] / mean);
        }
    }
}

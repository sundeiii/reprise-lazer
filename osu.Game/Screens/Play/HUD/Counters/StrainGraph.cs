// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Utils;

namespace osu.Game.Screens.Play.HUD.Counters
{
    /// <summary>
    /// Reprise: a map's difficulty over the song, as tosu gives overlays (its <c>performance.graph</c>) and BTMC's overlay draws it:
    /// the aim and speed strain peaks of each 400ms of the map added up, the song before the first note and after the last zeroes,
    /// then smoothed as the overlay does (fast-smooth.js, "Softly smooth": two sliding averages, as wide as the map is long).
    /// </summary>
    public static class StrainGraph
    {
        // osu!'s skills, as tosu names them ("aim" is the one with sliders, the first).
        private static readonly string[] channels = { @"Aim", @"Speed" };

        /// <summary>The graph's values (one per 400ms of the song at its rate), or empty for a ruleset without aim and speed.</summary>
        public static double[] Calculate(Ruleset ruleset, IWorkingBeatmap working, IBeatmap playable, IReadOnlyList<Mod> mods, double songLength,
                                         CancellationToken cancellationToken = default)
        {
            var peaks = ruleset.CreateDifficultyCalculator(working).CalculateStrainPeaks(mods, cancellationToken);

            var series = channels.Select(name => peaks.FirstOrDefault(p => p.Skill.GetType().Name == name).Peaks)
                                 .Where(p => p != null)
                                 .ToList();

            if (series.Count == 0)
                return Array.Empty<double>();

            int length = series.Max(s => s.Length);
            double[] strains = new double[length];

            foreach (double[] s in series)
            {
                for (int i = 0; i < s.Length; i++)
                    strains[i] += s[i];
            }

            return Smooth(pad(strains, playable.HitObjects, ModUtils.CalculateRateWithMods(mods), songLength));
        }

        // tosu's updateGraph: the strains placed in the song, nothing before the first note and after the last.
        private static double[] pad(double[] strains, IReadOnlyList<HitObject> objects, double rate, double songLength)
        {
            const double section = 400;

            if (objects.Count == 0)
                return strains;

            double start = objects[0].StartTime / rate;
            double end = objects.Max(h => h.GetEndTime()) / rate;

            var notSpinners = objects.Where(h => !(h is IHasDuration && h.GetType().Name.Contains(@"Spinner", StringComparison.Ordinal))).ToList();
            double firstGraphObject = Math.Round(notSpinners.FirstOrDefault()?.StartTime ?? 0) / rate;
            double lastGraphObject = Math.Round(notSpinners.LastOrDefault()?.StartTime ?? 0) / rate;
            double total = songLength / rate;

            int emptyLeft = (int)Math.Floor(start / section);
            int emptyRight = total >= end ? (int)Math.Ceiling((total - end) / section) : 0;
            int offsetLeft = (int)Math.Floor((firstGraphObject - start) / section);
            int offsetRight = total >= end ? (int)Math.Ceiling((end - lastGraphObject) / section) : 0;

            int left = Math.Max(0, emptyLeft) + Math.Max(0, offsetLeft);
            int right = Math.Max(0, emptyRight) + Math.Max(0, offsetRight);

            double[] padded = new double[left + strains.Length + right];
            Array.Copy(strains, 0, padded, left, strains.Length);
            return padded;
        }

        /// <summary>BTMC's smoothing: as wide as the map has notes ("drain" samples), two passes ("Softly smooth").</summary>
        public static double[] Smooth(double[] data)
        {
            if (data.Length == 0)
                return data;

            for (int i = 0; i < data.Length; i++)
                data[i] = Math.Max(0, data[i]);

            double percent = data.Max() / 100;
            int drainSamples = data.Count(d => d > percent);
            double windowWidth = 0.00609 * drainSamples + 0.88911;

            return slidingAverage(slidingAverage(data, windowWidth), windowWidth);
        }

        // fast-smooth.js's smooth(), without smoothing the ends.
        private static double[] slidingAverage(double[] array, double windowWidth)
        {
            int width = (int)Math.Floor(windowWidth + 0.5);
            if (width <= 1)
                return (double[])array.Clone();

            int half = (int)Math.Floor(width / 2.0 + 0.5);
            double[] result = new double[array.Length];

            void set(int index, double v)
            {
                if (index >= 0 && index < result.Length)
                    result[index] = v;
            }

            double sum = 0;
            for (int j = 0; j < width && j < array.Length; j++)
                sum += array[j];

            int i = 0;

            for (; i < array.Length - width + 1; i++)
            {
                set(i + half - 1, Math.Max(0, sum));
                sum -= array[i];
                if (i + width < array.Length)
                    sum += array[i + width];
            }

            double tail = 0;
            for (int j = Math.Max(0, array.Length - width + 1); j < array.Length; j++)
                tail += array[j];
            set(i + half, Math.Max(0, tail));

            for (int j = 0; j < result.Length; j++)
                result[j] /= width;

            return result;
        }
    }
}

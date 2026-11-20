// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps.ControlPoints;

namespace osu.Game.Beatmaps.SongSwap
{
    /// <summary>Matches musical beat positions across both timelines without changing the map's timing data.</summary>
    public static class SongSwapTiming
    {
        public record Section(double SongStart, double SongEnd, double MapStart, double Stretch)
        {
            public double MapEnd => MapStart + (SongEnd - SongStart) / Stretch;
        }

        public record Plan(IReadOnlyList<Section> Sections)
        {
            public double Start => Sections[0].MapStart;
            public double End => Sections[^1].MapEnd;
        }

        /// <summary>The first playable note in the main BPM section, falling back to the first non-spinner.</summary>
        public static double SyncTime(IBeatmap beatmap)
        {
            double mainBeatLength = beatmap.GetMostCommonBeatLength();
            return beatmap.HitObjects.FirstOrDefault(h => !SongSwapping.IsSpinner(h)
                                                        && Math.Abs(beatmap.ControlPointInfo.TimingPointAt(h.StartTime).BeatLength - mainBeatLength) < 0.01)?.StartTime
                   ?? SongSwapping.FirstObjectTime(beatmap);
        }

        /// <summary>
        /// Aligns the main-BPM notes and matches beat positions throughout both songs. Pulse interpretation is chosen once,
        /// so a tempo change cannot unexpectedly flip a song between half and double time.
        /// </summary>
        public static Plan Create(IBeatmap map, IBeatmap song, double songLength)
        {
            if (!double.IsFinite(songLength) || songLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(songLength));

            var target = new BeatTimeline(map.ControlPointInfo.TimingPoints);
            var source = new BeatTimeline(song.ControlPointInfo.TimingPoints);
            var fit = SongSwapping.Fit(60000 / map.GetMostCommonBeatLength(), 60000 / song.GetMostCommonBeatLength())
                      ?? throw new InvalidOperationException("The song has unsupported timing data.");
            double factor = fit.Factor;
            double playStart = map.HitObjects.Count == 0 ? 0 : map.HitObjects.Min(h => h.StartTime);
            double playEnd = Math.Max(playStart + 1, SongSwapping.EndTime(map));

            // A whole-beat offset aligns the pulse, including when the first objects sit between beats.
            double preferred = Math.Round(target.BeatAt(SyncTime(map)) - factor * source.BeatAt(Math.Clamp(SyncTime(song), 0, songLength)));
            double earliest = target.BeatAt(playEnd) - factor * source.BeatAt(songLength);
            double latest = target.BeatAt(playStart) - factor * source.BeatAt(0);
            if (earliest > latest + 1e-7)
                throw new InvalidOperationException("The replacement song is too short after matching the map's tempo changes. Pick a longer song.");

            // Retain beat alignment whenever there is room. A tight fit may need a fractional offset for full coverage.
            double firstBeat = Math.Ceiling(earliest - 1e-7);
            double lastBeat = Math.Floor(latest + 1e-7);
            double offset = firstBeat <= lastBeat ? Math.Clamp(preferred, firstBeat, lastBeat) : Math.Clamp(preferred, earliest, Math.Max(earliest, latest));

            // Split only where either timeline changes slope. This also includes the beat approaching a timing reset,
            // allowing its phase correction to happen over that beat rather than as a jump in the audio position.
            var boundaries = new SortedSet<double> { 0, songLength };
            foreach (var point in source.Points)
                addBoundary(point.Time);
            foreach (var point in target.Points)
                addBoundary(source.TimeAt((point.Beat - offset) / factor));

            var sections = new List<Section>();
            double[] times = boundaries.ToArray();
            for (int i = 1; i < times.Length; i++)
            {
                double start = times[i - 1];
                double end = times[i];
                double mapStart = target.TimeAt(source.BeatAt(start) * factor + offset);
                double mapEnd = target.TimeAt(source.BeatAt(end) * factor + offset);
                double stretch = (end - start) / (mapEnd - mapStart);
                if (!double.IsFinite(stretch) || stretch < 0.05 || stretch > 51)
                    throw new InvalidOperationException("These timing changes need an unsupported playback speed. Pick another song.");

                if (sections.Count > 0 && Math.Abs(sections[^1].Stretch - stretch) < 1e-9)
                    sections[^1] = sections[^1] with { SongEnd = end };
                else
                    sections.Add(new Section(start, end, mapStart, stretch));
            }
            return new Plan(sections);

            void addBoundary(double time)
            {
                if (time > 1e-7 && time < songLength - 1e-7)
                    boundaries.Add(time);
            }
        }

        /// <summary>
        /// A continuous musical beat clock. Red timing points establish a fresh beat grid, not just a new BPM.
        /// A reset slightly off the previous grid adjusts the last beat's duration instead of carrying its phase error forward.
        /// </summary>
        private sealed class BeatTimeline
        {
            public record Point(double Time, double Beat);

            public readonly List<Point> Points = new List<Point>();
            private readonly double firstBeatLength;
            private readonly double lastBeatLength;

            public BeatTimeline(IReadOnlyList<TimingControlPoint> timings)
            {
                if (timings.Count == 0)
                    timings = new[] { TimingControlPoint.DEFAULT };
                firstBeatLength = timings[0].BeatLength;
                lastBeatLength = timings[^1].BeatLength;
                Points.Add(new Point(timings[0].Time, 0));

                double beat = 0;
                for (int i = 1; i < timings.Count; i++)
                {
                    var previous = timings[i - 1];
                    var next = timings[i];
                    double distance = (next.Time - previous.Time) / previous.BeatLength;
                    double beats = Math.Round(distance);
                    // Sub-beat timing corrections must not manufacture a whole additional beat.
                    if (beats < 1)
                        beats = distance;
                    if (beats <= 0)
                        continue;
                    if (beats > 1)
                        Points.Add(new Point(previous.Time + (beats - 1) * previous.BeatLength, beat + beats - 1));
                    beat += beats;
                    Points.Add(new Point(next.Time, beat));
                }
            }

            public double BeatAt(double time) => convert(time, true);
            public double TimeAt(double beat) => convert(beat, false);

            private double convert(double value, bool fromTime)
            {
                double x(Point p) => fromTime ? p.Time : p.Beat;
                double y(Point p) => fromTime ? p.Beat : p.Time;
                if (value <= x(Points[0]))
                    return y(Points[0]) + (value - x(Points[0])) * (fromTime ? 1 / firstBeatLength : firstBeatLength);
                if (value >= x(Points[^1]))
                    return y(Points[^1]) + (value - x(Points[^1])) * (fromTime ? 1 / lastBeatLength : lastBeatLength);

                int lower = 0;
                int upper = Points.Count - 1;
                while (upper - lower > 1)
                {
                    int middle = (lower + upper) / 2;
                    if (x(Points[middle]) <= value)
                        lower = middle;
                    else
                        upper = middle;
                }
                return y(Points[lower]) + (value - x(Points[lower])) / (x(Points[upper]) - x(Points[lower])) * (y(Points[upper]) - y(Points[lower]));
            }
        }
    }
}

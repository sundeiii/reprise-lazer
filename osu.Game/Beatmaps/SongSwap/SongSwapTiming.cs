// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps.ControlPoints;

namespace osu.Game.Beatmaps.SongSwap
{
    /// <summary>
    /// Matches musical beat positions across both timelines without changing the map's timing data: the song's best part on the map's
    /// (their kiai, else their preview points, else their first notes), every beat on the map's beats, through tempo changes on either side,
    /// and, when the song's too short, a bar-aligned part of it looped to the end.
    /// </summary>
    public static class SongSwapTiming
    {
        /// <summary>How long (ms) the crossfade into a loop's seam is; it ends on the seam's downbeat, which is kept sharp.</summary>
        public const double SEAM_JOIN = 60;

        /// <summary>How long (ms) the crossfade between two tempo sections of continuous audio is.</summary>
        public const double TEMPO_JOIN = 10;

        /// <summary>A stretch of the song (ms) played at one speed from a point of the map (ms).</summary>
        /// <param name="Join">How long (ms) before <paramref name="MapStart"/> it fades in over the previous section.</param>
        /// <param name="Seam">Whether it jumps in the song (a loop) rather than carrying on from the previous section.</param>
        public record Section(double SongStart, double SongEnd, double MapStart, double Stretch, double Join = 0, bool Seam = false)
        {
            public double MapEnd => MapStart + (SongEnd - SongStart) / Stretch;
        }

        /// <summary>How the song's lined up on the map.</summary>
        public enum Anchor
        {
            /// <summary>The song's kiai on the map's (its chorus on the map's).</summary>
            Kiai,

            /// <summary>Their preview points (their highlights, as song select plays them).</summary>
            Preview,

            /// <summary>Their first main-tempo notes (as SongSwapper lines songs up).</summary>
            FirstNotes,
        }

        public record Plan(IReadOnlyList<Section> Sections, Anchor By = Anchor.FirstNotes, int Loops = 0)
        {
            public double Start => Sections[0].MapStart;
            public double End => Sections[^1].MapEnd;

            /// <summary>The same plan, all of it later on the map by <paramref name="ms"/> (earlier, if negative).</summary>
            public Plan Shifted(double ms) => this with { Sections = Sections.Select(s => s with { MapStart = s.MapStart + ms }).ToList() };

            /// <summary>
            /// The same plan with sections that carry straight on from each other (the same song, speed and place on the map) made one: it's
            /// rendered as one stream, without a join (a join between two separately stretched streams can be heard).
            /// </summary>
            public Plan Joined()
            {
                var joined = new List<Section>();

                foreach (var section in Sections)
                {
                    if (joined.Count > 0 && !section.Seam
                                         && Math.Abs(section.SongStart - joined[^1].SongEnd) < 1e-6
                                         && Math.Abs(section.Stretch - joined[^1].Stretch) < 1e-9
                                         && Math.Abs(section.MapStart - joined[^1].MapEnd) < 1e-6)
                        joined[^1] = joined[^1] with { SongEnd = section.SongEnd };
                    else
                        joined.Add(section);
                }

                return this with { Sections = joined };
            }

            /// <summary>Whether another plan places the song the same way.</summary>
            public bool SameAs(Plan other) =>
                Sections.Count == other.Sections.Count
                && Sections.Zip(other.Sections).All(p => Math.Abs(p.First.MapStart - p.Second.MapStart) < 1e-6
                                                         && Math.Abs(p.First.SongStart - p.Second.SongStart) < 1e-6
                                                         && Math.Abs(p.First.SongEnd - p.Second.SongEnd) < 1e-6);
        }

        /// <summary>
        /// A map's main beat length: the one most of its notes are played at (as SongSwapper picks it; the faster on a tie), so a long intro
        /// or outro with few notes doesn't count for more than where the map's actually played.
        /// </summary>
        public static double MainBeatLength(IBeatmap beatmap)
        {
            var mostNotes = beatmap.HitObjects.Where(h => !SongSwapping.IsSpinner(h))
                                   .GroupBy(h => Math.Round(beatmap.ControlPointInfo.TimingPointAt(h.StartTime).BeatLength, 3))
                                   .Where(g => g.Key > 0)
                                   .OrderByDescending(g => g.Count())
                                   .ThenBy(g => g.Key)
                                   .FirstOrDefault();

            return mostNotes != null ? beatmap.ControlPointInfo.TimingPointAt(mostNotes.First().StartTime).BeatLength : beatmap.GetMostCommonBeatLength();
        }

        /// <summary>The first playable note in the main BPM section, falling back to the first non-spinner.</summary>
        public static double SyncTime(IBeatmap beatmap)
        {
            double mainBeatLength = MainBeatLength(beatmap);
            return beatmap.HitObjects.FirstOrDefault(h => !SongSwapping.IsSpinner(h)
                                                        && Math.Abs(beatmap.ControlPointInfo.TimingPointAt(h.StartTime).BeatLength - mainBeatLength) < 0.01)?.StartTime
                   ?? SongSwapping.FirstObjectTime(beatmap);
        }

        /// <summary>When a map's chorus starts: its first kiai of at least 5 seconds (else its first kiai), if it has one.</summary>
        public static double? KiaiTime(IBeatmap beatmap)
        {
            var effects = beatmap.ControlPointInfo.EffectPoints;
            double? first = null;

            for (int i = 0; i < effects.Count; i++)
            {
                if (!effects[i].KiaiMode || (i > 0 && effects[i - 1].KiaiMode))
                    continue;

                double start = effects[i].Time;
                double end = effects.Skip(i + 1).FirstOrDefault(e => !e.KiaiMode)?.Time ?? double.PositiveInfinity;
                first ??= start;
                if (end - start >= 5000)
                    return start;
            }

            return first;
        }

        /// <summary>A map's preview point, if it has one.</summary>
        public static double? PreviewTime(IBeatmap beatmap) => beatmap.Metadata.PreviewTime > 0 ? beatmap.Metadata.PreviewTime : null;

        /// <summary>The moment of the map its best part's heard from (for previews): its kiai, else its preview point, else its first note.</summary>
        public static double HighlightTime(IBeatmap beatmap) => KiaiTime(beatmap) ?? PreviewTime(beatmap) ?? SyncTime(beatmap);

        /// <summary>
        /// Plans the song on the map. Pulse interpretation is chosen once, so a tempo change cannot unexpectedly flip a song between
        /// half and double time.
        /// </summary>
        /// <param name="loop">Whether a song too short for the map may be looped (else it's refused).</param>
        /// <exception cref="InvalidOperationException">The song can't cover the map (and isn't to be looped), or needs an unsupported speed.</exception>
        public static Plan Create(IBeatmap map, IBeatmap song, double songLength, bool loop = false)
        {
            if (!double.IsFinite(songLength) || songLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(songLength));

            var target = new BeatTimeline(map.ControlPointInfo.TimingPoints);
            var source = new BeatTimeline(song.ControlPointInfo.TimingPoints);
            var fit = SongSwapping.Fit(60000 / MainBeatLength(map), 60000 / MainBeatLength(song))
                      ?? throw new InvalidOperationException("The song has unsupported timing data.");
            double factor = fit.Factor;
            double playStart = map.HitObjects.Count == 0 ? 0 : map.HitObjects.Min(h => h.StartTime);
            double playEnd = Math.Max(playStart + 1, SongSwapping.EndTime(map));

            double sourceFirst = source.BeatAt(0);
            double sourceLast = source.BeatAt(songLength);

            // Offsets (map beat = factor * song beat + offset) that start the song by the map's first note / last it to the map's end.
            double latest = target.BeatAt(playStart) - factor * sourceFirst;
            double earliest = target.BeatAt(playEnd) - factor * sourceLast;

            // A bar of the map (its meter where it's lined up), for moving the song without breaking its phrases.
            double bar = Math.Max(1, map.ControlPointInfo.TimingPointAt(SyncTime(map)).TimeSignature.Numerator);

            var anchors = new List<(Anchor by, double offset)>();
            addAnchor(Anchor.Kiai, KiaiTime(map), KiaiTime(song));
            addAnchor(Anchor.Preview, PreviewTime(map), PreviewTime(song));
            addAnchor(Anchor.FirstNotes, SyncTime(map), SyncTime(song));

            Anchor by;
            double offset;
            bool looping = false;
            int exact = anchors.FindIndex(a => fits(a.offset));

            if (exact >= 0)
                (by, offset) = anchors[exact];
            else if (earliest <= latest + 1e-7)
            {
                // Long enough, but not where it'd be best: moved by whole bars (its phrases kept on the map's), else whole beats, else as
                // little as covering the map takes (a tight fit can need a fractional offset).
                var shifted = anchors.Select(a => (a.by, preferred: a.offset, offset: shiftedIntoRange(a.offset, bar)))
                                     .Where(a => a.offset != null)
                                     .OrderBy(a => Math.Abs(a.offset!.Value - a.preferred))
                                     .ThenBy(a => a.by)
                                     .ToList();

                if (shifted.Count > 0)
                    (by, offset) = (shifted[0].by, shifted[0].offset!.Value);
                else
                {
                    by = anchors[0].by;
                    double firstBeat = Math.Ceiling(earliest - 1e-7);
                    double lastBeat = Math.Floor(latest + 1e-7);
                    offset = firstBeat <= lastBeat ? Math.Clamp(anchors[0].offset, firstBeat, lastBeat) : Math.Clamp(anchors[0].offset, earliest, Math.Max(earliest, latest));
                }
            }
            else
            {
                if (!loop)
                    throw new InvalidOperationException("The replacement song is too short after matching the map's tempo. Pick a longer song, or turn on looping.");

                // Its best part where it's best; only moved (by whole bars) if it'd start after the map's first note. The end's looped.
                (by, offset) = anchors[0];
                if (offset > latest + 1e-7)
                    offset -= Math.Ceiling((offset - latest - 1e-7) / bar) * bar;
                looping = true;
            }

            var pieces = new List<(double from, double to, double mapBeat, bool seam)>();
            int loops = 0;

            if (!looping)
                pieces.Add((sourceFirst, sourceLast, factor * sourceFirst + offset, false));
            else
            {
                var (loopStart, loopEnd) = loopRange(song, source, songLength);
                pieces.Add((sourceFirst, loopEnd, factor * sourceFirst + offset, false));

                // Looped until a last pass, played out to the song's own ending, carries past the map's end.
                double outro = playEnd + SongSwapping.TAIL;

                while (true)
                {
                    double at = pieces[^1].mapBeat + factor * (pieces[^1].to - pieces[^1].from);

                    if (target.TimeAt(at + factor * (sourceLast - loopStart)) >= outro || loops >= 500)
                    {
                        pieces.Add((loopStart, sourceLast, at, true));
                        break;
                    }

                    pieces.Add((loopStart, loopEnd, at, true));
                    loops++;
                }
            }

            var sections = new List<Section>();
            foreach (var piece in pieces)
                addPiece(piece.from, piece.to, piece.mapBeat, piece.seam);

            return new Plan(sections, by, loops);

            void addAnchor(Anchor anchor, double? mapTime, double? songTime)
            {
                if (mapTime == null || songTime == null)
                    return;

                // A whole-beat offset aligns the pulse, including when the anchors sit between beats.
                anchors.Add((anchor, Math.Round(target.BeatAt(mapTime.Value) - factor * source.BeatAt(Math.Clamp(songTime.Value, 0, songLength)))));
            }

            bool fits(double o) => o >= earliest - 1e-7 && o <= latest + 1e-7;

            double? shiftedIntoRange(double o, double step)
            {
                if (o > latest)
                    o -= Math.Ceiling((o - latest - 1e-7) / step) * step;
                else if (o < earliest)
                    o += Math.Ceiling((earliest - o - 1e-7) / step) * step;
                return fits(o) ? o : null;
            }

            // Split only where either timeline changes slope. This also includes the beat approaching a timing reset,
            // allowing its phase correction to happen over that beat rather than as a jump in the audio position.
            void addPiece(double fromBeat, double toBeat, double mapBeat, bool seam)
            {
                double start = source.TimeAt(fromBeat);
                double end = source.TimeAt(toBeat);
                var boundaries = new SortedSet<double> { start, end };
                foreach (var point in source.Points)
                    addBoundary(point.Time);
                foreach (var point in target.Points)
                    addBoundary(source.TimeAt(fromBeat + (point.Beat - mapBeat) / factor));

                double[] times = boundaries.ToArray();
                bool first = true;

                for (int i = 1; i < times.Length; i++)
                {
                    double a = times[i - 1];
                    double b = times[i];
                    double mapA = target.TimeAt(mapBeat + factor * (source.BeatAt(a) - fromBeat));
                    double mapB = target.TimeAt(mapBeat + factor * (source.BeatAt(b) - fromBeat));
                    double stretch = (b - a) / (mapB - mapA);
                    if (!double.IsFinite(stretch) || stretch < 0.05 || stretch > 51)
                        throw new InvalidOperationException("These timing changes need an unsupported playback speed. Pick another song.");

                    // Kept apart at the song's own timing points even at the same speed: each can be off from the music by its own amount,
                    // which matching the map's feel corrects section by section (see SongSwapAnalysis.MatchFeel).
                    bool songTimingPoint = song.ControlPointInfo.TimingPoints.Any(t => Math.Abs(t.Time - a) < 0.5);

                    if (!first && !songTimingPoint && Math.Abs(sections[^1].Stretch - stretch) < 1e-9)
                        sections[^1] = sections[^1] with { SongEnd = b };
                    else
                    {
                        double join = sections.Count == 0 ? 0 : first && seam ? SEAM_JOIN : TEMPO_JOIN;
                        sections.Add(new Section(a, b, mapA, stretch, join, first && seam));
                    }

                    first = false;
                }

                void addBoundary(double time)
                {
                    if (time > start + 1e-7 && time < end - 1e-7)
                        boundaries.Add(time);
                }
            }
        }

        /// <summary>
        /// The part of a song that's looped (song beats): whole bars from its chorus (its kiai, if at least 8 bars of the song follow it) or
        /// its first main-tempo note, to the last whole bar before its last note.
        /// </summary>
        private static (double start, double end) loopRange(IBeatmap song, BeatTimeline source, double songLength)
        {
            double bar = Math.Max(1, song.ControlPointInfo.TimingPointAt(SyncTime(song)).TimeSignature.Numerator);
            double lastNote = SongSwapping.EndTime(song);
            double limit = source.BeatAt(Math.Min(songLength - 300, lastNote > 0 ? lastNote : songLength));
            // The renderer needs a little of the song before the loop's start for its crossfade.
            double minimum = source.BeatAt(Math.Min(songLength, 300));

            // Bars counted from the song's first timing point (its first downbeat).
            double startAt(double time)
            {
                double beat = Math.Round(source.BeatAt(time) / bar) * bar;
                while (beat < minimum)
                    beat += bar;
                return beat;
            }

            double start = startAt(SyncTime(song));
            if (KiaiTime(song) is double kiai && limit - startAt(kiai) >= bar * 8)
                start = startAt(kiai);

            double end = start + Math.Floor((limit - start + 1e-7) / bar) * bar;
            if (end - start < bar * 2)
                throw new InvalidOperationException("The replacement song is too short to loop. Pick a longer song.");

            return (start, end);
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

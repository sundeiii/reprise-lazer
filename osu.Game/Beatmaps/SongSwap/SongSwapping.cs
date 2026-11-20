// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ManagedBass;
using ManagedBass.Fx;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Beatmaps.SongSwap
{
    /// <summary>
    /// Reprise: a beatmap's song swapped for another's (as SlowDogged's SongSwapper does it): another song of the same, half or double
    /// BPM, stretched to this map's BPM (its pitch kept) and lined up with it, put in place of the map's own song. Only the audio changes,
    /// not the map, so plays on it count as ever.
    /// </summary>
    public static class SongSwapping
    {
        /// <summary>How far a song's BPM may be stretched (either way) to fit.</summary>
        public const double MAX_STRETCH = 0.25;

        /// <summary>
        /// How another song's BPM fits this map's: the factor its BPM is taken at (1, or ½ / 2 for half or double time) and how much it's
        /// stretched by then (this map's BPM over that); null if it doesn't fit within <see cref="MAX_STRETCH"/>.
        /// </summary>
        public static (double Factor, double Stretch)? Fit(double mapBpm, double songBpm)
        {
            if (mapBpm <= 0 || songBpm <= 0)
                return null;

            var best = new[] { 1, 0.5, 2 }
                       .Select(f => (Factor: f, Stretch: mapBpm / (songBpm * f)))
                       .OrderBy(f => Math.Abs(Math.Log(f.Stretch)))
                       .First();

            return Math.Abs(best.Stretch - 1) <= MAX_STRETCH ? best : null;
        }

        /// <summary>When a beatmap's play starts: its first object that isn't a spinner (as SongSwapper lines songs up), else its first.</summary>
        public static double FirstObjectTime(IBeatmap beatmap)
        {
            var objects = beatmap.HitObjects;

            if (objects.Count == 0)
                return 0;

            return (objects.FirstOrDefault(h => !isSpinner(h)) ?? objects[0]).StartTime;
        }

        // A spinner (osu!'s, taiko's swell, catch's banana shower): not where the music's beat is felt.
        private static bool isSpinner(HitObject h) => h.GetType().Name is "Spinner" or "Swell" or "BananaShower";

        /// <summary>A way of lining the song up: a moment of the song's put on a moment of the map's.</summary>
        public enum Anchor
        {
            /// <summary>Their kiai (the song's highlight on the map's).</summary>
            Kiai,

            /// <summary>Their preview points (where song select plays them from: their best part).</summary>
            Preview,

            /// <summary>Their first notes (as SongSwapper lines songs up).</summary>
            FirstObject,
        }

        /// <summary>How the swapped song's placed: where it starts in the map's time, by what, and how much of the map's play it covers.</summary>
        public record Placement(double Offset, Anchor By, double Coverage);

        /// <summary>When a map's first kiai starts, if it has one.</summary>
        public static double? FirstKiai(IBeatmap beatmap) =>
            beatmap.ControlPointInfo.EffectPoints.FirstOrDefault(e => e.KiaiMode)?.Time;

        /// <summary>A map's preview point, if it has one.</summary>
        public static double? PreviewTime(IBeatmap beatmap) =>
            beatmap.Metadata.PreviewTime > 0 ? beatmap.Metadata.PreviewTime : null;

        /// <summary>When a map's play ends: its last object's end.</summary>
        public static double EndTime(IBeatmap beatmap) => beatmap.HitObjects.Count == 0 ? 0 : beatmap.HitObjects.Max(h => h.GetEndTime());

        /// <summary>Whether a song (its length, unstretched) stretched by <paramref name="stretch"/> lasts the whole of a map's play.</summary>
        public static bool LongEnough(double songLength, double stretch, double mapPlayLength) => songLength / stretch >= mapPlayLength;

        /// <summary>
        /// The best way of placing the song on the map (its "optimal part"), always covering the whole of the map's play (its first note
        /// to its last: the music never stops while playing): by their kiai, their preview points or their first notes, in that order of
        /// preference, the first of them that covers it as it is; else the first moved as little as covering it takes. Then moved to
        /// where the song's beats land on the map's (keeping it covered).
        /// </summary>
        /// <param name="map">This map (its timing, kiai, preview point and notes).</param>
        /// <param name="song">The song's map.</param>
        /// <param name="stretch">How much the song's stretched by (see <see cref="Fit"/>).</param>
        /// <param name="songLength">How long the song is (ms, unstretched).</param>
        /// <exception cref="InvalidOperationException">The song (stretched) is shorter than the map's play.</exception>
        public static Placement Place(IBeatmap map, IBeatmap song, double stretch, double songLength)
        {
            double playStart = map.HitObjects.Count == 0 ? 0 : map.HitObjects.Min(h => h.StartTime);
            double playEnd = Math.Max(playStart + 1, EndTime(map));
            double stretchedLength = songLength / stretch;

            if (stretchedLength < playEnd - playStart)
                throw new InvalidOperationException("The song is shorter than the map: it would stop while playing.");

            // The offsets that cover the whole play: the song starting by the first note and going on past the last.
            double earliest = playEnd - stretchedLength;
            double latest = playStart;

            var options = new[]
            {
                (by: Anchor.Kiai, map: FirstKiai(map), song: FirstKiai(song)),
                (by: Anchor.Preview, map: PreviewTime(map), song: PreviewTime(song)),
                (by: Anchor.FirstObject, map: (double?)FirstObjectTime(map), song: (double?)FirstObjectTime(song)),
            }.Where(o => o.map != null && o.song != null).ToList();

            var chosen = options.FirstOrDefault(o => isWithin(o.map!.Value - o.song!.Value / stretch, earliest, latest));

            if (chosen == default)
                chosen = options.OrderBy(o => distanceOutside(o.map!.Value - o.song!.Value / stretch, earliest, latest)).First();

            double raw = Math.Clamp(chosen.map!.Value - chosen.song!.Value / stretch, earliest, latest);
            double snapped = snapToBeat(map, song, stretch, raw, chosen.map.Value);

            // On the beat if that still covers it; else the nearest beat the other way (a beat either side), else where it was.
            double beat = map.ControlPointInfo.TimingPointAt(chosen.map.Value).BeatLength;
            double offset = new[] { snapped, snapped - beat, snapped + beat, raw }.First(o => isWithin(o, earliest, latest));

            return new Placement(offset, chosen.by, 1);
        }

        private static bool isWithin(double offset, double earliest, double latest) => offset >= earliest && offset <= latest;

        private static double distanceOutside(double offset, double earliest, double latest) =>
            offset < earliest ? earliest - offset : offset > latest ? offset - latest : 0;

        /// <summary>
        /// Where the swapped song starts in this map's time (ms; above 0, after a silence, below 0, cut short at its start) lining up their
        /// first notes, on the beat (see <see cref="Place"/> for the best place).
        /// </summary>
        public static double Offset(IBeatmap map, IBeatmap song, double stretch) =>
            snapToBeat(map, song, stretch, FirstObjectTime(map) - FirstObjectTime(song) / stretch, FirstObjectTime(map));

        // The offset moved so the song's beat (stretched) lands on the map's, around the given moment of the map.
        private static double snapToBeat(IBeatmap map, IBeatmap song, double stretch, double offset, double at)
        {
            var mapTiming = map.ControlPointInfo.TimingPointAt(at);
            var songTiming = song.ControlPointInfo.TimingPointAt((at - offset) * stretch);

            double beat = mapTiming.BeatLength;

            if (beat <= 0 || double.IsNaN(beat) || double.IsInfinity(beat))
                return offset;

            double songBeatAt = songTiming.Time / stretch + offset;
            double apart = (songBeatAt - mapTiming.Time) / beat;

            return offset - (apart - Math.Round(apart)) * beat;
        }

        /// <summary>How long a song is (ms), as BASS reads it (which must be set up).</summary>
        public static double LengthOf(byte[] song)
        {
            int decode = Bass.CreateStream(song, 0, song.Length, BassFlags.Decode | BassFlags.Prescan);

            if (decode == 0)
                throw new InvalidOperationException($"The song couldn't be read ({Bass.LastError}).");

            try
            {
                return Bass.ChannelBytes2Seconds(decode, Bass.ChannelGetLength(decode)) * 1000;
            }
            finally
            {
                Bass.StreamFree(decode);
            }
        }

        /// <summary>How long after the map's play ends the song goes on (full), before fading out.</summary>
        public const double TAIL = 2000;

        /// <summary>How long a song longer than the map takes to fade out (after <see cref="TAIL"/>).</summary>
        public const double FADE_OUT = 3000;

        /// <summary>How long a song cut into (started part of the way in) takes to fade in.</summary>
        public const double FADE_IN = 800;

        /// <summary>
        /// The song stretched (its pitch kept) and placed (see <see cref="Place"/>), as a 16-bit wave file: faded in if it's cut into, faded
        /// out after the map's play ends (<paramref name="mapEnd"/>, plus <see cref="TAIL"/>) if it goes on longer, or followed by silence if
        /// it's shorter (so the map never outlasts its song). Uses BASS (the game's audio), which must be set up.
        /// </summary>
        public static byte[] Render(byte[] song, double stretch, double offset, double mapEnd)
        {
            int decode = Bass.CreateStream(song, 0, song.Length, BassFlags.Decode | BassFlags.Prescan | BassFlags.Float);

            if (decode == 0)
                throw new InvalidOperationException($"The song couldn't be read ({Bass.LastError}).");

            int tempo = BassFx.TempoCreate(decode, BassFlags.Decode | BassFlags.FxFreeSource | BassFlags.Float);

            if (tempo == 0)
            {
                Bass.StreamFree(decode);
                throw new InvalidOperationException($"The song couldn't be stretched ({Bass.LastError}).");
            }

            try
            {
                Bass.ChannelSetAttribute(tempo, ChannelAttribute.Tempo, (stretch - 1) * 100);

                var info = Bass.ChannelGetInfo(tempo);
                int channels = info.Channels;
                int rate = info.Frequency;

                long samplesAt(double ms) => (long)Math.Round(ms / 1000 * rate) * channels;

                long fadeOutStart = samplesAt(mapEnd + TAIL);
                long end = samplesAt(mapEnd + TAIL + FADE_OUT);
                long fadeOutLength = Math.Max(1, end - fadeOutStart);
                long fadeInLength = offset < 0 ? Math.Max(1, samplesAt(FADE_IN)) : 0;

                using var output = new MemoryStream();
                using var writer = new BinaryWriter(output);

                writeWaveHeader(writer, rate, channels, 0);

                long written = 0;
                long songWritten = 0;

                // Silence before it, or its start skipped (and what's left of it faded in).
                long samplesBefore = samplesAt(offset);
                long skip = 0;

                if (samplesBefore > 0)
                    written += writeSilence(writer, samplesBefore);
                else
                    skip = -samplesBefore;

                float[] buffer = new float[16384];
                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                try
                {
                    while (written < end)
                    {
                        int bytes = Bass.ChannelGetData(tempo, handle.AddrOfPinnedObject(), buffer.Length * sizeof(float) | (int)DataFlags.Float);

                        if (bytes <= 0)
                            break;

                        int count = bytes / sizeof(float);
                        int start = 0;

                        if (skip > 0)
                        {
                            int skipped = (int)Math.Min(skip, count);
                            skip -= skipped;
                            start = skipped;
                        }

                        for (int i = start; i < count && written < end; i++)
                        {
                            float gain = 1;

                            if (songWritten < fadeInLength)
                                gain *= (float)songWritten / fadeInLength;

                            if (written >= fadeOutStart)
                                gain *= 1 - (float)(written - fadeOutStart) / fadeOutLength;

                            writer.Write((short)Math.Round(Math.Clamp(buffer[i] * gain, -1f, 1f) * short.MaxValue));
                            written++;
                            songWritten++;
                        }
                    }
                }
                finally
                {
                    handle.Free();
                }

                // Silence after it (a song shorter than the map), as long as the map needs.
                long minimum = samplesAt(mapEnd + TAIL);

                if (written < minimum)
                    written += writeSilence(writer, minimum - written);

                writer.Flush();

                byte[] wave = output.ToArray();
                fixWaveSizes(wave, written * sizeof(short));
                return wave;
            }
            finally
            {
                Bass.StreamFree(tempo);
            }
        }

        private static long writeSilence(BinaryWriter writer, long samples)
        {
            for (long i = 0; i < samples; i++)
                writer.Write((short)0);

            return samples;
        }

        /// <summary>A 16-bit PCM wave file's header (its sizes filled in after, see <see cref="fixWaveSizes"/>).</summary>
        private static void writeWaveHeader(BinaryWriter writer, int rate, int channels, int dataBytes)
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(rate);
            writer.Write(rate * channels * sizeof(short));
            writer.Write((short)(channels * sizeof(short)));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
        }

        private static void fixWaveSizes(byte[] wave, long dataBytes)
        {
            BitConverter.TryWriteBytes(wave.AsSpan(4), (int)(36 + dataBytes));
            BitConverter.TryWriteBytes(wave.AsSpan(40), (int)dataBytes);
        }
    }
}

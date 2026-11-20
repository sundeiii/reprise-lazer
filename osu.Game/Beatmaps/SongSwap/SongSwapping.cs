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

        /// <summary>
        /// Where the swapped song starts in this map's time (ms; above 0, after a silence, below 0, cut short at its start): its first
        /// object on this map's, then moved to the nearest place its beats land on this map's beats.
        /// </summary>
        /// <param name="map">This map (its timing and first object).</param>
        /// <param name="song">The song's map (its timing and first object).</param>
        /// <param name="stretch">How much the song's stretched by (see <see cref="Fit"/>).</param>
        public static double Offset(IBeatmap map, IBeatmap song, double stretch)
        {
            double offset = FirstObjectTime(map) - FirstObjectTime(song) / stretch;

            // On the beat: this map's beat and the song's (stretched) at the first object, a whole number of this map's beats apart.
            var mapTiming = map.ControlPointInfo.TimingPointAt(FirstObjectTime(map));
            var songTiming = song.ControlPointInfo.TimingPointAt(FirstObjectTime(song));

            double beat = mapTiming.BeatLength;

            if (beat <= 0 || double.IsNaN(beat) || double.IsInfinity(beat))
                return offset;

            double songBeatAt = songTiming.Time / stretch + offset;
            double apart = (songBeatAt - mapTiming.Time) / beat;

            return offset - (apart - Math.Round(apart)) * beat;
        }

        /// <summary>
        /// The song stretched (its pitch kept) and placed (see <see cref="Offset"/>), as a 16-bit wave file at least <paramref name="minimumLength"/>
        /// long (silence after it, so the map never outlasts its song). Uses BASS (the game's audio), which must be set up.
        /// </summary>
        public static byte[] Render(byte[] song, double stretch, double offset, double minimumLength)
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

                using var output = new MemoryStream();
                using var writer = new BinaryWriter(output);

                writeWaveHeader(writer, rate, channels, 0);

                long written = 0;

                // Silence before it, or its start skipped.
                long samplesBefore = (long)Math.Round(offset / 1000 * rate) * channels;
                long skip = 0;

                if (samplesBefore > 0)
                    written += writeSilence(writer, samplesBefore);
                else
                    skip = -samplesBefore;

                float[] buffer = new float[16384];
                var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                try
                {
                    while (true)
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

                        for (int i = start; i < count; i++)
                            writer.Write((short)Math.Round(Math.Clamp(buffer[i], -1f, 1f) * short.MaxValue));

                        written += count - start;
                    }
                }
                finally
                {
                    handle.Free();
                }

                // Silence after it, as long as the map needs.
                long minimum = (long)Math.Ceiling(minimumLength / 1000 * rate) * channels;

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

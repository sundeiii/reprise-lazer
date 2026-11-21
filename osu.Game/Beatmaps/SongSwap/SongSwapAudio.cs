// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ManagedBass;
using ManagedBass.Fx;

namespace osu.Game.Beatmaps.SongSwap
{
    /// <summary>How a song's rendered: its pitch, the part of it made (all of it, or a preview's window), and ticks on the map's notes.</summary>
    /// <param name="KeepPitch">Whether a stretched song keeps its pitch (else it's sped up or slowed down like a record).</param>
    /// <param name="FirstNote">The map's first note (ms): a song cut into fades in by then (else over <see cref="SongSwapping.FADE_IN"/>).</param>
    /// <param name="From">The map's time (ms) the rendering starts at (else 0).</param>
    /// <param name="To">The map's time (ms) the rendering ends at (else where the song's faded out).</param>
    /// <param name="Ticks">The map's notes (ms), ticked over the song (for hearing a preview's sync).</param>
    public record SongSwapRenderOptions(bool KeepPitch = true, double? FirstNote = null, double? From = null, double? To = null, IReadOnlyList<double>? Ticks = null);

    /// <summary>Offline rendering of a timing plan. No beatmap files or timing points are changed.</summary>
    internal static class SongSwapAudio
    {
        // How much of the song (ms, as output) the stretcher is given before and after the part kept.
        private const double context = 250;

        public static byte[] Render(byte[] song, SongSwapTiming.Plan plan, double mapEnd, SongSwapRenderOptions options)
        {
            int decode = Bass.CreateStream(song, 0, song.Length, BassFlags.Decode | BassFlags.Prescan | BassFlags.Float);
            if (decode == 0)
                throw new InvalidOperationException($"The song couldn't be read ({Bass.LastError}).");

            try
            {
                var info = Bass.ChannelGetInfo(decode);
                int channels = info.Channels;
                int rate = info.Frequency;
                double sourceLength = Bass.ChannelBytes2Seconds(decode, Bass.ChannelGetLength(decode)) * 1000;
                long frameAt(double ms) => (long)Math.Round(ms * rate / 1000);

                // The tail may end naturally before the fade. Silence is only allowed after the last playable object.
                double outputEnd = Math.Max(mapEnd + SongSwapping.TAIL, Math.Min(plan.End, mapEnd + SongSwapping.TAIL + SongSwapping.FADE_OUT));
                double from = Math.Clamp(options.From ?? 0, 0, outputEnd);
                double to = Math.Clamp(options.To ?? outputEnd, from, outputEnd);
                long baseFrame = frameAt(from);
                var output = new float[checked((int)(Math.Max(0, frameAt(to) - baseFrame) * channels))];
                bool rendered = false;

                for (int index = 0; index < plan.Sections.Count; index++)
                {
                    var section = plan.Sections[index];

                    // Crossfaded over the end of the previous section (when there's one in this window), ending on its own start.
                    double join = rendered ? section.Join : 0;
                    double writeStart = Math.Max(section.MapStart - join, from);

                    // Played on until the next section takes over: a part moved a little later (to match the map's feel) leaves no silence.
                    double nextStart = index + 1 < plan.Sections.Count ? plan.Sections[index + 1].MapStart : section.MapEnd;
                    double writeEnd = Math.Min(Math.Max(section.MapEnd, nextStart), to);
                    if (writeEnd <= writeStart)
                        continue;

                    double songAt = section.SongStart + (writeStart - section.MapStart) * section.Stretch;
                    double sourceStart = Math.Clamp(songAt - context * section.Stretch, 0, sourceLength);
                    double sourceEnd = Math.Min(sourceLength, section.SongEnd + context * section.Stretch);
                    if (sourceEnd <= sourceStart)
                        continue;

                    // The stretcher's own delay is measured and taken off, so beats land where planned rather than early.
                    double skipMs = (songAt - sourceStart) / section.Stretch + Latency(rate, channels, section.Stretch, options.KeepPitch);
                    long writeFrame = frameAt(writeStart) - baseFrame;
                    if (skipMs < 0)
                    {
                        writeFrame += frameAt(-skipMs);
                        skipMs = 0;
                    }

                    long skip = frameAt(skipMs) * channels;
                    long position = writeFrame * channels;
                    long limit = Math.Min(output.Length, (frameAt(writeEnd) - baseFrame) * channels);
                    long fadeFrom = frameAt(section.MapStart - join) - baseFrame;
                    long fadeTo = frameAt(section.MapStart) - baseFrame;

                    long startByte = Bass.ChannelSeconds2Bytes(decode, sourceStart / 1000);
                    long remaining = Bass.ChannelSeconds2Bytes(decode, sourceEnd / 1000) - startByte;
                    if (!Bass.ChannelSetPosition(decode, startByte))
                        throw new InvalidOperationException($"The song couldn't be positioned ({Bass.LastError}).");

                    StreamProcedure read = (_, buffer, length, _) =>
                    {
                        int requested = (int)Math.Min(length, remaining);
                        int bytes = requested > 0 ? Bass.ChannelGetData(decode, buffer, requested) : 0;
                        if (bytes < 0)
                            bytes = 0;
                        remaining -= bytes;
                        return bytes | (remaining <= 0 || bytes == 0 ? (int)StreamProcedureType.End : 0);
                    };

                    int tempo = createTempo(read, rate, channels, section.Stretch, options.KeepPitch);

                    try
                    {
                        var buffer = new float[16384];
                        var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                        try
                        {
                            while (position < limit)
                            {
                                int bytes = Bass.ChannelGetData(tempo, pinned.AddrOfPinnedObject(), buffer.Length * sizeof(float));
                                if (bytes <= 0)
                                    break;

                                int count = bytes / sizeof(float);
                                int start = (int)Math.Min(skip, count);
                                skip -= start;

                                for (int sample = start; sample < count && position < limit; sample++, position++)
                                {
                                    long frame = position / channels;

                                    if (frame < fadeTo && fadeTo > fadeFrom)
                                    {
                                        double m = Math.Clamp((double)(frame - fadeFrom) / (fadeTo - fadeFrom), 0, 1);
                                        // A loop's seam joins different audio: an equal-power crossfade keeps its loudness even.
                                        (double previous, double next) = section.Seam ? (Math.Cos(m * Math.PI / 2), Math.Sin(m * Math.PI / 2)) : (1 - m, m);
                                        output[position] = (float)(output[position] * previous + buffer[sample] * next);
                                    }
                                    else
                                        output[position] = buffer[sample];
                                }
                            }

                            // Refuse truncated/undecodable audio instead of silently filling a playable section with zeros.
                            if (position < Math.Min(limit, (frameAt(mapEnd) - baseFrame) * channels) - channels * (long)rate / 100)
                                throw new InvalidOperationException("The replacement audio ended before the map. Pick a longer song.");
                        }
                        finally
                        {
                            pinned.Free();
                        }
                    }
                    finally
                    {
                        Bass.StreamFree(tempo);
                        GC.KeepAlive(read);
                    }

                    rendered = true;
                }

                // Faded in (when the song's cut into) by the first note, and out after the map's end, both on a smooth S-curve.
                double fadeIn = Math.Clamp(options.FirstNote ?? SongSwapping.FADE_IN, 100, SongSwapping.FADE_IN);
                bool cut = plan.Start < -1;
                double gainAt(double time)
                {
                    double gain = cut && time < fadeIn ? sCurve(time / fadeIn) : 1;
                    double fadeOut = (time - mapEnd - SongSwapping.TAIL) / SongSwapping.FADE_OUT;
                    return fadeOut > 0 ? gain * (1 - sCurve(Math.Min(1, fadeOut))) : gain;
                }

                var ticks = options.Ticks?.Where(t => t >= from - 50 && t < to).ToList();
                float music = ticks?.Count > 0 ? 0.85f : 1;

                for (long frame = 0; frame < output.Length / channels; frame++)
                {
                    float gain = (float)gainAt((baseFrame + frame) * 1000.0 / rate) * music;
                    for (int c = 0; c < channels; c++)
                        output[frame * channels + c] *= gain;
                }

                if (ticks != null)
                {
                    // A short, bright tick on each note, so a preview shows how the song sits on the map.
                    long tickFrames = frameAt(40);
                    foreach (double tick in ticks)
                    {
                        long at = frameAt(tick) - baseFrame;
                        for (long f = Math.Max(0, -at); f < tickFrames && at + f < output.Length / channels; f++)
                        {
                            float v = (float)(Math.Sin(2 * Math.PI * 1600 * f / rate) * Math.Exp(-f * 1000.0 / rate / 8) * 0.3);
                            for (int c = 0; c < channels; c++)
                                output[(at + f) * channels + c] += v;
                        }
                    }
                }

                return wave(output, rate, channels);
            }
            finally
            {
                Bass.StreamFree(decode);
            }
        }

        private static double sCurve(double x) => (1 - Math.Cos(Math.Clamp(x, 0, 1) * Math.PI)) / 2;

        // A tempo stream over the given source: its pitch kept (time-stretched), or changed with its speed (resampled).
        private static int createTempo(StreamProcedure read, int rate, int channels, double stretch, bool keepPitch)
        {
            int source = Bass.CreateStream(rate, channels, BassFlags.Decode | BassFlags.Float, read);
            if (source == 0)
                throw new InvalidOperationException($"The audio section couldn't be created ({Bass.LastError}).");

            int tempo = BassFx.TempoCreate(source, BassFlags.Decode | BassFlags.FxFreeSource | BassFlags.Float);
            if (tempo == 0)
            {
                Bass.StreamFree(source);
                throw new InvalidOperationException($"The song couldn't be stretched ({Bass.LastError}).");
            }

            if (keepPitch)
            {
                // Much shorter windows than the stretcher's own (automatic) ones: drums stay crisp (measured on real songs, their beats
                // stood out about 1.5x as clearly as at 40/10ms, and 2x as clearly as the automatic ones) and wobble less.
                Bass.ChannelSetAttribute(tempo, ChannelAttribute.TempoSequenceMilliseconds, 25);
                Bass.ChannelSetAttribute(tempo, ChannelAttribute.TempoSeekWindowMilliseconds, 6);
                Bass.ChannelSetAttribute(tempo, ChannelAttribute.TempoOverlapMilliseconds, 4);
            }

            bool set = keepPitch
                ? Bass.ChannelSetAttribute(tempo, ChannelAttribute.Tempo, (stretch - 1) * 100)
                : Bass.ChannelSetAttribute(tempo, ChannelAttribute.TempoFrequency, rate * stretch);

            if (!set)
            {
                Bass.StreamFree(tempo);
                throw new InvalidOperationException($"The song's tempo couldn't be matched ({Bass.LastError}).");
            }

            return tempo;
        }

        private static readonly ConcurrentDictionary<(int rate, int channels, long stretch, bool keepPitch), double> latencies = new();

        /// <summary>
        /// How late (ms; negative: early) the stretcher puts audio, at a speed: measured once per speed on a click track (the stretcher
        /// lags or leads by a few to tens of milliseconds, depending on the speed), so it can be taken off.
        /// </summary>
        public static double Latency(int rate, int channels, double stretch, bool keepPitch)
        {
            if (Math.Abs(stretch - 1) < 1e-9)
                return 0;

            return latencies.GetOrAdd((rate, channels, (long)Math.Round(stretch * 1e6), keepPitch), _ => measureLatency(rate, channels, stretch, keepPitch));
        }

        private static double measureLatency(int rate, int channels, double stretch, bool keepPitch)
        {
            const double first = 500;
            const double every = 290;
            const int clicks = 24;

            int frames = (int)((first + every * clicks + 500) * rate / 1000);
            var input = new float[frames * channels];

            for (int k = 0; k < clicks; k++)
            {
                int at = (int)Math.Round((first + k * every) * rate / 1000);
                for (int f = 0; f < rate * 3 / 1000; f++)
                {
                    float v = (float)(Math.Sin(2 * Math.PI * 2000 * f / rate) * 0.8);
                    for (int c = 0; c < channels; c++)
                        input[(at + f) * channels + c] = v;
                }
            }

            int offset = 0;
            StreamProcedure read = (_, buffer, length, _) =>
            {
                int count = Math.Min(length / sizeof(float), input.Length - offset);
                Marshal.Copy(input, offset, buffer, count);
                offset += count;
                return count * sizeof(float) | (offset >= input.Length ? (int)StreamProcedureType.End : 0);
            };

            int tempo = createTempo(read, rate, channels, stretch, keepPitch);
            var errors = new List<double>();

            try
            {
                var buffer = new float[16384];
                var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                long frame = 0;
                long lastLoud = -rate;

                try
                {
                    int bytes;
                    while ((bytes = Bass.ChannelGetData(tempo, pinned.AddrOfPinnedObject(), buffer.Length * sizeof(float))) > 0)
                    {
                        for (int sample = 0; sample < bytes / sizeof(float); sample += channels, frame++)
                        {
                            if (Math.Abs(buffer[sample]) < 0.25)
                                continue;

                            if (frame - lastLoud > rate / 10)
                            {
                                double time = frame * 1000.0 / rate;
                                double k = Math.Round((time * stretch - first) / every);
                                double error = time - (first + k * every) / stretch;
                                if (k >= 0 && k < clicks && Math.Abs(error) < every / 3 / stretch)
                                    errors.Add(error);
                            }

                            lastLoud = frame;
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
                Bass.StreamFree(tempo);
                GC.KeepAlive(read);
            }

            if (errors.Count < clicks / 2)
                return 0;

            errors.Sort();
            return errors[errors.Count / 2];
        }

        private static byte[] wave(float[] samples, int rate, int channels)
        {
            using var result = new MemoryStream();
            using var writer = new BinaryWriter(result);
            int bytes = checked(samples.Length * sizeof(short));

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + bytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(rate);
            writer.Write(rate * channels * sizeof(short));
            writer.Write((short)(channels * sizeof(short)));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(bytes);

            foreach (float sample in samples)
                writer.Write((short)Math.Round(Math.Clamp(sample, -1, 1) * short.MaxValue));

            writer.Flush();
            return result.ToArray();
        }
    }
}

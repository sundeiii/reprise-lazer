// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ManagedBass;
using ManagedBass.Fx;

namespace osu.Game.Beatmaps.SongSwap
{
    /// <summary>Offline, pitch-preserving rendering of a timing plan. No beatmap files or timing points are changed.</summary>
    internal static class SongSwapAudio
    {
        private const double context = 150;
        private const double join = 10;

        public static byte[] Render(byte[] song, SongSwapTiming.Plan plan, double mapEnd)
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
                var output = new float[checked((int)(Math.Max(0, frameAt(outputEnd)) * channels))];

                for (int i = 0; i < plan.Sections.Count; i++)
                {
                    var section = plan.Sections[i];
                    if (section.MapEnd <= 0 || section.MapStart >= outputEnd)
                        continue;

                    // Give the stretcher context on both sides; only the planned interval and a short join are kept.
                    double sourceStart = Math.Max(0, section.SongStart - context * section.Stretch);
                    double sourceEnd = Math.Min(sourceLength, section.SongEnd + context * section.Stretch);
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

                    int source = Bass.CreateStream(rate, channels, BassFlags.Decode | BassFlags.Float, read);
                    if (source == 0)
                        throw new InvalidOperationException($"The audio section couldn't be created ({Bass.LastError}).");

                    int tempo = BassFx.TempoCreate(source, BassFlags.Decode | BassFlags.FxFreeSource | BassFlags.Float);
                    if (tempo == 0)
                    {
                        Bass.StreamFree(source);
                        throw new InvalidOperationException($"The song couldn't be stretched ({Bass.LastError}).");
                    }

                    try
                    {
                        if (!Bass.ChannelSetAttribute(tempo, ChannelAttribute.Tempo, (section.Stretch - 1) * 100))
                            throw new InvalidOperationException($"The song's tempo couldn't be matched ({Bass.LastError}).");

                        long firstFrame = frameAt(section.MapStart);
                        long lastFrame = Math.Min(frameAt(section.MapEnd), frameAt(outputEnd));
                        long blendFrames = i == plan.Sections.Count - 1 ? 0 : Math.Min(frameAt(join), Math.Max(0, lastFrame - firstFrame));
                        long skip = frameAt((section.SongStart - sourceStart) / section.Stretch) * channels;
                        long position = firstFrame * channels;
                        long limit = Math.Min(output.Length, (lastFrame + blendFrames) * channels);
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
                                    if (position < 0)
                                        continue;
                                    long frame = position / channels;
                                    // The preceding section has rendered a 10ms tail at this same output position.
                                    float mix = i > 0 ? (float)Math.Clamp((double)(frame - firstFrame) / Math.Max(1, frameAt(join)), 0, 1) : 1;
                                    output[position] = output[position] * (1 - mix) + buffer[sample] * mix;
                                }
                            }

                            // Refuse truncated/undecodable audio instead of silently filling a playable section with zeros.
                            if (position < Math.Min(lastFrame, frameAt(mapEnd)) * channels - channels)
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
                }

                using var result = new MemoryStream();
                using var writer = new BinaryWriter(result);
                writeHeader(writer, rate, channels, checked(output.Length * sizeof(short)));
                for (int sample = 0; sample < output.Length; sample++)
                {
                    double time = sample / channels * 1000.0 / rate;
                    double gain = plan.Start < 0 ? Math.Clamp(time / SongSwapping.FADE_IN, 0, 1) : 1;
                    gain *= 1 - Math.Clamp((time - mapEnd - SongSwapping.TAIL) / SongSwapping.FADE_OUT, 0, 1);
                    writer.Write((short)Math.Round(Math.Clamp(output[sample] * gain, -1, 1) * short.MaxValue));
                }

                writer.Flush();
                return result.ToArray();
            }
            finally
            {
                Bass.StreamFree(decode);
            }
        }

        private static void writeHeader(BinaryWriter writer, int rate, int channels, int bytes)
        {
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
        }
    }
}

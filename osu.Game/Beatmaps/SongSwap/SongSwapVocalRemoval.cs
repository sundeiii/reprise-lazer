// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using ManagedBass;
using ManagedBass.Mix;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Beatmaps.SongSwap
{
    /// <summary>
    /// Reprise: a song's instrumental, its vocals taken out by UVR's MDX-Net "Inst HQ 3" model (run on the CPU with ONNX Runtime; fetched
    /// from UVR's model repository the first time). Each song's is kept, so it's only made once (about a minute and a half for a song).
    /// </summary>
    public class SongSwapVocalRemoval
    {
        private const string model_name = @"UVR-MDX-NET-Inst_HQ_3.onnx";
        private const string model_url = @"https://github.com/TRvlvr/model_repo/releases/download/all_public_uvr_models/" + model_name;
        private const string model_sha256 = @"317554b07fe1ea5279a77f2b1520a41ea4b93432560c4ffd08792c30fddf9adc";

        // The model's (UVR's settings for it): 44.1kHz, a 6144-sample FFT every 1024, the lowest 3072 bins, 256 frames at a time.
        private const int rate = 44100;
        private const int n_fft = 6144;
        private const int hop = 1024;
        private const int dim_f = 3072;
        private const int dim_t = 256;
        private const int bins = n_fft / 2 + 1;
        private const int chunk_size = hop * (dim_t - 1);
        private const int trim = n_fft / 2;
        private const int gen_size = chunk_size - 2 * trim;
        private const float compensate = 1.022f;

        private readonly Storage storage;
        private readonly object sessionLock = new object();
        private InferenceSession? session;

        public SongSwapVocalRemoval(Storage storage)
        {
            this.storage = storage;
        }

        /// <summary>
        /// The song's instrumental (a 44.1kHz wave file), made the first time (slow: in the background) and kept.
        /// </summary>
        /// <param name="progress">How far along it is (0 to 1) while it's made.</param>
        public byte[] Instrumental(byte[] song, Action<double>? progress = null, CancellationToken cancellation = default)
        {
            string path = $@"instrumentals/{Convert.ToHexString(SHA256.HashData(song)).ToLowerInvariant()}.wav";

            if (storage.Exists(path))
                return read(path);

            var (left, right) = decode(song);
            var (instLeft, instRight) = separate(left, right, progress, cancellation);
            byte[] wave = toWave(instLeft, instRight);

            using (var stream = storage.CreateFileSafely(path))
                stream.Write(wave);

            return wave;
        }

        /// <summary>Whether the song's instrumental has been made already (so it's quick).</summary>
        public bool HasInstrumental(byte[] song) =>
            storage.Exists($@"instrumentals/{Convert.ToHexString(SHA256.HashData(song)).ToLowerInvariant()}.wav");

        private byte[] read(string path)
        {
            using var stream = storage.GetStream(path);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        private InferenceSession getSession()
        {
            lock (sessionLock)
            {
                if (session != null)
                    return session;

                string path = $@"models/{model_name}";

                if (!storage.Exists(path))
                {
                    Logger.Log($"Song swap: fetching the vocal removal model ({model_url})");

                    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                    byte[] model = http.GetByteArrayAsync(model_url).GetAwaiter().GetResult();

                    if (!Convert.ToHexString(SHA256.HashData(model)).Equals(model_sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("The vocal removal model didn't download right.");

                    using var stream = storage.CreateFileSafely(path);
                    stream.Write(model);
                }

                var options = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    // Leaves a core or two for the game.
                    IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount - 2),
                };

                return session = new InferenceSession(storage.GetFullPath(path), options);
            }
        }

        // The song as 44.1kHz stereo.
        private static (float[] left, float[] right) decode(byte[] song)
        {
            int source = Bass.CreateStream(song, 0, song.Length, BassFlags.Decode | BassFlags.Prescan | BassFlags.Float);
            if (source == 0)
                throw new InvalidOperationException($"The song couldn't be read ({Bass.LastError}).");

            int mixer = BassMix.CreateMixerStream(rate, 2, BassFlags.Decode | BassFlags.Float | BassFlags.MixerEnd);

            try
            {
                if (mixer == 0 || !BassMix.MixerAddChannel(mixer, source, BassFlags.MixerChanDownMix))
                    throw new InvalidOperationException($"The song couldn't be converted ({Bass.LastError}).");

                using var samples = new MemoryStream();
                float[] buffer = new float[rate * 2];
                var pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                try
                {
                    int bytes;
                    while ((bytes = Bass.ChannelGetData(mixer, pinned.AddrOfPinnedObject(), buffer.Length * sizeof(float))) > 0)
                        samples.Write(MemoryMarshal.AsBytes(buffer.AsSpan(0, bytes / sizeof(float))));
                }
                finally
                {
                    pinned.Free();
                }

                var all = MemoryMarshal.Cast<byte, float>(samples.GetBuffer().AsSpan(0, (int)samples.Length));
                int frames = all.Length / 2;
                float[] left = new float[frames];
                float[] right = new float[frames];

                for (int i = 0; i < frames; i++)
                {
                    left[i] = all[2 * i];
                    right[i] = all[2 * i + 1];
                }

                return (left, right);
            }
            finally
            {
                if (mixer != 0)
                    Bass.StreamFree(mixer);
                Bass.StreamFree(source);
            }
        }

        // As UVR runs MDX-Net models: overlapping chunks (their edges trimmed), each turned into its spectrogram, the model's instrumental
        // spectrogram turned back into sound.
        private (float[] left, float[] right) separate(float[] left, float[] right, Action<double>? progress, CancellationToken cancellation)
        {
            var model = getSession();
            string input = model.InputMetadata.Keys.First();

            int n = left.Length;
            int pad = gen_size - n % gen_size;
            int total = trim + n + pad + trim;
            float[][] padded = { new float[total], new float[total] };
            Array.Copy(left, 0, padded[0], trim, n);
            Array.Copy(right, 0, padded[1], trim, n);

            float[][] output = { new float[n + pad], new float[n + pad] };
            double[] window = hann(n_fft);
            int chunks = (n + pad) / gen_size;

            for (int chunk = 0; chunk < chunks; chunk++)
            {
                cancellation.ThrowIfCancellationRequested();

                int start = chunk * gen_size;
                var tensor = new DenseTensor<float>(new[] { 1, 4, dim_f, dim_t });

                for (int c = 0; c < 2; c++)
                {
                    var spec = stft(padded[c], start, window);

                    for (int t = 0; t < dim_t; t++)
                    {
                        for (int f = 0; f < dim_f; f++)
                        {
                            tensor[0, c * 2, f, t] = (float)spec[t][f].Real;
                            tensor[0, c * 2 + 1, f, t] = (float)spec[t][f].Imaginary;
                        }
                    }
                }

                using (var results = model.Run(new[] { NamedOnnxValue.CreateFromTensor(input, tensor) }))
                {
                    var result = results.First().AsTensor<float>();

                    for (int c = 0; c < 2; c++)
                    {
                        var spec = new Complex[dim_t][];

                        for (int t = 0; t < dim_t; t++)
                        {
                            spec[t] = new Complex[bins];
                            for (int f = 0; f < dim_f; f++)
                                spec[t][f] = new Complex(result[0, c * 2, f, t], result[0, c * 2 + 1, f, t]);
                        }

                        float[] wave = istft(spec, window);
                        for (int i = 0; i < gen_size; i++)
                            output[c][start + i] = wave[trim + i] * compensate;
                    }
                }

                progress?.Invoke((chunk + 1) / (double)chunks);
            }

            return (output[0][..n], output[1][..n]);
        }

        private static double[] hann(int length)
        {
            double[] w = new double[length];
            for (int i = 0; i < length; i++)
                w[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / length);
            return w;
        }

        // torch.stft(center: true, reflect padding) of one chunk.
        private static Complex[][] stft(float[] x, int start, double[] window)
        {
            int half = n_fft / 2;
            var frames = new Complex[dim_t][];
            var buffer = new Complex[n_fft];

            for (int t = 0; t < dim_t; t++)
            {
                for (int i = 0; i < n_fft; i++)
                {
                    int at = t * hop + i - half;
                    if (at < 0)
                        at = -at;
                    else if (at >= chunk_size)
                        at = 2 * (chunk_size - 1) - at;

                    buffer[i] = new Complex(x[start + at] * window[i], 0);
                }

                frames[t] = fft(buffer)[..bins];
            }

            return frames;
        }

        // torch.istft(center: true) back to the chunk's length.
        private static float[] istft(Complex[][] spec, double[] window)
        {
            int half = n_fft / 2;
            int full = n_fft + hop * (dim_t - 1);
            double[] y = new double[full];
            double[] weights = new double[full];
            var buffer = new Complex[n_fft];

            for (int t = 0; t < dim_t; t++)
            {
                // The inverse through the forward transform: conjugated in and out.
                for (int k = 0; k <= half; k++)
                    buffer[k] = Complex.Conjugate(spec[t][k]);
                for (int k = half + 1; k < n_fft; k++)
                    buffer[k] = spec[t][n_fft - k];

                var time = fft(buffer);

                for (int i = 0; i < n_fft; i++)
                {
                    y[t * hop + i] += time[i].Real / n_fft * window[i];
                    weights[t * hop + i] += window[i] * window[i];
                }
            }

            float[] result = new float[chunk_size];
            for (int i = 0; i < chunk_size; i++)
                result[i] = weights[i + half] > 1e-10 ? (float)(y[i + half] / weights[i + half]) : 0;
            return result;
        }

        // 6144 = 3 × 2048: one radix-3 step over radix-2 transforms.
        private static Complex[] fft(Complex[] x)
        {
            int n = x.Length;
            int m = n / 3;
            var parts = new Complex[3][];

            for (int r = 0; r < 3; r++)
            {
                var part = new Complex[m];
                for (int i = 0; i < m; i++)
                    part[i] = x[3 * i + r];
                parts[r] = radix2(part);
            }

            var result = new Complex[n];

            for (int k = 0; k < n; k++)
            {
                result[k] = parts[0][k % m]
                            + Complex.FromPolarCoordinates(1, -2 * Math.PI * k / n) * parts[1][k % m]
                            + Complex.FromPolarCoordinates(1, -4 * Math.PI * k / n) * parts[2][k % m];
            }

            return result;
        }

        private static Complex[] radix2(Complex[] a)
        {
            int n = a.Length;

            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                    j ^= bit;
                j ^= bit;
                if (i < j)
                    (a[i], a[j]) = (a[j], a[i]);
            }

            for (int length = 2; length <= n; length <<= 1)
            {
                double angle = -2 * Math.PI / length;
                var step = new Complex(Math.Cos(angle), Math.Sin(angle));

                for (int i = 0; i < n; i += length)
                {
                    Complex w = 1;

                    for (int k = 0; k < length / 2; k++)
                    {
                        var u = a[i + k];
                        var v = a[i + k + length / 2] * w;
                        a[i + k] = u + v;
                        a[i + k + length / 2] = u - v;
                        w *= step;
                    }
                }
            }

            return a;
        }

        private static byte[] toWave(float[] left, float[] right)
        {
            int n = left.Length;
            using var memory = new MemoryStream(44 + n * 4);
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write("RIFF"u8);
                writer.Write(36 + n * 4);
                writer.Write("WAVE"u8);
                writer.Write("fmt "u8);
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)2);
                writer.Write(rate);
                writer.Write(rate * 4);
                writer.Write((short)4);
                writer.Write((short)16);
                writer.Write("data"u8);
                writer.Write(n * 4);

                for (int i = 0; i < n; i++)
                {
                    writer.Write((short)Math.Clamp(left[i] * 32767, -32768, 32767));
                    writer.Write((short)Math.Clamp(right[i] * 32767, -32768, 32767));
                }
            }

            return memory.ToArray();
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using ManagedBass;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Beatmaps.SongSwap;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>
    /// Takes the vocals out of a real song: run by hand, with SONG_SWAP_MODEL_DIR a folder holding models/UVR-MDX-NET-Inst_HQ_3.onnx (else it's
    /// fetched) and SONG_SWAP_TEST_SONG a song with vocals.
    /// </summary>
    [Explicit("Needs the 67 MB model and a real song; slow.")]
    public class SongSwapVocalRemovalTest
    {
        [Test]
        public void TestVocalsTakenOutAndKept()
        {
            if (!Bass.Init(0) && Bass.LastError != Errors.Already)
                Assert.Ignore("No BASS.");

            string dir = Environment.GetEnvironmentVariable("SONG_SWAP_MODEL_DIR") ?? Path.Combine(Path.GetTempPath(), "song-swap-vocal-removal");
            string song = Environment.GetEnvironmentVariable("SONG_SWAP_TEST_SONG") ?? throw new IgnoreException("No song given.");

            var storage = new NativeStorage(dir);
            storage.DeleteDirectory("instrumentals");
            var removal = new SongSwapVocalRemoval(storage);
            byte[] data = File.ReadAllBytes(song);

            double last = 0;
            var watch = Stopwatch.StartNew();
            byte[] instrumental = removal.Instrumental(data, p => last = p);
            TestContext.Out.WriteLine($"made in {watch.Elapsed.TotalSeconds:0.0}s");

            Assert.That(last, Is.EqualTo(1));
            Assert.That(removal.HasInstrumental(data));
            Assert.That(SongSwapping.LengthOf(instrumental), Is.EqualTo(SongSwapping.LengthOf(data)).Within(50), "as long as the song");

            File.WriteAllBytes(Path.Combine(dir, "instrumental.wav"), instrumental);

            watch.Restart();
            byte[] again = removal.Instrumental(data);
            double secondTime = watch.Elapsed.TotalSeconds;
            Assert.That(again.AsSpan().SequenceEqual(instrumental), "kept");
            Assert.That(secondTime, Is.LessThan(2), "quick the second time");
        }
    }
}

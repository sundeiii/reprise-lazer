// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Framework.Text;
using osu.Game.Graphics;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>Reprise: Windows' Arial baked from this computer's copy into the game's font format, for the Simplistic pp counter.</summary>
    [TestFixture]
    public class InstalledFontBakerTest
    {
        [Test]
        public void TestArialBaked()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            if (!File.Exists(path))
                Assert.Ignore("No Arial here.");

            var timer = Stopwatch.StartNew();
            var store = InstalledFontBaker.Bake(path, "Arial", "Regular");
            TestContext.Progress.WriteLine($"baked in {timer.ElapsedMilliseconds}ms");

            Assert.That(store, Is.Not.Null);
            Assert.That(store!.GetAvailableResources(), Is.EquivalentTo(new[] { "Fonts/Arial/Arial-Regular.bin", "Fonts/Arial/Arial-Regular_0.png" }));

            // Read as the game reads its fonts.
            var glyphs = new GlyphStore(new ResourceStore<byte[]>(store), "Fonts/Arial/Arial-Regular");
            glyphs.LoadFontAsync().WaitSafely();

            var p = glyphs.Get('p');
            var zero = glyphs.Get('0');
            Assert.That(p, Is.Not.Null);
            Assert.That(zero!.XAdvance, Is.EqualTo(56).Within(1), "Arial's digits are 0.556em");
            Assert.That(p!.XAdvance, Is.EqualTo(56).Within(1));
            Assert.That(glyphs.GetKerning('A', 'V'), Is.LessThan(0), "with Arial's kerning");
            Assert.That(glyphs.Baseline, Is.EqualTo(91).Within(1));
        }
    }

    internal static class TaskExtensionsForTest
    {
        public static void WaitSafely(this System.Threading.Tasks.Task task) => task.GetAwaiter().GetResult();
    }
}

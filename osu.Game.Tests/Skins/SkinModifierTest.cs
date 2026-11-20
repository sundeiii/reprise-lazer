// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using osu.Game.Skinning.Mixer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.Skins
{
    /// <summary>Reprise: the skin modifier's changes to a skin.</summary>
    [TestFixture]
    public class SkinModifierTest
    {
        private static byte[] png(int width, int height, Rgba32 colour, Rectangle? filled = null)
        {
            using var image = new Image<Rgba32>(width, height);
            var area = filled ?? new Rectangle(0, 0, width, height);

            for (int x = area.Left; x < area.Right; x++)
            {
                for (int y = area.Top; y < area.Bottom; y++)
                    image[x, y] = colour;
            }

            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            return stream.ToArray();
        }

        private static Image<Rgba32> load(byte[] data) => Image.Load<Rgba32>(data);

        private static string ini(Dictionary<string, byte[]> files) => Encoding.UTF8.GetString(files["skin.ini"]);

        private static Dictionary<string, byte[]> skin(params (string path, byte[] data)[] files)
        {
            var d = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["skin.ini"] = Encoding.UTF8.GetBytes("[General]\r\nName: test\r\n[Colours]\r\nCombo1: 255,0,0\r\nCombo2: 0,255,0\r\nCombo3: 0,0,255\r\n"),
            };

            foreach (var (path, data) in files)
                d[path] = data;

            return d;
        }

        private static readonly Func<string, byte[]?> no_classic = _ => null;

        [Test]
        public void TestHideGreats()
        {
            var files = skin(("hit300.png", png(4, 4, Color.Red)), ("hit300-0@2x.png", png(4, 4, Color.Red)), ("hit100.png", png(4, 4, Color.Blue)));

            SkinModifying.Modify(files, new SkinModifications { HideGreats = true }, no_classic);

            Assert.That(files["hit300.png"], Is.EqualTo(SkinMixing.TRANSPARENT_PNG));
            Assert.That(files["hit300g@2x.png"], Is.EqualTo(SkinMixing.TRANSPARENT_PNG));
            Assert.That(files.ContainsKey("hit300-0@2x.png"), Is.False);
            Assert.That(files.ContainsKey("hit100.png"), Is.True, "100s stay");
        }

        [Test]
        public void TestSmoothTrail()
        {
            var cursor = png(8, 8, Color.White);
            var files = skin(("cursor.png", cursor), ("cursortrail.png", png(32, 32, Color.White, new Rectangle(12, 12, 8, 8))));

            SkinModifying.Modify(files, new SkinModifications { SmoothTrail = true }, no_classic);

            Assert.That(files["cursormiddle.png"], Is.EqualTo(cursor), "the cursor as its middle");
            Assert.That(files["cursor.png"], Is.EqualTo(SkinMixing.TRANSPARENT_PNG));

            using var trail = load(files["cursortrail.png"]);
            Assert.That((trail.Width, trail.Height), Is.EqualTo((8, 8)), "the trail cropped to what's in it");
        }

        [Test]
        public void TestStillInterface()
        {
            var files = skin(("menu-back-0.png", png(2, 2, Color.Red)), ("menu-back-1.png", png(2, 2, Color.Green)), ("menu-back-2.png", png(2, 2, Color.Blue)));

            SkinModifying.Modify(files, new SkinModifications { StillInterface = true }, no_classic);

            Assert.That(files.ContainsKey("menu-back-0.png"), Is.False);
            using var back = load(files["menu-back.png"]);
            Assert.That(back[0, 0], Is.EqualTo(new Rgba32(0, 128, 0, 255)).Or.EqualTo(Color.Green.ToPixel<Rgba32>()), "the middle frame kept");
        }

        [Test]
        public void TestComboColours()
        {
            var files = skin();

            SkinModifying.Modify(files, new SkinModifications { ComboColours = new[] { Color.FromRgb(1, 2, 3), Color.FromRgb(4, 5, 6) } }, no_classic);

            Assert.That(ini(files), Does.Contain("Combo1: 1,2,3").And.Contain("Combo2: 4,5,6").And.Not.Contain("Combo3"));
        }

        [Test]
        public void TestInstafade()
        {
            var files = skin(
                ("hitcircle@2x.png", png(100, 100, Color.White)),
                ("hitcircleoverlay@2x.png", png(100, 100, Color.Transparent)),
                ("default-1@2x.png", png(20, 30, Color.Black)));

            // The other numbers from the classic skin.
            SkinModifying.Modify(files, new SkinModifications { Instafade = true }, name => name.StartsWith("default-", StringComparison.Ordinal) ? png(20, 30, Color.Black) : null);

            Assert.That(files["hitcircle@2x.png"], Is.EqualTo(SkinMixing.TRANSPARENT_PNG));
            Assert.That(files["hitcircleoverlay.png"], Is.EqualTo(SkinMixing.TRANSPARENT_PNG));

            using var one = load(files["default-1@2x.png"]);
            Assert.That((one.Width, one.Height), Is.EqualTo((125, 125)), "the circle (at 125%) with the number on it");
            Assert.That(one[2, 62], Is.EqualTo(new Rgba32(0, 255, 0, 255)), "the circle in the second combo colour");
            Assert.That(one[62, 62], Is.EqualTo(new Rgba32(0, 0, 0, 255)), "the number over it");

            using var oneSd = load(files["default-1.png"]);
            Assert.That(oneSd.Width, Is.EqualTo(62), "and its 1x");

            using var zero = load(files["default-0@2x.png"]);
            Assert.That(zero[62, 62].A, Is.EqualTo(0), "0 left empty");

            Assert.That(ini(files), Does.Contain("HitCircleOverlap: 62").And.Contain("Combo1: 0,255,0").And.Not.Contain("Combo3"));
        }
    }
}

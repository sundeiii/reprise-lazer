// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Graphics
{
    /// <summary>
    /// Reprise: a font installed on this computer, baked when the game starts into the bitmap font format the game draws text with (as its own
    /// fonts are baked): its printable ASCII, at an em of 100px (so a font size is its CSS font size), with its kerning. For fonts that can't be
    /// shipped with the game, like Windows' Arial, but every Windows computer has.
    /// </summary>
    public static class InstalledFontBaker
    {
        private const int em = 100;
        private const int page = 1024;
        private const int padding = 2;
        private const int spacing = 4;

        /// <summary>
        /// The font file baked, as a resource store holding <c>Fonts/{family}/{family}-{weight}.bin</c> and its page (or null, if it can't be read).
        /// </summary>
        public static IResourceStore<byte[]>? Bake(string path, string family, string weight)
        {
            try
            {
                var font = new FontCollection().Add(path).CreateFont(em);
                var options = new TextOptions(font) { Dpi = 72, KerningMode = KerningMode.Standard };
                var metrics = font.FontMetrics.HorizontalMetrics;
                float scale = em / (float)font.FontMetrics.UnitsPerEm;
                int ascent = (int)Math.Round(metrics.Ascender * scale);
                int lineHeight = ascent + (int)Math.Round(-metrics.Descender * scale);

                string name = $@"{family}-{weight}";
                var chars = Enumerable.Range(32, 95).Select(c => (char)c).ToArray();
                var glyphs = new List<(char c, int x, int y, int w, int h, int xOffset, int yOffset, int advance)>();

                using var atlas = new Image<Rgba32>(page, page, new Rgba32(255, 255, 255, 0));
                int x = spacing, y = spacing, shelf = 0;

                // Taller glyphs first, onto shelves.
                foreach (char c in chars.OrderByDescending(c => TextMeasurer.MeasureBounds(c.ToString(), options).Height))
                {
                    string text = c.ToString();
                    int advance = (int)Math.Round(TextMeasurer.MeasureAdvance(text, options).Width);
                    var bounds = TextMeasurer.MeasureBounds(text, options);

                    if (bounds.Width <= 0 || bounds.Height <= 0)
                    {
                        // An empty glyph (space) still points at a pixel: the framework can't load a 0-pixel glyph. The page's last corner stays clear.
                        glyphs.Add((c, page - 1, page - 1, 1, 1, 0, 0, advance));
                        continue;
                    }

                    int left = (int)Math.Floor(bounds.Left), top = (int)Math.Floor(bounds.Top);
                    int w = (int)Math.Ceiling(bounds.Right) - left + padding * 2;
                    int h = (int)Math.Ceiling(bounds.Bottom) - top + padding * 2;

                    if (x + w + spacing > page)
                    {
                        x = spacing;
                        y += shelf + spacing;
                        shelf = 0;
                    }

                    if (y + h > page)
                        return null;

                    // Drawn white, its coverage in alpha (as the game's own font pages are).
                    var at = new RichTextOptions(font) { Dpi = 72, Origin = new PointF(x + padding - left, y + padding - top) };
                    atlas.Mutate(i => i.DrawText(at, text, Color.White));

                    glyphs.Add((c, x, y, w, h, left - padding, top - padding, advance));
                    x += w + spacing;
                    shelf = Math.Max(shelf, h);
                }

                var kerning = new List<(char a, char b, short amount)>();
                var advances = chars.ToDictionary(c => c, c => TextMeasurer.MeasureAdvance(c.ToString(), options).Width);

                foreach (char a in chars)
                {
                    foreach (char b in chars)
                    {
                        int amount = (int)Math.Round(TextMeasurer.MeasureAdvance($"{a}{b}", options).Width - advances[a] - advances[b]);
                        if (amount != 0)
                            kerning.Add((a, b, (short)amount));
                    }
                }

                using var png = new MemoryStream();
                atlas.SaveAsPng(png);

                return new BakedFontStore(new Dictionary<string, byte[]>
                {
                    [$@"Fonts/{family}/{name}.bin"] = binary(family, name, lineHeight, ascent, glyphs, kerning),
                    [$@"Fonts/{family}/{name}_0.png"] = png.ToArray(),
                });
            }
            catch (Exception)
            {
                return null;
            }
        }

        // BMFont's binary format, laid out as the game's own fonts are (see osu-resources' Fonts/README.md).
        private static byte[] binary(string family, string name, int lineHeight, int ascent, List<(char c, int x, int y, int w, int h, int xOffset, int yOffset, int advance)> glyphs,
                                     List<(char a, char b, short amount)> kerning)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(Encoding.ASCII.GetBytes("BMF"));
            writer.Write((byte)3);

            byte[] familyName = Encoding.ASCII.GetBytes(family + "\0");
            writeBlock(1, 14 + familyName.Length, w =>
            {
                w.Write((short)em);
                w.Write((byte)0xC0);
                w.Write((byte)0);
                w.Write((ushort)100);
                w.Write((byte)4);
                w.Write(new byte[] { 0, 0, 0, 0 });
                w.Write((byte)spacing);
                w.Write((byte)spacing);
                w.Write((byte)0);
                w.Write(familyName);
            });

            writeBlock(2, 15, w =>
            {
                w.Write((ushort)lineHeight);
                w.Write((ushort)ascent);
                w.Write((ushort)page);
                w.Write((ushort)page);
                w.Write((ushort)1);
                w.Write(new byte[] { 0, 0, 4, 4, 4 });
            });

            byte[] pageName = Encoding.ASCII.GetBytes($"{name}_0.png\0");
            writeBlock(3, pageName.Length, w => w.Write(pageName));

            writeBlock(4, glyphs.Count * 20, w =>
            {
                foreach (var g in glyphs)
                {
                    w.Write((uint)g.c);
                    w.Write((ushort)g.x);
                    w.Write((ushort)g.y);
                    w.Write((ushort)g.w);
                    w.Write((ushort)g.h);
                    w.Write((short)g.xOffset);
                    w.Write((short)g.yOffset);
                    w.Write((short)g.advance);
                    w.Write((byte)0);
                    w.Write((byte)15);
                }
            });

            writeBlock(5, kerning.Count * 10, w =>
            {
                foreach (var k in kerning)
                {
                    w.Write((uint)k.a);
                    w.Write((uint)k.b);
                    w.Write(k.amount);
                }
            });

            writer.Flush();
            return stream.ToArray();

            void writeBlock(byte type, int length, Action<BinaryWriter> write)
            {
                writer.Write(type);
                writer.Write(length);
                write(writer);
            }
        }

        private class BakedFontStore : IResourceStore<byte[]>
        {
            private readonly Dictionary<string, byte[]> files;

            public BakedFontStore(Dictionary<string, byte[]> files) => this.files = files;

            public byte[] Get(string name) => files.GetValueOrDefault(name)!;

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Get(name));

            public Stream? GetStream(string name) => files.TryGetValue(name, out byte[]? data) ? new MemoryStream(data) : null;

            public IEnumerable<string> GetAvailableResources() => files.Keys;

            public void Dispose()
            {
            }
        }
    }
}

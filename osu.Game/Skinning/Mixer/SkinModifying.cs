// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// The instafade, smooth trail and still interface changes below follow OsuSkinMixer's skin modifier
// (https://github.com/rednir/OsuSkinMixer, src/Services/SkinModifierMachine.cs), used under its licence:
//
// MIT License
//
// Copyright (c) 2025 rednir
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
// CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Skinning.Mixer
{
    /// <summary>Reprise: what the skin modifier changes in a skin.</summary>
    public class SkinModifications
    {
        /// <summary>Hitcircles that vanish the moment they're hit: circle, overlay and number made into the number's image.</summary>
        public bool Instafade { get; set; }

        /// <summary>A smooth cursor trail: the cursor's image as its middle (which a trail flows from), the trail cropped to what's in it.</summary>
        public bool SmoothTrail { get; set; }

        /// <summary>Interface elements held still (each animation's middle frame kept as the element).</summary>
        public bool StillInterface { get; set; }

        /// <summary>No 300s shown in play (osu!'s and the gekis and katus with them).</summary>
        public bool HideGreats { get; set; }

        /// <summary>The combo colours instead of the skin's (null: the skin's own).</summary>
        public Color[]? ComboColours { get; set; }

        public bool Any => Instafade || SmoothTrail || StillInterface || HideGreats || ComboColours != null;
    }

    /// <summary>
    /// Reprise: a skin modified (as OsuSkinMixer's modifier changes one): instafade circles, a smooth cursor trail, a still interface,
    /// no 300s, other combo colours. The game's classic skin's images stand in for those the skin hasn't got.
    /// </summary>
    public static class SkinModifying
    {
        /// <summary>The game's default combo colours (a skin without any of its own has these).</summary>
        public static readonly Color[] DEFAULT_COMBO_COLOURS =
        {
            Color.FromRgb(255, 192, 0),
            Color.FromRgb(0, 202, 0),
            Color.FromRgb(18, 124, 255),
            Color.FromRgb(242, 24, 57),
        };

        /// <param name="files">The skin's files by path (changed in place).</param>
        /// <param name="modifications">What to change.</param>
        /// <param name="classic">The game's classic skin's image by name ("hitcircle@2x.png"), for those the skin hasn't got.</param>
        public static void Modify(Dictionary<string, byte[]> files, SkinModifications modifications, Func<string, byte[]?> classic)
        {
            var ini = SkinIni.Parse(get(files, @"skin.ini"));

            if (modifications.ComboColours is Color[] colours && colours.Length > 0)
                setComboColours(ini, colours);

            if (modifications.HideGreats)
                hideGreats(files);

            if (modifications.SmoothTrail)
                smoothTrail(files);

            if (modifications.StillInterface)
                stillInterface(files);

            // Last: it bakes in the combo colour as it is by then.
            if (modifications.Instafade)
                instafade(files, ini, classic);

            files[@"skin.ini"] = Encoding.UTF8.GetBytes(ini.ToString());
        }

        /// <summary>The skin's combo colours (its skin.ini's, in the order the game uses them: 1, 2, 3...), or the game's defaults.</summary>
        public static Color[] ComboColoursOf(SkinIni ini)
        {
            var colours = new List<Color>();

            for (int i = 1; i <= 8; i++)
            {
                if (ini.Get(@"Colours", $@"Combo{i}") is string value && parseColour(value) is Color c)
                    colours.Add(c);
                else
                    break;
            }

            return colours.Count > 0 ? colours.ToArray() : DEFAULT_COMBO_COLOURS;
        }

        private static void setComboColours(SkinIni ini, Color[] colours)
        {
            for (int i = 1; i <= 8; i++)
            {
                if (i <= colours.Length)
                    ini.Set(@"Colours", $@"Combo{i}", toIni(colours[i - 1]));
                else
                    ini.Remove(@"Colours", $@"Combo{i}");
            }
        }

        private static void hideGreats(Dictionary<string, byte[]> files)
        {
            foreach (string name in new[] { @"hit300", @"hit300g", @"hit300k" })
            {
                var option = new SkinMixerFileOption(name, false, isAnimatable: true);

                foreach (string path in files.Keys.Where(option.Matches).ToArray())
                    files.Remove(path);

                foreach (string path in option.BlankFiles())
                    files[path] = SkinMixing.TRANSPARENT_PNG;
            }
        }

        private static void smoothTrail(Dictionary<string, byte[]> files)
        {
            foreach (string suffix in new[] { string.Empty, @"@2x" })
            {
                string cursor = $@"cursor{suffix}.png";
                string trail = $@"cursortrail{suffix}.png";

                if (get(files, cursor) is not byte[] cursorData || get(files, trail) is not byte[] trailData)
                    continue;

                using var trailImage = Image.Load<Rgba32>(trailData);
                var content = contentRect(trailImage);

                if (content.Width == 0 || content.Height == 0)
                    continue;

                trailImage.Mutate(i => i.Crop(content));
                files[trail] = png(trailImage);

                // A cursor with a middle draws its trail as one smooth line.
                files[$@"cursormiddle{suffix}.png"] = cursorData;
                files[cursor] = SkinMixing.TRANSPARENT_PNG;
            }
        }

        private static void stillInterface(Dictionary<string, byte[]> files)
        {
            var interfaceGroup = (SkinMixerGroup)SkinMixerParts.Create()[0];

            foreach (var option in interfaceGroup.Leaves.OfType<SkinMixerFileOption>().Where(o => o.IsAnimatable))
            {
                // Its frames ("menu-back-0.png", "menu-back-1@2x.png"...), by frame and by @2x or not.
                var frames = files.Keys
                                  .Where(f => !f.Contains('/') && f.StartsWith(option.FileName + '-', StringComparison.OrdinalIgnoreCase))
                                  .Select(f => (path: f, frame: frameNumber(f, option.FileName), hd: Path.GetFileNameWithoutExtension(f).EndsWith(@"@2x", StringComparison.OrdinalIgnoreCase)))
                                  .Where(f => f.frame != null)
                                  .ToArray();

                if (frames.Length == 0)
                    continue;

                foreach (var set in frames.GroupBy(f => f.hd))
                {
                    var ordered = set.OrderBy(f => f.frame).ToArray();
                    var keep = ordered[ordered.Length / 2];

                    files[$@"{option.FileName}{(set.Key ? @"@2x" : string.Empty)}{Path.GetExtension(keep.path)}"] = files[keep.path];

                    foreach (var f in ordered)
                        files.Remove(f.path);
                }
            }
        }

        private static int? frameNumber(string path, string name)
        {
            string rest = Path.GetFileNameWithoutExtension(path)[(name.Length + 1)..];

            if (rest.EndsWith(@"@2x", StringComparison.OrdinalIgnoreCase))
                rest = rest[..^3];

            return int.TryParse(rest, NumberStyles.None, CultureInfo.InvariantCulture, out int frame) ? frame : null;
        }

        private static void instafade(Dictionary<string, byte[]> files, SkinIni ini, Func<string, byte[]?> classic)
        {
            // Following skinship's instafade guide (as OsuSkinMixer does), at @2x.
            string prefix = (ini.Get(@"Fonts", @"HitCirclePrefix") ?? @"default").Trim().Replace('\\', '/');

            using var circle = loadHd(files, @"hitcircle", classic);
            using var overlay = loadHd(files, @"hitcircleoverlay", classic);

            if (circle == null || overlay == null)
                return;

            circle.Mutate(i => i.Resize((int)(circle.Width * 1.25), (int)(circle.Height * 1.25)));
            overlay.Mutate(i => i.Resize((int)(overlay.Width * 1.25), (int)(overlay.Height * 1.25)));

            // The circle in the combo colour it'd be drawn in (every combo the same colour from now on, see below).
            var colours = ComboColoursOf(ini);
            var colour = (colours.Length > 1 ? colours[1] : colours[0]).ToPixel<Rgba32>();

            circle.ProcessPixelRows(rows =>
            {
                for (int y = 0; y < rows.Height; y++)
                {
                    var row = rows.GetRowSpan(y);

                    for (int x = 0; x < row.Length; x++)
                        row[x] = new Rgba32((byte)(row[x].R * colour.R / 255), (byte)(row[x].G * colour.G / 255), (byte)(row[x].B * colour.B / 255), row[x].A);
                }
            });

            int width = Math.Max(circle.Width, overlay.Width);
            int height = Math.Max(circle.Height, overlay.Height);

            for (int n = 0; n <= 9; n++)
            {
                using var number = loadHd(files, $@"{prefix}-{n}", name => classic($@"default-{n}@2x.png"))
                                   ?? new Image<Rgba32>(1, 1);

                using var baked = new Image<Rgba32>(width, height);

                baked.Mutate(i =>
                {
                    i.DrawImage(circle, new Point((width - circle.Width) / 2, (height - circle.Height) / 2), 1);
                    i.DrawImage(overlay, new Point((width - overlay.Width) / 2, (height - overlay.Height) / 2), 1);
                    i.DrawImage(number, new Point((width - number.Width) / 2, (height - number.Height) / 2), 1);
                });

                // 0 is never a combo's first number: left empty (as the guide has it).
                if (n == 0)
                    baked.Mutate(i => i.Opacity(0));

                replace(files, $@"{prefix}-{n}", baked);
            }

            // The circle and its overlay are in the numbers now.
            foreach (string name in new[] { @"hitcircle", @"hitcircleoverlay" })
            {
                files[$@"{name}.png"] = SkinMixing.TRANSPARENT_PNG;
                files[$@"{name}@2x.png"] = SkinMixing.TRANSPARENT_PNG;
            }

            // Numbers past 9 put side by side, overlapping by a whole number (so only the circle shows once).
            ini.Set(@"Fonts", @"HitCircleOverlap", (width / 2).ToString(CultureInfo.InvariantCulture));

            // One combo colour (the one baked in), the approach circle in it too.
            string baked2 = toIni(colours.Length > 1 ? colours[1] : colours[0]);

            for (int i = 3; i <= 8; i++)
                ini.Remove(@"Colours", $@"Combo{i}");

            ini.Set(@"Colours", @"Combo1", baked2);
            ini.Set(@"Colours", @"Combo2", baked2);
        }

        /// <summary>An element at @2x: the skin's @2x, its 1x doubled, or the classic skin's.</summary>
        private static Image<Rgba32>? loadHd(Dictionary<string, byte[]> files, string name, Func<string, byte[]?> classic)
        {
            if (get(files, $@"{name}@2x.png") is byte[] hd)
                return Image.Load<Rgba32>(hd);

            if (get(files, $@"{name}.png") is byte[] sd)
            {
                var image = Image.Load<Rgba32>(sd);
                image.Mutate(i => i.Resize(image.Width * 2, image.Height * 2));
                return image;
            }

            return classic($@"{name}@2x.png") is byte[] fallback ? Image.Load<Rgba32>(fallback) : null;
        }

        /// <summary>An element's @2x and 1x replaced by the image (at @2x) and its half.</summary>
        private static void replace(Dictionary<string, byte[]> files, string name, Image<Rgba32> hd)
        {
            files[$@"{name}@2x.png"] = png(hd);

            using var sd = hd.Clone(i => i.Resize(Math.Max(1, hd.Width / 2), Math.Max(1, hd.Height / 2)));
            files[$@"{name}.png"] = png(sd);
        }

        private static Rectangle contentRect(Image<Rgba32> image)
        {
            int left = image.Width, top = image.Height, right = -1, bottom = -1;

            image.ProcessPixelRows(rows =>
            {
                for (int y = 0; y < rows.Height; y++)
                {
                    var row = rows.GetRowSpan(y);

                    for (int x = 0; x < row.Length; x++)
                    {
                        if (row[x].A == 0)
                            continue;

                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }
                }
            });

            return right < 0 ? Rectangle.Empty : new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }

        private static byte[] png(Image image)
        {
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            return stream.ToArray();
        }

        private static byte[]? get(Dictionary<string, byte[]> files, string path) =>
            files.FirstOrDefault(f => f.Key.Equals(path, StringComparison.OrdinalIgnoreCase)).Value;

        private static Color? parseColour(string value)
        {
            string[] parts = value.Split(',');

            if (parts.Length < 3)
                return null;

            byte[] rgb = new byte[3];

            for (int i = 0; i < 3; i++)
            {
                if (!byte.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out rgb[i]))
                    return null;
            }

            return Color.FromRgb(rgb[0], rgb[1], rgb[2]);
        }

        private static string toIni(Color colour)
        {
            var c = colour.ToPixel<Rgba32>();
            return $@"{c.R},{c.G},{c.B}";
        }
    }
}

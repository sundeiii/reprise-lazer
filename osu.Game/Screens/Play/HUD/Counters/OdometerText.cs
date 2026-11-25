// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Screens.Play.HUD.Counters
{
    /// <summary>
    /// Reprise: a whole number whose digits roll to their new values, as HubSpot's odometer.js (tosu overlays) does: each digit runs through the
    /// values between (228 → 231 rolls its last digit 8, 9, 0, 1), up for a rise and down for a fall, in 300ms; thousands are set apart with commas.
    /// Each digit shows in a box a line (1em) tall, its glyph set in it as CSS sets a line (the font's own line height centred on it).
    /// </summary>
    public partial class OdometerText : FillFlowContainer
    {
        public const double DURATION = 300;

        // odometer.js: at most (2000ms / (1000 / 30fps)) / 2 values a digit runs through; further, it skips evenly.
        private const int max_values = 30;

        private readonly FontUsage font;
        private readonly float lineHeight;
        private long value;
        private bool shown;
        private int version;

        /// <param name="font">The font.</param>
        /// <param name="lineHeight">The font's line height, in ems (as baked).</param>
        public OdometerText(FontUsage font, float lineHeight)
        {
            this.font = font;
            this.lineHeight = lineHeight;

            AutoSizeAxes = Axes.Both;
            Direction = FillDirection.Horizontal;
        }

        /// <summary>Rolls to a value (at once, the first time or if <paramref name="instant"/>).</summary>
        public void RollTo(long newValue, bool instant = false)
        {
            if (shown && newValue == value && !instant)
                return;

            long old = value;
            value = newValue;

            if (!shown || instant || old == newValue)
            {
                shown = true;
                showStatic(newValue);
                return;
            }

            roll(old, newValue);
        }

        private void showStatic(long number)
        {
            version++;
            Clear();

            string text = number.ToString("#,0", CultureInfo.InvariantCulture);
            foreach (char c in text)
                Add(c == ',' ? mark(",") : column(new[] { c - '0' }, true));
        }

        private void roll(long from, long to)
        {
            int thisVersion = ++version;
            Clear();

            bool up = to > from;
            int digits = Math.Max(digitCount(from), digitCount(to));

            for (int i = 0; i < digits; i++)
            {
                long scale = (long)Math.Pow(10, digits - i - 1);
                long start = from / scale;
                long end = to / scale;

                var frames = framesBetween(start, end).Select(f => (int)Math.Abs(f % 10)).ToList();
                if (!up)
                    frames.Reverse();

                // Thousands: a comma before every third digit from the right.
                if (i > 0 && (digits - i) % 3 == 0)
                    Add(mark(","));

                Add(column(frames, up));
            }

            // Then the plain number (a digit fewer, when it fell under a power of ten).
            Scheduler.AddDelayed(() =>
            {
                if (version == thisVersion)
                    showStatic(value);
            }, DURATION);
        }

        private static List<long> framesBetween(long start, long end)
        {
            long dist = end - start;
            var frames = new List<long>();

            if (Math.Abs(dist) > max_values)
            {
                double step = dist / (double)max_values;

                for (double cur = start; dist > 0 ? cur < end : cur > end; cur += step)
                    frames.Add((long)Math.Round(cur));

                if (frames[^1] != end)
                    frames.Add(end);

                return frames;
            }

            for (long f = start; start <= end ? f <= end : f >= end; f += start <= end ? 1 : -1)
                frames.Add(f);

            return frames;
        }

        private static int digitCount(long number) => Math.Max(1, Math.Abs(number).ToString().Length);

        private float boxHeight => font.Size;

        // A glyph in a box a line tall, as CSS sets it: the font's own line centred on it.
        private OsuSpriteText glyph(string text) => new OsuSpriteText
        {
            Text = text,
            Font = font,
            Y = (boxHeight - lineHeight * font.Size) / 2,
        };

        private Drawable mark(string text) => new Container
        {
            AutoSizeAxes = Axes.X,
            Height = boxHeight,
            Child = glyph(text),
        };

        // A digit's box: as wide as the digit it ends on, its ribbon of digits rolling through it.
        private Drawable column(IReadOnlyList<int> frames, bool up)
        {
            int last = up ? frames[^1] : frames[0];

            var ribbon = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Children = frames.Select(f => new Container
                {
                    AutoSizeAxes = Axes.X,
                    Height = boxHeight,
                    Child = glyph(f.ToString()),
                }).ToArray(),
            };

            float travel = (frames.Count - 1) * boxHeight;

            if (frames.Count > 1)
            {
                ribbon.Y = up ? 0 : -travel;
                ribbon.MoveToY(up ? -travel : 0, DURATION, Easing.Out);
            }

            return new Container
            {
                AutoSizeAxes = Axes.X,
                Height = boxHeight,
                Masking = true,
                Children = new Drawable[]
                {
                    // Sized by the digit it ends on.
                    new Container
                    {
                        AutoSizeAxes = Axes.X,
                        Height = boxHeight,
                        Alpha = 0,
                        AlwaysPresent = true,
                        Child = glyph(last.ToString()),
                    },
                    ribbon,
                },
            };
        }
    }
}

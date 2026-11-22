// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Screens.Play.HUD.Counters
{
    /// <summary>Reprise: a number that counts up (or down) to its new value, as tosu overlays' countUp.js does (half a second, easing out).</summary>
    public partial class CountingSpriteText : OsuSpriteText
    {
        public const double DURATION = 500;

        private readonly BindableDouble shown = new BindableDouble();
        private readonly Func<int, string> format;
        private int target;

        /// <param name="format">How the number's written (else as it is).</param>
        public CountingSpriteText(Func<int, string>? format = null)
        {
            this.format = format ?? (n => n.ToString());
            shown.BindValueChanged(v => Text = this.format((int)Math.Round(v.NewValue)), true);
        }

        /// <summary>Counts to a value (at once, if <paramref name="instant"/>).</summary>
        public void CountTo(int value, bool instant = false)
        {
            if (value == target && !instant)
                return;

            target = value;
            this.TransformBindableTo(shown, value, instant ? 0 : DURATION, Easing.OutExpo);
        }
    }
}

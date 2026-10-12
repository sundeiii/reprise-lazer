// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osuTK;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Where a legacy skin's gameplay HUD is laid out: the window's whole height, at most 16:9 wide (centred), at stable's size whatever
    /// the UI scale. Legacy skins are made for 4:3 to 16:9 (some as one full-screen image framing the playfield, with the score and the
    /// rest in its boxes), so in wider windows everything still lines up, with margins at the sides.
    /// </summary>
    public static class StableHudArea
    {
        public const float ASPECT_RATIO = 16f / 9;

        /// <summary>
        /// The area in <paramref name="available"/>: its whole height, at most 16:9 wide (stable's layouts are made for 4:3 to 16:9).
        /// </summary>
        public static Vector2 Fit(Vector2 available) =>
            new Vector2(Math.Min(available.X, available.Y * ASPECT_RATIO), available.Y);

        /// <summary>
        /// Puts <paramref name="drawable"/> (sized relative to its parent) in the area, undoing <paramref name="uiScale"/>; with
        /// <paramref name="active"/> false, back to filling its parent.
        /// </summary>
        public static void Apply(Drawable drawable, bool active, float uiScale)
        {
            drawable.RelativeSizeAxes = Axes.Both;
            drawable.Anchor = Anchor.Centre;
            drawable.Origin = Anchor.Centre;

            if (!active || drawable.Parent == null || uiScale <= 0)
            {
                drawable.Scale = Vector2.One;
                drawable.Size = Vector2.One;
                return;
            }

            var available = drawable.Parent.ChildSize;

            if (available.X <= 0 || available.Y <= 0)
                return;

            float scale = 1 / uiScale;
            var area = Fit(available);

            drawable.Scale = new Vector2(scale);
            drawable.Size = new Vector2(area.X / available.X, area.Y / available.Y) / scale;
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;

namespace osu.Game.Skinning
{
    /// <summary>
    /// osu!stable's font: Aller (its own, made into the game's font atlases), with its sizes.
    /// </summary>
    /// <remarks>
    /// Stable's text is "Aller Light" by default, "Aller" under 20 (its size times the screen's height over 768: at 1080p under
    /// about 14.2, at 768 under 20) and "Aller" bold when bold. Its sizes are points: times 4/3 in a layout 768 high.
    /// </remarks>
    public static class StableFont
    {
        /// <param name="screenHeight">The window's height (stable picks the weight by the size on the screen).</param>
        public static FontUsage Get(float size, bool bold = false, float screenHeight = 1080)
        {
            string weight = bold ? "Bold" : size * screenHeight / 768 < 20 ? "Regular" : "Light";
            return new FontUsage("Aller", size * 4 / 3, weight);
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;

namespace osu.Game.Screens.Play.HUD.Counters
{
    /// <summary>
    /// Reprise: the fonts the tosu pp counters are drawn in, baked into the game (ASCII only, sized so a font size is its CSS font size):
    /// DarkAndWhite's Roboto (Medium, its weight 500), and Simplistic's Arial: Windows' own, baked from the player's copy when the game
    /// starts (it can't be shipped), else Arimo, its free, metric-identical twin. Not in <see cref="osu.Game.Graphics.Typeface"/>, so the skin editor's font lists don't offer them with weights they lack.
    /// </summary>
    public static class TosuFonts
    {
        public static FontUsage RobotoMedium(float size) => new FontUsage(@"Roboto", size, @"Medium");

        /// <summary>Whether Windows' own Arial was baked from this computer's copy when the game started (else Arimo stands in).</summary>
        public static bool ArialInstalled { get; set; }

        public static FontUsage Arial(float size, bool bold = false) => new FontUsage(ArialInstalled ? @"Arial" : @"Arimo", size, bold ? @"Bold" : @"Regular");
    }
}

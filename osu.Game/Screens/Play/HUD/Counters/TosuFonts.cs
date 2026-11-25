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

        /// <summary>BTMC's Baloo 2 (Bold, its main font; Medium, its difficulty names).</summary>
        public static FontUsage BalooBold(float size) => new FontUsage(@"Baloo2", size, @"Bold");

        public static FontUsage BalooMedium(float size) => new FontUsage(@"Baloo2", size, @"Medium");

        /// <summary>Baloo 2's line heights, in ems (as baked): CSS sets a line of text with its line height centred on the line.</summary>
        public const float BALOO_BOLD_LINE_HEIGHT = 1.61f;

        public const float BALOO_MEDIUM_LINE_HEIGHT = 1.58f;
    }
}

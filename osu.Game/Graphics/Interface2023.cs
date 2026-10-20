// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using osu.Game.Overlays;

namespace osu.Game.Graphics
{
    /// <summary>
    /// Reprise: the interface as lazer had it in 2023 (<see cref="OsuSetting.Interface2023"/>): the main menu's icons (2023's icon font,
    /// Resources/Fonts2023) and its edit button going straight to the editor, the toolbar's hexagon icons (Resources/Textures/Icons2023,
    /// from osu!'s resources of 2023.417.0), 2023's tips, and the signed-in panel's user card. Read when the game starts.
    /// </summary>
    public static class Interface2023
    {
        /// <summary>Whether the 2023 interface is in use (read once, when the game starts).</summary>
        public static bool Enabled { get; private set; }

        public static void Apply(bool enabled) => Enabled = enabled;

        /// <summary>The 2023 icon font's family (its icons are gone from today's).</summary>
        public const string FONT = @"osuFont";

        // The main menu's icons in 2023's icon font.
        public static IconUsage CrossCircle => new IconUsage((char)0xe005, FONT);
        public static IconUsage Logo => new IconUsage((char)0xe006, FONT);
        public static IconUsage ChevronDownCircle => new IconUsage((char)0xe007, FONT);
        public static IconUsage EditCircle => new IconUsage((char)0xe033, FONT);
        public static IconUsage LeftCircle => new IconUsage((char)0xe034, FONT);
        public static IconUsage Charts => new IconUsage((char)0xe036, FONT);

        /// <summary><paramref name="then"/> with the 2023 interface, else <paramref name="now"/>.</summary>
        public static T Pick<T>(T now, T then) => Enabled ? then : now;

        /// <summary>The toolbar's 2023 hexagon icon for an overlay ("news", ...), or null where it had none.</summary>
        public static string? HexaconFor(object overlay) => overlay switch
        {
            NewsOverlay => @"news",
            ChangelogOverlay => @"devtools",
            WikiOverlay => @"wiki",
            RankingsOverlay => @"rankings",
            BeatmapListingOverlay => @"beatmap",
            ChatOverlay => @"messaging",
            DashboardOverlay => @"social",
            NowPlayingOverlay => @"music",
            SettingsOverlay => @"settings",
            NotificationOverlay => @"notification",
            _ => null,
        };

        /// <summary>A 2023 hexagon icon ("home", "news", ...), as the toolbar showed them.</summary>
        public partial class Hexacon : Sprite
        {
            private readonly string name;

            public Hexacon(string name)
            {
                this.name = name;
                FillMode = FillMode.Fit;
            }

            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                Texture = textures.Get($@"Icons2023/Hexacons/{name}");
            }
        }
    }
}

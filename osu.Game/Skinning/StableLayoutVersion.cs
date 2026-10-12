// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Game.Configuration;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Which osu!stable the stable layouts look like.
    /// </summary>
    public enum StableLayoutVersion
    {
        /// <summary>
        /// By the skin: <see cref="Later"/> for skins with their own song select top or bottom (they're made for the later stable),
        /// else <see cref="Stable2015"/>.
        /// </summary>
        [Description("Automatic")]
        Automatic,

        /// <summary>
        /// Stable's 2015 client (b20151228): song select's top and bottom are always stable's own, with its choice boxes and Aller.
        /// </summary>
        [Description("2015")]
        Stable2015,

        /// <summary>
        /// The later stable: skins draw song select's top and bottom ("songselect-top", "songselect-bottom").
        /// </summary>
        [Description("Later")]
        Later,
    }

    public static class StableLayoutVersionExtensions
    {
        /// <summary>
        /// Whether the stable layouts look like 2015's with this skin (<see cref="StableLayoutVersion.Automatic"/> decides by the skin).
        /// </summary>
        public static bool Is2015(this OsuConfigManager config, ISkin skin)
        {
            switch (config.Get<StableLayoutVersion>(OsuSetting.StableLayoutVersion))
            {
                case StableLayoutVersion.Stable2015:
                    return true;

                case StableLayoutVersion.Later:
                    return false;

                default:
                    return skin.GetTexture(@"songselect-top") == null && skin.GetTexture(@"songselect-bottom") == null;
            }
        }
    }
}

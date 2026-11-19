// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Overlays.LazerImport;

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>
    /// Reprise: skins from osu!'s own lazer on this computer (<see cref="LazerSkinImportOverlay"/>).
    /// </summary>
    public partial class ImportLazerSkinsButton : SettingsButtonV2
    {
        [Resolved]
        private LazerSkinImportOverlay? overlay { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            Text = "Import skins from osu!lazer...";
            Action = () => overlay?.Show();
        }
    }
}

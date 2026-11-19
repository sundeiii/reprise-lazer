// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;

namespace osu.Game.Overlays.LazerImport
{
    /// <summary>
    /// Reprise: skins from osu!'s own lazer on this computer (from the skin settings).
    /// </summary>
    [Cached]
    [Cached(typeof(LazerImportOverlay))]
    public partial class LazerSkinImportOverlay : LazerImportOverlay
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            AddStep<ScreenImportLazerSkins>();

            Header.Title = "import skins";
            Header.Description = "bring your skins over from osu!lazer";
        }
    }
}

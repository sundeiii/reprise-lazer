// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;

namespace osu.Game.Overlays.LazerImport
{
    /// <summary>
    /// Reprise: beatmaps from osu!'s own lazer on this computer (from the maintenance settings).
    /// </summary>
    [Cached]
    [Cached(typeof(LazerImportOverlay))]
    public partial class LazerBeatmapImportOverlay : LazerImportOverlay
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            AddStep<ScreenImportLazerBeatmaps>();

            Header.Title = "import beatmaps";
            Header.Description = "bring your beatmaps over from osu!lazer";
        }
    }
}

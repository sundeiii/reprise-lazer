// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;

namespace osu.Game.Overlays.LazerImport
{
    /// <summary>
    /// Reprise: collections from osu!'s own lazer on this computer (from the maintenance settings).
    /// </summary>
    [Cached]
    [Cached(typeof(LazerImportOverlay))]
    public partial class LazerCollectionImportOverlay : LazerImportOverlay
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            AddStep<ScreenImportLazerCollections>();

            Header.Title = "import collections";
            Header.Description = "bring your collections over from osu!lazer";
        }
    }
}

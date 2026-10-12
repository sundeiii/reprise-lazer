// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>
    /// The website's beatmap listing views (osu!'s search design proposal): "Extra" is the game's own extra card, the rest are the website's cards.
    /// </summary>
    public enum BeatmapListingView
    {
        Nano,
        Mini,
        Normal,
        Extra,
        Cover,
        List,
    }
}

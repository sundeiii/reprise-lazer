// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Beatmaps;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Reprise: a map's artist and title as osu!stable's screens write them ("Artist - Title"), in the original
    /// language or romanised, following the "Prefer metadata in original language" setting like the rest of lazer.
    /// </summary>
    public static class StableMetadata
    {
        /// <summary>The artist, or its original when there is one and the setting prefers it.</summary>
        public static RomanisableString Artist(IBeatmapMetadataInfo metadata) => new RomanisableString(original(metadata.ArtistUnicode, metadata.Artist), metadata.Artist);

        /// <summary>The title, or its original when there is one and the setting prefers it.</summary>
        public static RomanisableString Title(IBeatmapMetadataInfo metadata) => new RomanisableString(original(metadata.TitleUnicode, metadata.Title), metadata.Title);

        /// <summary>"<paramref name="before"/>Artist - Title<paramref name="after"/>", in either language.</summary>
        public static RomanisableString ArtistTitle(IBeatmapMetadataInfo metadata, string before = "", string after = "") => new RomanisableString(
            $"{before}{original(metadata.ArtistUnicode, metadata.Artist)} - {original(metadata.TitleUnicode, metadata.Title)}{after}",
            $"{before}{metadata.Artist} - {metadata.Title}{after}");

        private static string original(string? unicode, string romanised) => string.IsNullOrEmpty(unicode) ? romanised : unicode;
    }
}

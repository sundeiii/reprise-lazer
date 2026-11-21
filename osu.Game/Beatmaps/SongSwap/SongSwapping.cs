// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using ManagedBass;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;

namespace osu.Game.Beatmaps.SongSwap
{
    /// <summary>Matching and rendering a replacement song without modifying the beatmap's objects or timing.</summary>
    public static class SongSwapping
    {
        /// <summary>
        /// Matches the nearest musical pulse (quarter, half, same, double or quadruple BPM), with the least
        /// proportional speed change. The factor changes the pulse interpretation, not the playback speed.
        /// </summary>
        public static (double Factor, double Stretch)? Fit(double mapBpm, double songBpm)
        {
            if (!double.IsFinite(mapBpm) || !double.IsFinite(songBpm) || mapBpm <= 0 || songBpm <= 0)
                return null;

            var best = new[] { 1, 0.5, 2, 0.25, 4 }
                       .Select(f => (Factor: f, Stretch: mapBpm / songBpm / f))
                       .OrderBy(f => Math.Abs(Math.Log(f.Stretch)))
                       .First();

            // BASS FX supports tempo changes from -95% to +5000%.
            return best.Stretch >= 0.05 && best.Stretch <= 51 ? best : null;
        }

        /// <summary>The first object that isn't a spinner, or the first object if the map only contains spinners.</summary>
        public static double FirstObjectTime(IBeatmap beatmap) =>
            (beatmap.HitObjects.FirstOrDefault(h => !IsSpinner(h)) ?? beatmap.HitObjects.FirstOrDefault())?.StartTime ?? 0;

        // A spinner (osu!'s, taiko's swell, catch's banana shower) isn't a useful musical onset. A map read from its file (as song swap
        // reads them) has legacy objects, which say what they are by their legacy type.
        internal static bool IsSpinner(HitObject h) =>
            h is IHasLegacyHitObjectType legacy ? legacy.LegacyType.HasFlag(LegacyHitObjectType.Spinner) : h.GetType().Name is "Spinner" or "Swell" or "BananaShower";

        /// <summary>When a map's play ends: its last object's end.</summary>
        public static double EndTime(IBeatmap beatmap) => beatmap.HitObjects.Count == 0 ? 0 : beatmap.HitObjects.Max(h => h.GetEndTime());

        /// <summary>A conservative duration estimate for the list; the full timing plan checks actual audio before applying a swap.</summary>
        public static bool LongEnough(double songLength, double stretch, double mapPlayLength) => songLength / stretch >= mapPlayLength;

        /// <summary>How long a song is (ms), as BASS reads it (which must be set up).</summary>
        public static double LengthOf(byte[] song)
        {
            int decode = Bass.CreateStream(song, 0, song.Length, BassFlags.Decode | BassFlags.Prescan);
            if (decode == 0)
                throw new InvalidOperationException($"The song couldn't be read ({Bass.LastError}).");

            try
            {
                return Bass.ChannelBytes2Seconds(decode, Bass.ChannelGetLength(decode)) * 1000;
            }
            finally
            {
                Bass.StreamFree(decode);
            }
        }

        public const double TAIL = 2000;
        public const double FADE_OUT = 3000;
        public const double FADE_IN = 800;

        /// <summary>Renders the timing plan with pitch preserved, short joins between tempo sections, and intro/outro fades.</summary>
        public static byte[] Render(byte[] song, SongSwapTiming.Plan plan, double mapEnd) => Render(song, plan, mapEnd, new SongSwapRenderOptions());

        /// <summary>Renders the timing plan (all of it, or a preview's window), crossfading its joins and fading in and out.</summary>
        public static byte[] Render(byte[] song, SongSwapTiming.Plan plan, double mapEnd, SongSwapRenderOptions options) => SongSwapAudio.Render(song, plan, mapEnd, options);

        /// <summary>Renders a song at a constant speed and offset.</summary>
        public static byte[] Render(byte[] song, double stretch, double offset, double mapEnd) =>
            Render(song, new SongSwapTiming.Plan(new[] { new SongSwapTiming.Section(0, LengthOf(song), offset, stretch) }), mapEnd);
    }
}

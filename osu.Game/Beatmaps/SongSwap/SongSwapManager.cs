// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Extensions;

namespace osu.Game.Beatmaps.SongSwap
{
    /// <summary>
    /// Reprise: puts a swapped song in place of a beatmap set's own (under the same name, so the maps themselves, and their scores, are
    /// untouched), keeping the original to put back.
    /// </summary>
    public class SongSwapManager
    {
        private readonly BeatmapManager beatmaps;
        private readonly Storage files;
        private readonly Storage backups;

        public SongSwapManager(BeatmapManager beatmaps, Storage storage)
        {
            this.beatmaps = beatmaps;
            files = storage.GetStorageForDirectory(@"files");
            backups = storage.GetStorageForDirectory(@"song-swaps");
            VocalRemoval = new SongSwapVocalRemoval(backups);
        }

        /// <summary>Reprise: takes the vocals out of songs swapped in as instrumentals.</summary>
        public SongSwapVocalRemoval VocalRemoval { get; }

        private static string backupPath(BeatmapSetInfo set, string audioFile) => $@"{set.ID}/{audioFile.Replace('/', '_').Replace('\\', '_')}";

        private static string notePath(BeatmapSetInfo set) => $@"{set.ID}/swapped-with.txt";

        /// <summary>Whether the set's song is swapped (its own kept to put back).</summary>
        public bool IsSwapped(BeatmapSetInfo set) => backups.Exists(notePath(set));

        /// <summary>What the set's song is swapped with ("Artist - Title"), if it is.</summary>
        public string? SwappedWith(BeatmapSetInfo set)
        {
            if (!IsSwapped(set))
                return null;

            using var stream = backups.GetStream(notePath(set));
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// A file of the set, read (found in the database: a detached set has no files listed).
        /// </summary>
        public byte[] ReadFile(BeatmapSetInfo set, string filename)
        {
            string path = storagePath(set, filename) ?? throw new FileNotFoundException(filename);

            using var stream = files.GetStream(path);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        /// <summary>Whether <paramref name="song"/> is <paramref name="map"/>'s own song (its set's, the same file).</summary>
        public static bool IsOwnSong(BeatmapInfo map, BeatmapInfo song) =>
            map.BeatmapSet != null && song.BeatmapSet?.ID == map.BeatmapSet.ID
                                   && string.Equals(song.Metadata.AudioFile, map.Metadata.AudioFile, StringComparison.OrdinalIgnoreCase);

        /// <summary>A song made to swap in (see <see cref="Prepare"/>), ready to put in place (see <see cref="Apply"/>).</summary>
        public record PreparedSwap(BeatmapSetInfo Set, string AudioFile, byte[] Song, byte[]? Original, string SongName);

        /// <summary>
        /// Swaps <paramref name="map"/>'s song for <paramref name="song"/>'s: <see cref="Prepare"/> then <see cref="Apply"/>, both here (on the
        /// update thread; slow).
        /// </summary>
        public void Swap(BeatmapInfo map, BeatmapInfo song, bool loop = true, bool keepPitch = true, bool instrumental = false) =>
            Apply(Prepare(map, song, loop, keepPitch, instrumental));

        /// <summary>
        /// Puts a prepared song in place of the set's own, keeping its own to put back. On the update thread (it writes to the database).
        /// </summary>
        public void Apply(PreparedSwap swap)
        {
            // The set's own song kept (only the first time: a swap of a swap keeps the original).
            if (swap.Original != null && !IsSwapped(swap.Set))
            {
                using (var backup = backups.CreateFileSafely(backupPath(swap.Set, swap.AudioFile)))
                    backup.Write(swap.Original);
            }

            using (var note = backups.CreateFileSafely(notePath(swap.Set)))
                note.Write(System.Text.Encoding.UTF8.GetBytes(swap.SongName));

            replaceSong(swap.Set, swap.AudioFile, swap.Song);
        }

        /// <summary>
        /// Makes <paramref name="song"/>'s song ready to swap in for <paramref name="map"/>'s (stretched to fit and lined up, see
        /// <see cref="SongSwapping"/>). Slow (it reads and stretches the whole song): in the background.
        /// </summary>
        /// <param name="loop">Whether a song too short for the map's looped (else it's refused).</param>
        /// <param name="keepPitch">Whether a stretched song keeps its pitch (else it changes with its speed, like a record).</param>
        /// <param name="instrumental">Whether the song's vocals are taken out (see <see cref="SongSwapVocalRemoval"/>).</param>
        /// <param name="progress">How far taking the vocals out is (0 to 1), when it has to be done.</param>
        public PreparedSwap Prepare(BeatmapInfo map, BeatmapInfo song, bool loop = true, bool keepPitch = true, bool instrumental = false,
                                    Action<double>? progress = null)
        {
            var mapSet = map.BeatmapSet ?? throw new InvalidOperationException("The map has no set.");
            string audioFile = map.Metadata.AudioFile;

            if (string.IsNullOrEmpty(audioFile) || storagePath(mapSet, audioFile) == null)
                throw new InvalidOperationException("The map has no song of its own to swap.");

            // The map's own song, its vocals out: just its instrumental (the same length and timing, so nothing to line up).
            if (IsOwnSong(map, song))
            {
                if (!instrumental)
                    throw new InvalidOperationException("That's the map's own song already.");

                byte[] own = VocalRemoval.Instrumental(readOriginalSong(mapSet, audioFile), progress);
                byte[]? ownOriginal = IsSwapped(mapSet) ? null : ReadFile(mapSet, audioFile);

                return new PreparedSwap(mapSet, audioFile, own, ownOriginal, $@"{song.Metadata.Artist} - {song.Metadata.Title} (instrumental)");
            }

            var (mapBeatmap, songData, plan) = makePlan(map, song, loop, instrumental, progress);
            double mapEnd = SongSwapping.EndTime(mapBeatmap);
            var options = new SongSwapRenderOptions(keepPitch, FirstNote: SongSwapping.FirstObjectTime(mapBeatmap));
            var joined = plan.Joined();
            byte[] rendered = SongSwapping.Render(songData, joined, mapEnd, options);

            // Moved (part by part) to sit on the map's beats as the map's own song does, then made again there (if it moved).
            var matched = matchFeel(mapBeatmap, readOriginalSong(mapSet, audioFile), rendered, 0, plan, out _).Joined();
            if (!matched.SameAs(joined))
                rendered = SongSwapping.Render(songData, matched, mapEnd, options);

            // The set's own song, to keep (unless it's swapped already: that one's kept already).
            byte[]? original = IsSwapped(mapSet) ? null : ReadFile(mapSet, audioFile);

            return new PreparedSwap(mapSet, audioFile, rendered, original,
                $@"{song.Metadata.Artist} - {song.Metadata.Title}{(instrumental ? " (instrumental)" : string.Empty)}");
        }

        /// <summary>A short preview of a swap (as a wave file): the map's best part, with a tick on each of its notes.</summary>
        /// <param name="Shift">How far (ms) it was moved to sit on the map's beats as the map's own song does.</param>
        public record Preview(byte[] Song, SongSwapTiming.Anchor By, int Loops, double Shift);

        /// <summary>
        /// About 20 seconds of <paramref name="map"/> played to <paramref name="song"/>, from just before its kiai (else its preview point, else
        /// its first note), with its notes ticked over it to hear how it lines up. Much quicker than <see cref="Prepare"/>: in the background.
        /// </summary>
        public Preview RenderPreview(BeatmapInfo map, BeatmapInfo song, bool loop = true, bool keepPitch = true, bool instrumental = false,
                                     Action<double>? progress = null)
        {
            if (IsOwnSong(map, song))
            {
                // Its instrumental as it is, at the map's best part.
                var ownMap = beatmaps.GetWorkingBeatmap(map).Beatmap;
                var ownSet = map.BeatmapSet ?? throw new InvalidOperationException("The map has no set.");
                byte[] own = instrumental ? VocalRemoval.Instrumental(readOriginalSong(ownSet, map.Metadata.AudioFile), progress) : readOriginalSong(ownSet, map.Metadata.AudioFile);
                double start = Math.Max(0, SongSwapTiming.HighlightTime(ownMap) - 2000);
                var same = new SongSwapTiming.Plan(new[] { new SongSwapTiming.Section(0, SongSwapping.LengthOf(own), 0, 1) });
                var ownTicks = ownMap.HitObjects.Where(h => !SongSwapping.IsSpinner(h)).Select(h => h.StartTime).ToList();
                var ownOptions = new SongSwapRenderOptions(keepPitch, SongSwapping.FirstObjectTime(ownMap), start, start + 20000) { Ticks = ownTicks };

                return new Preview(SongSwapping.Render(own, same, SongSwapping.EndTime(ownMap), ownOptions), SongSwapTiming.Anchor.FirstNotes, 0, 0);
            }

            var (mapBeatmap, songData, plan) = makePlan(map, song, loop, instrumental, progress);
            double mapEnd = SongSwapping.EndTime(mapBeatmap);
            double from = Math.Max(0, SongSwapTiming.HighlightTime(mapBeatmap) - 2000);
            var ticks = mapBeatmap.HitObjects.Where(h => !SongSwapping.IsSpinner(h)).Select(h => h.StartTime).ToList();
            var options = new SongSwapRenderOptions(keepPitch, SongSwapping.FirstObjectTime(mapBeatmap), from, from + 30000);

            // Measured over a little more than is heard (without the ticks), then made again where it sits as the map's own song does.
            byte[] probe = SongSwapping.Render(songData, plan.Joined(), mapEnd, options);
            var mapSet = map.BeatmapSet ?? throw new InvalidOperationException("The map has no set.");
            var matched = matchFeel(mapBeatmap, readOriginalSong(mapSet, map.Metadata.AudioFile), probe, from, plan, out double shift).Joined();

            byte[] rendered = SongSwapping.Render(songData, matched, mapEnd, options with { To = from + 20000, Ticks = ticks });

            return new Preview(rendered, plan.By, plan.Loops, shift);
        }

        // The plan moved to sit on the map's beats as the map's own song does (see SongSwapAnalysis.MatchFeel).
        private static SongSwapTiming.Plan matchFeel(IBeatmap mapBeatmap, byte[] mapAudio, byte[] rendered, double renderedFrom, SongSwapTiming.Plan plan,
                                                     out double overall) =>
            SongSwapAnalysis.MatchFeel(plan, mapBeatmap, SongSwapAnalysis.OnsetStrength(mapAudio), SongSwapAnalysis.OnsetStrength(rendered), renderedFrom, out overall);

        private (IBeatmap map, byte[] songData, SongSwapTiming.Plan plan) makePlan(BeatmapInfo map, BeatmapInfo song, bool loop, bool instrumental,
                                                                                   Action<double>? progress)
        {
            var songSet = song.BeatmapSet ?? throw new InvalidOperationException("The song's map has no set.");
            var mapBeatmap = beatmaps.GetWorkingBeatmap(map).Beatmap;
            var songBeatmap = beatmaps.GetWorkingBeatmap(song).Beatmap;

            // A previously swapped set still has its original timing data. Use its original audio as the source as well.
            byte[] songData = readOriginalSong(songSet, song.Metadata.AudioFile);

            // Its instrumental instead (the same length, so its timing is the same).
            if (instrumental)
                songData = VocalRemoval.Instrumental(songData, progress);

            return (mapBeatmap, songData, SongSwapTiming.Create(mapBeatmap, songBeatmap, SongSwapping.LengthOf(songData), loop));
        }

        private byte[] readOriginalSong(BeatmapSetInfo set, string filename)
        {
            string path = backupPath(set, filename);
            if (!backups.Exists(path))
                return ReadFile(set, filename);

            using var stream = backups.GetStream(path);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        /// <summary>Puts the set's own song back.</summary>
        public void Restore(BeatmapSetInfo set)
        {
            if (!IsSwapped(set))
                return;

            string? audioFile = set.Beatmaps.Select(b => b.Metadata.AudioFile).FirstOrDefault(f => !string.IsNullOrEmpty(f) && backups.Exists(backupPath(set, f)));

            if (audioFile != null)
            {
                byte[] original;

                using (var stream = backups.GetStream(backupPath(set, audioFile)))
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    original = memory.ToArray();
                }

                replaceSong(set, audioFile, original);
                backups.Delete(backupPath(set, audioFile));
            }

            backups.Delete(notePath(set));
        }

        // Where a file of the set's stored, from the database (by the set's ID).
        private string? storagePath(BeatmapSetInfo set, string filename) =>
            beatmaps.QueryBeatmapSet(s => s.ID == set.ID)?.PerformRead(s => s.GetFile(filename)?.File.GetStoragePath());

        private void replaceSong(BeatmapSetInfo set, string audioFile, byte[] data)
        {
            // Under its own name: the maps (their .osu files and hashes) stay as they are, so they're still ranked and their scores count.
            // AddFile synchronises the supplied model's Files in place. It may be the currently playing working beatmap's
            // snapshot: changing that snapshot would make TryTransferTrack mistake its old audio for the replacement.
            // Write through a separate model so the music controller can compare the old and new file hashes correctly.
            var destination = new BeatmapSetInfo { ID = set.ID };
            using var contents = new MemoryStream(data);
            beatmaps.AddFile(destination, contents, audioFile);
            ((IWorkingBeatmapCache)beatmaps).Invalidate(set);
        }
    }
}

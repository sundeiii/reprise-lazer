// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets.Objects;

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
        }

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

        /// <summary>A song made to swap in (see <see cref="Prepare"/>), ready to put in place (see <see cref="Apply"/>).</summary>
        public record PreparedSwap(BeatmapSetInfo Set, string AudioFile, byte[] Song, byte[]? Original, string SongName);

        /// <summary>
        /// Swaps <paramref name="map"/>'s song for <paramref name="song"/>'s: <see cref="Prepare"/> then <see cref="Apply"/>, both here (on the
        /// update thread; slow).
        /// </summary>
        public void Swap(BeatmapInfo map, BeatmapInfo song) => Apply(Prepare(map, song));

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
        public PreparedSwap Prepare(BeatmapInfo map, BeatmapInfo song)
        {
            var mapSet = map.BeatmapSet ?? throw new InvalidOperationException("The map has no set.");
            var songSet = song.BeatmapSet ?? throw new InvalidOperationException("The song's map has no set.");

            string audioFile = map.Metadata.AudioFile;

            if (string.IsNullOrEmpty(audioFile) || storagePath(mapSet, audioFile) == null)
                throw new InvalidOperationException("The map has no song of its own to swap.");

            var mapBeatmap = beatmaps.GetWorkingBeatmap(map).Beatmap;
            var songBeatmap = beatmaps.GetWorkingBeatmap(song).Beatmap;

            var fit = SongSwapping.Fit(map.BPM, song.BPM) ?? throw new InvalidOperationException("The songs' BPMs don't fit.");

            double offset = SongSwapping.Offset(mapBeatmap, songBeatmap, fit.Stretch);
            double length = Math.Max(map.Length, mapBeatmap.HitObjects.LastOrDefault()?.GetEndTime() ?? 0) + 3000;

            byte[] rendered = SongSwapping.Render(ReadFile(songSet, song.Metadata.AudioFile), fit.Stretch, offset, length);

            // The set's own song, to keep (unless it's swapped already: that one's kept already).
            byte[]? original = IsSwapped(mapSet) ? null : ReadFile(mapSet, audioFile);

            return new PreparedSwap(mapSet, audioFile, rendered, original, $@"{song.Metadata.Artist} - {song.Metadata.Title}");
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
            beatmaps.AddFile(set, new MemoryStream(data), audioFile);
            ((IWorkingBeatmapCache)beatmaps).Invalidate(set);
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Overlays.LazerImport
{
    /// <summary>
    /// Reprise: osu!lazer's collections (here already: ours by the same name with all its beatmaps), each added as a collection of ours, or
    /// what's missing added to ours of the same name. Collections keep their beatmaps by hash, so ones not here yet show up once they are.
    /// </summary>
    [LocalisableDescription(typeof(ScreenImportLazerCollections), nameof(Title))]
    public partial class ScreenImportLazerCollections : LazerImportScreen
    {
        public static LocalisableString Title => "Pick the collections to bring over";

        protected override string Noun => "collection";
        protected override string NounPlural => "collections";

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        protected override IReadOnlyList<Entry> ReadEntries(LazerDataSource source)
        {
            var (ours, hashes) = realm.Run(r =>
            {
                var collections = r.All<BeatmapCollection>().AsEnumerable()
                                   .GroupBy(c => c.Name)
                                   .ToDictionary(g => g.Key, g => g.SelectMany(c => c.BeatmapMD5Hashes).ToHashSet());

                return (collections, r.All<BeatmapSetInfo>().Where(s => !s.DeletePending).AsEnumerable().SelectMany(s => s.Beatmaps).Select(b => b.MD5Hash).ToHashSet());
            });

            return source.ReadCollections()
                         .Select(c =>
                         {
                             int here = c.MD5Hashes.Count(hashes.Contains);
                             string beatmaps = $"{c.MD5Hashes.Count} beatmap{(c.MD5Hashes.Count == 1 ? string.Empty : "s")}";

                             return new Entry(c, c.Name, here == c.MD5Hashes.Count ? beatmaps : $"{beatmaps}, {here} of them here",
                                 ours.TryGetValue(c.Name, out var ourHashes) && c.MD5Hashes.All(ourHashes.Contains));
                         })
                         .ToList();
        }

        protected override Task Import(LazerDataSource source, IReadOnlyList<object> items, ProgressNotification notification)
        {
            var selected = items.Cast<LazerDataSource.LazerCollection>().ToList();
            int added = 0, merged = 0;

            realm.Write(r =>
            {
                foreach (var collection in selected)
                {
                    var ours = r.All<BeatmapCollection>().FirstOrDefault(c => c.Name == collection.Name);

                    if (ours == null)
                    {
                        r.Add(new BeatmapCollection(collection.Name, collection.MD5Hashes.ToList()));
                        added++;
                        continue;
                    }

                    var missing = collection.MD5Hashes.Except(ours.BeatmapMD5Hashes).ToList();

                    if (missing.Count == 0)
                        continue;

                    foreach (string hash in missing)
                        ours.BeatmapMD5Hashes.Add(hash);

                    ours.LastModified = DateTimeOffset.UtcNow;
                    merged++;
                }
            });

            var parts = new List<string>();

            if (added > 0)
                parts.Add($"Brought {CountOf(added)} over from osu!lazer.");
            if (merged > 0)
                parts.Add($"Added the missing beatmaps to {CountOf(merged)} of yours with the same name.");

            notification.CompletionText = parts.Count > 0 ? string.Join(' ', parts) : "Those collections are all here already.";
            notification.State = ProgressNotificationState.Completed;
            return Task.CompletedTask;
        }
    }
}

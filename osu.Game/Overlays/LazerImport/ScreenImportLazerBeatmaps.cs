// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Overlays.LazerImport
{
    /// <summary>
    /// Reprise: osu!lazer's beatmaps (here already: ours with the same online id, or a difficulty with the same hash), imported one set at a
    /// time as osu!lazer exports them (so a big pick doesn't take its size twice over in the temporary folder).
    /// </summary>
    [LocalisableDescription(typeof(ScreenImportLazerBeatmaps), nameof(Title))]
    public partial class ScreenImportLazerBeatmaps : LazerImportScreen
    {
        public static LocalisableString Title => "Pick the beatmaps to bring over";

        protected override string Noun => "beatmap";
        protected override string NounPlural => "beatmaps";

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        protected override IReadOnlyList<Entry> ReadEntries(LazerDataSource source)
        {
            var (onlineIds, hashes) = realm.Run(r =>
            {
                var sets = r.All<BeatmapSetInfo>().Where(s => !s.DeletePending).AsEnumerable().ToList();
                return (sets.Where(s => s.OnlineID > 0).Select(s => s.OnlineID).ToHashSet(),
                    sets.SelectMany(s => s.Beatmaps).Select(b => b.MD5Hash).ToHashSet());
            });

            return source.ReadBeatmapSets()
                         .Select(s => new Entry(s, s.DisplayTitle,
                             $"mapped by {(string.IsNullOrWhiteSpace(s.Mapper) ? "unknown" : s.Mapper)} · {s.MD5Hashes.Count} difficult{(s.MD5Hashes.Count == 1 ? "y" : "ies")}",
                             (s.OnlineID > 0 && onlineIds.Contains(s.OnlineID)) || s.MD5Hashes.Any(hashes.Contains)))
                         .ToList();
        }

        protected override async Task Import(LazerDataSource source, IReadOnlyList<object> items, ProgressNotification notification)
        {
            var selected = items.Cast<LazerDataSource.LazerBeatmapSet>().ToList();
            string folder = Path.Combine(Path.GetTempPath(), $@"reprise-lazer-beatmaps-import-{Guid.NewGuid():N}");
            var parameters = new ImportParameters { Batch = selected.Count >= 10 };
            int imported = 0;

            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    if (notification.CancellationToken.IsCancellationRequested)
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        return;
                    }

                    notification.Text = $"Bringing beatmaps over from osu!lazer ({i + 1} of {selected.Count})...";

                    string? path = null;

                    try
                    {
                        path = source.WriteOsz(selected[i], folder);

                        if (await beatmaps.Import(new ImportTask(path), parameters, notification.CancellationToken).ConfigureAwait(false) != null)
                            imported++;
                    }
                    catch (OperationCanceledException) when (notification.CancellationToken.IsCancellationRequested)
                    {
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Couldn't bring {selected[i]} over from osu!lazer");
                    }
                    finally
                    {
                        // Gone as it goes (the import may have deleted it already).
                        if (path != null)
                            DeleteTemporaryFolder(Path.GetDirectoryName(path)!);
                    }

                    notification.Progress = (float)(i + 1) / selected.Count;
                }

                if (imported == 0)
                {
                    notification.Text = "Couldn't bring the beatmaps over from osu!lazer. Check the logs for more information.";
                    notification.State = ProgressNotificationState.Cancelled;
                    return;
                }

                notification.CompletionText = imported == selected.Count
                    ? $"Brought {CountOf(imported)} over from osu!lazer."
                    : $"Brought {imported} of {CountOf(selected.Count)} over from osu!lazer.";
                notification.State = ProgressNotificationState.Completed;
            }
            finally
            {
                DeleteTemporaryFolder(folder);
            }
        }
    }
}

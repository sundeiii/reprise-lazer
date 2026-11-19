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
using osu.Game.Database;
using osu.Game.Overlays.Notifications;
using osu.Game.Skinning;

namespace osu.Game.Overlays.LazerImport
{
    /// <summary>
    /// Reprise: osu!lazer's skins (here already: ours by the same name and creator), imported as osu!lazer exports them.
    /// </summary>
    [LocalisableDescription(typeof(ScreenImportLazerSkins), nameof(Title))]
    public partial class ScreenImportLazerSkins : LazerImportScreen
    {
        public static LocalisableString Title => "Pick the skins to bring over";

        protected override string Noun => "skin";
        protected override string NounPlural => "skins";

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        protected override IReadOnlyList<Entry> ReadEntries(LazerDataSource source)
        {
            var ours = realm.Run(r => r.All<SkinInfo>().Where(s => !s.DeletePending).AsEnumerable().Select(s => (s.Name, s.Creator)).ToHashSet());

            return source.ReadSkins()
                         .Select(s => new Entry(s, s.Name.Trim(), string.IsNullOrWhiteSpace(s.Creator) ? "Unknown" : s.Creator.Trim(), ours.Contains((s.Name, s.Creator))))
                         .ToList();
        }

        protected override async Task Import(LazerDataSource source, IReadOnlyList<object> items, ProgressNotification notification)
        {
            var selected = items.Cast<LazerDataSource.LazerSkin>().ToList();
            string folder = Path.Combine(Path.GetTempPath(), $@"reprise-lazer-skins-import-{Guid.NewGuid():N}");

            try
            {
                var paths = new List<string>();

                for (int i = 0; i < selected.Count; i++)
                {
                    if (notification.CancellationToken.IsCancellationRequested)
                        return;

                    try
                    {
                        paths.Add(source.WriteOsk(selected[i], folder));
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Couldn't copy {selected[i]} from osu!lazer");
                    }

                    notification.Text = $"Copying skins from osu!lazer ({i + 1} of {selected.Count})...";
                    notification.Progress = (float)(i + 1) / selected.Count;
                }

                // The import's own notification takes over from here.
                notification.CompletionText = $"Copied {CountOf(paths.Count)} from osu!lazer.";
                notification.State = ProgressNotificationState.Completed;

                await skins.Import(paths.ToArray()).ConfigureAwait(false);
            }
            finally
            {
                DeleteTemporaryFolder(folder);
            }
        }
    }
}

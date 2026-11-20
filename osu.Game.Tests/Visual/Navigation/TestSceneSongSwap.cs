// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.SongSwap;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.SongSwap;
using osu.Game.Tests.Resources;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>Reprise: a map played to another song: swapped from its screen, the map itself untouched, and put back.</summary>
    public partial class TestSceneSongSwap : OsuGameTestScene
    {
        /// <summary>Set by a screenshot runner: called with a name once the screen is shown.</summary>
        public static Action<string>? Screenshot;

        private BeatmapManager beatmaps => Game.Dependencies.Get<BeatmapManager>();
        private SongSwapManager swaps => Game.Dependencies.Get<SongSwapManager>();
        private SongSwapOverlay overlay => Game.Dependencies.Get<SongSwapOverlay>();

        private BeatmapSetInfo map = null!;
        private string originalSongHash = null!;
        private string mapHash = null!;

        [Test]
        public void TestSwapAndRestore()
        {
            AddStep("import a map and another song", () =>
            {
                map = beatmaps.Import(new ImportTask(TestResources.GetTestBeatmapStream(), "map.osz")).GetResultSafely()!.Value.Detach();
                beatmaps.Import(new ImportTask(renamed(TestResources.GetTestBeatmapStream(), "Another Song"), "another.osz")).WaitSafely();

                originalSongHash = songHash();
                mapHash = map.Beatmaps.First().MD5Hash;
            });

            AddStep("open it for the map", () => overlay.ShowFor(map.Beatmaps.First()));
            AddUntilStep("the other song listed", () => overlay.ChildrenOfType<TruncatingSpriteText>().Any(t => t.Text.ToString().Contains("Another Song", StringComparison.Ordinal)));
            AddWaitStep("let it show", 5);
            AddStep("screenshot", () => Screenshot?.Invoke("song-swap"));

            AddStep("pick it", () =>
            {
                InputManager.MoveMouseTo(overlay.ChildrenOfType<TruncatingSpriteText>().First(t => t.Text.ToString().Contains("Another Song", StringComparison.Ordinal)));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("picked", () => overlay.ChildrenOfType<ScreenSongSwap>().Single().Picked != null);

            AddStep("swap", () => overlay.NextButton!.TriggerClick());
            AddUntilStep("swapped", () => swaps.IsSwapped(map), () => Is.True);
            AddUntilStep("its song changed", songHash, () => Is.Not.EqualTo(originalSongHash));

            AddAssert("the map itself the same (its scores count)", () => fresh().Beatmaps.First().MD5Hash, () => Is.EqualTo(mapHash));
            AddAssert("not marked as modified", () => fresh().Status, () => Is.Not.EqualTo(BeatmapOnlineStatus.LocallyModified));
            AddAssert("says what it's swapped with", () => swaps.SwappedWith(fresh()), () => Does.Contain("Another Song"));

            AddStep("restore", () => overlay.Restore(fresh().Beatmaps.First()));
            AddAssert("its own song back", songHash, () => Is.EqualTo(originalSongHash));
            AddAssert("not swapped", () => swaps.IsSwapped(fresh()), () => Is.False);
        }

        private BeatmapSetInfo fresh() => beatmaps.QueryBeatmapSet(s => s.ID == map.ID)!.PerformRead(s => s.Detach());

        // The set's song file's hash, from the database (a detached set has no files listed).
        private string songHash() => beatmaps.QueryBeatmapSet(s => s.ID == map.ID)!.PerformRead(s => s.GetFile(s.Beatmaps.First().Metadata.AudioFile)!.File.Hash);

        /// <summary>The archive with its maps retitled (another set, the same song and timing).</summary>
        private static Stream renamed(Stream osz, string title)
        {
            var output = new MemoryStream();

            using (var source = new ZipArchive(osz, ZipArchiveMode.Read))
            using (var target = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                foreach (var entry in source.Entries)
                {
                    using var read = entry.Open();
                    using var memory = new MemoryStream();
                    read.CopyTo(memory);
                    byte[] data = memory.ToArray();

                    if (entry.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase))
                    {
                        string text = Encoding.UTF8.GetString(data);
                        text = string.Join('\n', text.Split('\n').Select(l =>
                            l.StartsWith("Title:", StringComparison.Ordinal) || l.StartsWith("TitleUnicode:", StringComparison.Ordinal) ? l[..(l.IndexOf(':') + 1)] + title
                            : l.StartsWith("BeatmapID:", StringComparison.Ordinal) || l.StartsWith("BeatmapSetID:", StringComparison.Ordinal) ? l[..(l.IndexOf(':') + 1)] + "-1"
                            : l));
                        data = Encoding.UTF8.GetBytes(text);
                    }

                    using var write = target.CreateEntry(entry.FullName).Open();
                    write.Write(data);
                }
            }

            output.Position = 0;
            return output;
        }
    }
}

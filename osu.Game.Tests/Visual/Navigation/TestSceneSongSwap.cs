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
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.SongSwap;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays.SongSwap;
using osu.Game.Screens.Play.Leaderboards;
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
        private BeatmapSetInfo otherSong = null!;
        private byte[] originalSourceRender = null!;
        private string originalSongHash = null!;
        private string mapHash = null!;

        [Test]
        public void TestSwapAndRestore()
        {
            AddStep("import a map and another song", () =>
            {
                map = beatmaps.Import(new ImportTask(TestResources.GetTestBeatmapStream(), "map.osz")).GetResultSafely()!.Value.Detach();
                otherSong = beatmaps.Import(new ImportTask(renamed(TestResources.GetTestBeatmapStream(), "Another Song"), "another.osz")).GetResultSafely()!.Value.Detach();

                originalSongHash = songHash();
                mapHash = map.Beatmaps.First().MD5Hash;
                originalSourceRender = swaps.Prepare(otherSong.Beatmaps.First(), map.Beatmaps.First()).Song;
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
            AddWaitStep("show selected song", 5);
            AddStep("selected screenshot", () => Screenshot?.Invoke("song-swap-selected"));

            AddStep("preview it", () => button("Preview").TriggerClick());
            AddUntilStep("previewing", () => overlay.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().StartsWith("Previewing", StringComparison.Ordinal)));
            AddWaitStep("let it play", 5);
            AddStep("preview screenshot", () => Screenshot?.Invoke("song-swap-preview"));
            AddStep("stop it", () => button("Stop").TriggerClick());
            AddUntilStep("stopped", () => button("Preview").IsPresent);

            AddStep("swap", () => overlay.NextButton!.TriggerClick());
            AddUntilStep("swapped", () => swaps.IsSwapped(map), () => Is.True);
            AddUntilStep("its song changed", songHash, () => Is.Not.EqualTo(originalSongHash));

            AddAssert("the map itself the same (its scores count)", () => fresh().Beatmaps.First().MD5Hash, () => Is.EqualTo(mapHash));
            AddAssert("not marked as modified", () => fresh().Status, () => Is.Not.EqualTo(BeatmapOnlineStatus.LocallyModified));
            AddAssert("says what it's swapped with", () => swaps.SwappedWith(fresh()), () => Does.Contain("Another Song"));
            AddAssert("a swapped source still uses its original audio", () => swaps.Prepare(otherSong.Beatmaps.First(), fresh().Beatmaps.First()).Song,
                () => Is.EqualTo(originalSourceRender));

            AddStep("restore", () => overlay.Restore(fresh().Beatmaps.First()));
            AddAssert("its own song back", songHash, () => Is.EqualTo(originalSongHash));
            AddAssert("not swapped", () => swaps.IsSwapped(fresh()), () => Is.False);
        }

        [Test]
        public void TestOwnSongOfferedAsInstrumental()
        {
            AddStep("import a map", () => map = beatmaps.Import(new ImportTask(TestResources.GetTestBeatmapStream(), "map.osz")).GetResultSafely()!.Value.Detach());
            AddStep("instrumental off", () => Game.LocalConfig.SetValue(OsuSetting.SongSwapInstrumental, false));
            AddStep("open it for the map", () => overlay.ShowFor(map.Beatmaps.First()));
            AddUntilStep("screen shown", () => overlay.ChildrenOfType<ScreenSongSwap>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("its own song not listed", () => !ownRowShown());

            AddStep("instrumental on", () => Game.LocalConfig.SetValue(OsuSetting.SongSwapInstrumental, true));
            AddUntilStep("its own song listed", ownRowShown);
            AddAssert("it's the own song", () => SongSwapManager.IsOwnSong(map.Beatmaps.First(), map.Beatmaps.First()));

            // Without the vocal removal model here: its own song as it is renders on the map unchanged (the instrumental's path).
            AddAssert("previews as it is", () => swaps.RenderPreview(map.Beatmaps.First(), map.Beatmaps.First()).Song.Length, () => Is.GreaterThan(0));
            AddAssert("swapping it for itself refused", () =>
            {
                try
                {
                    swaps.Prepare(map.Beatmaps.First(), map.Beatmaps.First());
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            });

            AddStep("instrumental off", () => Game.LocalConfig.SetValue(OsuSetting.SongSwapInstrumental, false));
            AddUntilStep("not listed again", () => !ownRowShown());

            bool ownRowShown() => overlay.ChildrenOfType<TruncatingSpriteText>().Any(t => t.Text.ToString().Contains("(this map's song)", StringComparison.Ordinal));
        }

        private RoundedButton button(string text) => overlay.ChildrenOfType<RoundedButton>().First(b => b.Text.ToString() == text);

        private BeatmapSetInfo fresh() => beatmaps.QueryBeatmapSet(s => s.ID == map.ID)!.PerformRead(s => s.Detach());

        [TestCase(BeatmapLeaderboardScope.Local, false)]
        [TestCase(BeatmapLeaderboardScope.Global, false)]
        [TestCase(BeatmapLeaderboardScope.Global, true)]
        public void TestPlayingTrackReloadsAfterSwapAndRestore(BeatmapLeaderboardScope scope, bool paused)
        {
            WorkingBeatmap originalWorking = null!;
            WorkingBeatmap swappedWorking = null!;
            DrawableTrack originalTrack = null!;
            DrawableTrack swappedTrack = null!;
            string originalBeatmapHash = null!;

            AddStep("import songs and select map", () =>
            {
                map = beatmaps.Import(new ImportTask(TestResources.GetTestBeatmapStream(), "map.osz")).GetResultSafely()!.Value.Detach();
                otherSong = beatmaps.Import(new ImportTask(renamed(TestResources.GetTestBeatmapStream(), "Another Song"), "another.osz")).GetResultSafely()!.Value.Detach();
                originalSongHash = songHash();
                originalWorking = beatmaps.GetWorkingBeatmap(map.Beatmaps.First(), true);
                originalBeatmapHash = originalWorking.BeatmapInfo.Hash;
                mapHash = originalWorking.BeatmapInfo.MD5Hash;
                Game.Beatmap.Value = originalWorking;
                Game.MusicController.Play();
            });
            AddUntilStep("original track playing", () => Game.MusicController.IsPlaying && !Game.MusicController.CurrentTrack.IsDummyDevice);
            AddStep("fetch selected leaderboard", () =>
            {
                originalTrack = Game.MusicController.CurrentTrack;
                Game.Dependencies.Get<LeaderboardManager>().FetchWithCriteria(new LeaderboardCriteria(
                    originalWorking.BeatmapInfo, originalWorking.BeatmapInfo.Ruleset, scope, null));
                if (paused)
                    Game.MusicController.Stop(requestedByUser: true);
            });
            AddUntilStep("requested playback state", () => Game.MusicController.IsPlaying == !paused);

            // The context menu can pass the active working model itself, rather than a separate detached copy.
            AddStep("swap the active map", () => overlay.Swap(Game.Beatmap.Value.BeatmapInfo, otherSong.Beatmaps.First()));
            AddUntilStep("replacement written", () => songHash() != originalSongHash);
            AddAssert("playing snapshot keeps original file hash", () => audioHash(originalWorking), () => Is.EqualTo(originalSongHash));
            AddUntilStep("replacement loaded with playback state preserved", () => Game.MusicController.CurrentTrack != originalTrack && Game.MusicController.IsPlaying == !paused);
            AddAssert("active model uses replacement audio", () => audioHash(Game.Beatmap.Value), () => Is.EqualTo(songHash()));
            AddAssert("beatmap SHA and MD5 unchanged after swap", () =>
                Game.Beatmap.Value.BeatmapInfo.Hash == originalBeatmapHash && Game.Beatmap.Value.BeatmapInfo.MD5Hash == mapHash);
            AddStep("remember swapped playback", () =>
            {
                swappedWorking = Game.Beatmap.Value;
                swappedTrack = Game.MusicController.CurrentTrack;
            });

            AddStep("restore the active map", () => overlay.Restore(Game.Beatmap.Value.BeatmapInfo));
            AddAssert("original written back", songHash, () => Is.EqualTo(originalSongHash));
            AddAssert("swapped snapshot keeps replacement file hash", () => audioHash(swappedWorking), () => Is.Not.EqualTo(originalSongHash));
            AddUntilStep("original loaded with playback state preserved", () => Game.MusicController.CurrentTrack != swappedTrack && Game.MusicController.IsPlaying == !paused);
            AddAssert("active model uses original audio", () => audioHash(Game.Beatmap.Value), () => Is.EqualTo(originalSongHash));
            AddAssert("beatmap SHA and MD5 unchanged after restore", () =>
                Game.Beatmap.Value.BeatmapInfo.Hash == originalBeatmapHash && Game.Beatmap.Value.BeatmapInfo.MD5Hash == mapHash);
        }

        private static string audioHash(WorkingBeatmap working) => working.BeatmapSetInfo.GetFile(working.Metadata.AudioFile)!.File.Hash;

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

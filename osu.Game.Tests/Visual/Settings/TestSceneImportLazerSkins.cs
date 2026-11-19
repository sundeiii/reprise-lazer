// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Overlays.LazerImport;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.Visual.Settings
{
    /// <summary>
    /// Reprise: the "Import ... from osu!lazer" wizards' screens (skins, beatmaps, collections), on a small osu!lazer data folder of the
    /// test's own (never the one on this computer).
    /// </summary>
    public partial class TestSceneImportLazerSkins : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        private string dataPath = null!;

        [OneTimeSetUp]
        public void CreateData()
        {
            dataPath = Path.Combine(Path.GetTempPath(), $"lazer-import-test-{Guid.NewGuid():N}");
            LazerDataSourceTest.CreateLazerData(dataPath);
        }

        [OneTimeTearDown]
        public void DeleteData()
        {
            try
            {
                Directory.Delete(dataPath, true);
            }
            catch
            {
                // A leftover in the temporary folder.
            }
        }

        [Test]
        public void TestSkins() => testScreen(f => new ScreenImportLazerSkins { FindSource = f }, ScreenImportLazerSkins.Title, "Imported", "Import 1 skin");

        [Test]
        public void TestBeatmaps() => testScreen(f => new ScreenImportLazerBeatmaps { FindSource = f }, ScreenImportLazerBeatmaps.Title, "Artist - Song", "Import 1 beatmap");

        [Test]
        public void TestCollections() => testScreen(f => new ScreenImportLazerCollections { FindSource = f }, ScreenImportLazerCollections.Title, "Favourites", "Import 1 collection");

        [Test]
        public void TestNoLazer()
        {
            LazerImportScreen screen = null!;

            AddStep("show screen", () => show(screen = new ScreenImportLazerBeatmaps { FindSource = () => null }));
            AddUntilStep("says it isn't here", () => descriptionOf(screen).Contains("isn't on this computer", StringComparison.Ordinal));
            AddAssert("button closes", () => screen.NextStepText?.ToString() == "Close");
        }

        private void testScreen(Func<Func<LazerDataSource?>, LazerImportScreen> create, LocalisableString title, string onlyRow, string importText)
        {
            LazerImportScreen screen = null!;

            AddStep("show screen", () => show(screen = create(() => LazerDataSource.FromPath(dataPath))));

            AddUntilStep("titled", () => screen.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == title.ToString()));
            AddAssert("button closes with none ticked", () => screen.NextStepText?.ToString() == "Close");

            AddUntilStep("lists what's there", () => screen.ChildrenOfType<LazerImportRow>().Select(r => r.Title), () => Is.EqualTo(new[] { onlyRow }));
            AddAssert("says how many", () => descriptionOf(screen).StartsWith("osu!lazer on this computer has 1 ", StringComparison.Ordinal));

            AddStep("tick it", () => screen.ChildrenOfType<LazerImportRow>().Single().TriggerClick());
            AddAssert("button imports it", () => screen.NextStepText?.ToString(), () => Is.EqualTo(importText));
        }

        private void show(LazerImportScreen screen)
        {
            var stack = new ScreenStack { RelativeSizeAxes = Axes.Both };
            Child = stack;
            stack.Push(screen);
        }

        private static string descriptionOf(LazerImportScreen screen)
        {
            var description = screen.ChildrenOfType<OsuTextFlowContainer>().FirstOrDefault();
            return string.Concat(description?.ChildrenOfType<SpriteText>().Select(s => s.Text.ToString()) ?? []);
        }
    }
}

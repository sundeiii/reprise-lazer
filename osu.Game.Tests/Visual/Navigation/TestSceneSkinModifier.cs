// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.SkinMixer;
using osu.Game.Skinning;
using osu.Game.Skinning.Mixer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>Reprise: the skin modifier in the game: a skin's modified copy, made and put on.</summary>
    public partial class TestSceneSkinModifier : OsuGameTestScene
    {
        /// <summary>Set by a screenshot runner: called with a name once the modifier is shown.</summary>
        public static Action<string>? Screenshot;

        private SkinManager skins => Game.Dependencies.Get<SkinManager>();

        private SkinModifierOverlay modifier => Game.Dependencies.Get<SkinModifierOverlay>();

        [Test]
        public void TestModifySkin()
        {
            AddStep("import a skin", () => import("Modify Me",
                ("skin.ini", System.Text.Encoding.UTF8.GetBytes("[General]\nName: Modify Me\nVersion: 2.7\n")),
                ("cursor.png", png(16, 16)),
                ("cursortrail.png", png(32, 32)),
                ("hit300.png", png(8, 8))));
            AddUntilStep("there", () => skins.Query(s => s.Name == "Modify Me" && !s.DeletePending) != null);

            AddStep("open the modifier", () => modifier.Show());
            AddUntilStep("shown", () => modifier.State.Value == Visibility.Visible && modifier.ChildrenOfType<ScreenSkinModifier>().Any(s => s.IsLoaded));

            AddStep("pick it", () =>
            {
                var dropdown = modifier.ChildrenOfType<FormDropdown<SkinMixerSource>>().Single();
                dropdown.Current.Value = dropdown.Items.Single(s => s.Name == "Modify Me");
            });

            AddStep("instafade, smooth trail, no 300s", () =>
            {
                foreach (var box in modifier.ChildrenOfType<FormCheckBox>().Where(b => b.Caption.ToString() is "Instafade hitcircles" or "Smooth cursor trail" or "Hide 300s"))
                    box.Current.Value = true;
            });

            AddWaitStep("let it show", 5);
            AddStep("screenshot", () => Screenshot?.Invoke("skin-modifier"));

            AddStep("create", () => modifier.NextButton!.TriggerClick());
            AddUntilStep("the modified copy is on", () => skins.CurrentSkinInfo.Value.PerformRead(s => s.Name), () => Is.EqualTo("Modify Me (modified)"));

            AddAssert("changed as asked", () => skins.Query(s => s.Name == "Modify Me (modified)" && !s.DeletePending)!.PerformRead(s =>
                s.GetFile("cursormiddle.png") != null
                && s.GetFile("hit300@2x.png") != null
                // Instafade from the classic skin's circle: its numbers made.
                && s.GetFile("default-1@2x.png") != null));
            AddAssert("the original as it was", () => skins.Query(s => s.Name == "Modify Me" && !s.DeletePending)!.PerformRead(s => s.GetFile("cursormiddle.png") == null));
        }

        private void import(string name, params (string path, byte[] data)[] files)
        {
            var stream = new MemoryStream();

            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                foreach (var (path, data) in files)
                {
                    using var entry = zip.CreateEntry(path).Open();
                    entry.Write(data);
                }
            }

            stream.Position = 0;
            skins.Import(new ImportTask(stream, $"{name}.osk")).WaitSafely();
        }

        private static byte[] png(int width, int height)
        {
            using var image = new Image<Rgba32>(width, height, new Rgba32(255, 255, 255));
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            return stream.ToArray();
        }
    }
}

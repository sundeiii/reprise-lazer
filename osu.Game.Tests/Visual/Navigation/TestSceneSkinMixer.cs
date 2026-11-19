// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.SkinMixer;
using osu.Game.Skinning;
using osu.Game.Skinning.Mixer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>Reprise: the skin mixer in the game: a base skin with another's cursor, made and put on.</summary>
    public partial class TestSceneSkinMixer : OsuGameTestScene
    {
        /// <summary>Set by a screenshot runner: called with a name once the mixer is shown.</summary>
        public static Action<string>? Screenshot;

        private SkinManager skins => Game.Dependencies.Get<SkinManager>();

        private SkinMixerOverlay mixer => Game.Dependencies.Get<SkinMixerOverlay>();

        [Test]
        public void TestManySkins()
        {
            AddStep("import 40 skins", () =>
            {
                for (int i = 0; i < 40; i++)
                    import($"Many Skin {i:00}", ("skin.ini", text($"[General]\nName: Many Skin {i:00}\n")),("cursor.png", png((byte)(i * 6), 0, 0)));
            });

            AddStep("open the mixer", () => mixer.Show());
            AddUntilStep("shown", () => mixer.State.Value == Visibility.Visible && mixer.ChildrenOfType<ScreenSkinMixer>().Any(s => s.IsLoaded));
            AddWaitStep("settle", 5);
            AddStep("screenshot", () => Screenshot?.Invoke("skin-mixer-before"));

            AddStep("open the cursor group", () =>
            {
                InputManager.MoveMouseTo(mixer.ChildrenOfType<SpriteIcon>().Where(i => i.Parent?.Parent?.GetType().Name == "GroupToggle").ElementAt(2));
                InputManager.Click(MouseButton.Left);
            });
            AddWaitStep("settle", 3);

            AddStep("open the last group's list", () =>
            {
                // Menu Sounds, at the bottom.
                var dropdown = mixer.ChildrenOfType<FormDropdown<SkinMixerChoice>>().Single(d => d.Caption.ToString() == "Menu Sounds");
                mixer.ChildrenOfType<osu.Game.Graphics.Containers.OsuScrollContainer>().First().ScrollIntoView(dropdown, true);
            });
            AddWaitStep("scrolled", 5);
            AddStep("click it", () =>
            {
                var dropdown = mixer.ChildrenOfType<FormDropdown<SkinMixerChoice>>().Single(d => d.Caption.ToString() == "Menu Sounds");
                InputManager.MoveMouseTo(dropdown.ChildrenOfType<DropdownHeader>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddWaitStep("let it open", 5);
            AddStep("screenshot", () => Screenshot?.Invoke("skin-mixer-many"));
        }

        [Test]
        public void TestMixCursorIntoBase()
        {
            AddStep("import two skins", () =>
            {
                import("Mixer Base", ("skin.ini", text("[General]\nName: Mixer Base\nVersion: 2.7\n[Colours]\nCombo1: 255,0,0\n")), ("cursor.png", png(255, 0, 0)), ("hitcircle.png", png(0, 255, 0)));
                import("Mixer Other", ("skin.ini", text("[General]\nName: Mixer Other\n")), ("cursor.png", png(0, 0, 255)), ("cursortrail.png", png(0, 0, 128)));
            });
            AddUntilStep("both there", () => skins.Query(s => s.Name == "Mixer Other" && !s.DeletePending) != null && skins.Query(s => s.Name == "Mixer Base" && !s.DeletePending) != null);

            AddStep("open the mixer", () => mixer.Show());
            AddUntilStep("shown", () => mixer.State.Value == Visibility.Visible && mixer.ChildrenOfType<ScreenSkinMixer>().Any(s => s.IsLoaded));

            AddStep("base: Mixer Base", () =>
            {
                var dropdown = mixer.ChildrenOfType<FormDropdown<SkinMixerSource>>().Single();
                dropdown.Current.Value = dropdown.Items.Single(s => s.Name == "Mixer Base");
            });

            AddStep("cursor from Mixer Other", () =>
            {
                // The groups, in order: Interface, Gameplay, Cursor, Hitsounds, Menu Sounds.
                var cursor = mixer.ChildrenOfType<OsuDropdown<SkinMixerChoice>>().ElementAt(2);
                cursor.Current.Value = cursor.Items.Single(c => c.Skin?.Name == "Mixer Other");
            });

            AddStep("open the cursor group", () =>
            {
                // Its arrow (the groups, in order: Interface, Gameplay, Cursor, ...).
                InputManager.MoveMouseTo(mixer.ChildrenOfType<SpriteIcon>().Where(i => i.Parent?.Parent?.GetType().Name == "GroupToggle").ElementAt(2));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("its parts shown", () => mixer.ChildrenOfType<OsuDropdown<SkinMixerChoice>>().Count(d => d.IsPresent && d.Parent!.IsPresent) > 5);

            AddWaitStep("let it show", 5);
            AddStep("screenshot", () => Screenshot?.Invoke("skin-mixer"));

            AddStep("create", () => mixer.NextButton!.TriggerClick());

            AddUntilStep("the new skin is on", () => skins.CurrentSkinInfo.Value.PerformRead(s => s.Name), () => Is.EqualTo("Mixer Base (mixed)"));

            AddAssert("its cursor is the other's, the rest the base's", () =>
            {
                string hash(string skin, string file) => skins.Query(s => s.Name == skin && !s.DeletePending)!.PerformRead(s => s.GetFile(file)!.File.Hash);

                return hash("Mixer Base (mixed)", "cursor.png") == hash("Mixer Other", "cursor.png")
                       && hash("Mixer Base (mixed)", "cursortrail.png") == hash("Mixer Other", "cursortrail.png")
                       && hash("Mixer Base (mixed)", "hitcircle.png") == hash("Mixer Base", "hitcircle.png");
            });
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

        private static byte[] text(string s) => System.Text.Encoding.UTF8.GetBytes(s);

        private static byte[] png(byte r, byte g, byte b)
        {
            using var image = new Image<Rgba32>(4, 4, new Rgba32(r, g, b));
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            return stream.ToArray();
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Graphics.Cursor;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Tests.Visual.UserInterface
{
    /// <summary>
    /// The skin's cursor in menus (as osu!stable shows it), a setting of its own.
    /// </summary>
    public partial class TestSceneSkinMenuCursor : OsuManualInputManagerTestScene
    {
        /// <summary>
        /// Called with a name where a screenshot of the scene is wanted (by a runner showing it in a window; nothing in tests).
        /// </summary>
        public static Action<string>? Screenshot;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private MenuCursorContainer cursor = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("classic skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("show cursor", () =>
            {
                Child = cursor = new MenuCursorContainer { RelativeSizeAxes = Axes.Both, State = { Value = Visibility.Visible } };
            });
            AddStep("mouse in the middle", () => InputManager.MoveMouseTo(cursor));
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("reset", () =>
            {
                config.SetValue(OsuSetting.SkinCursorInMenus, false);
                skins.CurrentSkinInfo.SetDefault();
            });
        }

        [Test]
        public void TestSkinCursor()
        {
            AddStep("setting off", () => config.SetValue(OsuSetting.SkinCursorInMenus, false));
            AddUntilStep("the game's cursor", () => !skinCursor().Active.Value);

            AddStep("setting on", () => config.SetValue(OsuSetting.SkinCursorInMenus, true));
            AddUntilStep("the skin's cursor", () => skinCursor().Active.Value && skinCursor().ChildrenOfType<Sprite>().Any(s => s.Texture != null));

            AddStep("move around", () =>
            {
                for (int i = 0; i < 10; i++)
                    InputManager.MoveMouseTo(cursor.ToScreenSpace(cursor.DrawSize / 2 + new Vector2(i * 20, i * 8)));
            });
            AddRepeatStep("keep moving", () => InputManager.MoveMouseTo(cursor.ToScreenSpace(cursor.DrawSize / 2 + new Vector2(RNG(), RNG()))), 20);
            AddUntilStep("a trail", () => cursor.ChildrenOfType<SkinMenuCursorTrail>().Single().Children.Count > 0);
            AddStep("screenshot", () => Screenshot?.Invoke("SkinCursor"));

            // Gameplay hides the menu cursor for the playfield's: its trail must not keep drawing over it.
            AddStep("gameplay's cursor takes over", () => cursor.State.Value = Visibility.Hidden);
            // At once, as stable swaps cursors: fading, it showed together with gameplay's for a moment.
            AddAssert("skin cursor gone at once", () => cursor.ActiveCursor.Alpha == 0);
            AddRepeatStep("move in gameplay", () => InputManager.MoveMouseTo(cursor.ToScreenSpace(cursor.DrawSize / 2 + new Vector2(RNG(), RNG()))), 10);
            AddAssert("no menu trail", () => cursor.ChildrenOfType<SkinMenuCursorTrail>().Single().Children.Count == 0);
            AddStep("back to the menus", () => cursor.State.Value = Visibility.Visible);
            AddAssert("skin cursor back at once", () => cursor.ActiveCursor.Alpha == 1);
            AddRepeatStep("move again", () => InputManager.MoveMouseTo(cursor.ToScreenSpace(cursor.DrawSize / 2 + new Vector2(RNG(), RNG()))), 10);
            AddUntilStep("the trail again", () => cursor.ChildrenOfType<SkinMenuCursorTrail>().Single().Children.Count > 0);

            // Gameplay's legacy trail makes the shared texture 1.6x smaller (for the playfield's scale): not the menus'.
            AddStep("gameplay changes the texture's scale", () => skinCursor().TrailTexture!.ScaleAdjust *= osu.Game.Skinning.LegacySkin.STABLE_MAGIC_SCALE_FACTOR);
            AddRepeatStep("move more", () => InputManager.MoveMouseTo(cursor.ToScreenSpace(cursor.DrawSize / 2 + new Vector2(RNG(), RNG()))), 10);
            AddAssert("the cursor is centred on the mouse", () =>
            {
                var sprite = skinCursor().ChildrenOfType<Sprite>().First();
                return Precision.AlmostEquals(sprite.ScreenSpaceDrawQuad.Centre, InputManager.CurrentState.Mouse.Position, 1);
            });
            AddAssert("the trail keeps the skin's size", () => cursor.ChildrenOfType<SkinMenuCursorTrail>().Single().Children.All(t => t.DrawSize == skinCursor().TrailSize && t.DrawSize.X > 0));

            AddStep("setting off again", () => config.SetValue(OsuSetting.SkinCursorInMenus, false));
            AddUntilStep("the game's cursor again", () => !skinCursor().Active.Value);
        }

        /// <summary>
        /// A skin of the user's (a copy: importing deletes the archive), for screenshots by the runner; nothing happens without it.
        /// </summary>
        [Test]
        public void TestUserSkin()
        {
            const string path = "C:/Users/Lilian/Documents/skins/-.S.FREEDOM.DiVE.REiMAGINED.osk";

            AddStep("import a copy", () =>
            {
                if (!System.IO.File.Exists(path))
                    return;

                string copy = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "skin-cursor-test.osk");
                System.IO.File.Copy(path, copy, true);
                var imported = skins.Import(new osu.Game.Database.ImportTask(copy)).GetResultSafely();
                if (imported != null)
                    skins.CurrentSkinInfo.Value = imported;
            });
            AddStep("setting on", () => config.SetValue(OsuSetting.SkinCursorInMenus, true));
            AddUntilStep("the skin's cursor", () => skinCursor().Active.Value);

            for (int i = 0; i < 40; i++)
            {
                int step = i;
                AddStep($"circle {step}", () =>
                {
                    float angle = step / 40f * MathF.PI * 2;
                    InputManager.MoveMouseTo(cursor.ToScreenSpace(cursor.DrawSize / 2 + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 150));
                });
            }

            AddStep("screenshot", () => Screenshot?.Invoke("UserSkin"));
        }

        private static int seed;

        private static float RNG() => (seed = (seed * 1103515245 + 12345) & 0x7fffffff) % 300 - 150;

        private SkinMenuCursor skinCursor() => cursor.ChildrenOfType<SkinMenuCursor>().Single();
    }
}

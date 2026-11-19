// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Overlays.LazerSkinImport;

namespace osu.Game.Tests.Visual.Settings
{
    /// <summary>
    /// Reprise: the "Import skins from osu!lazer" wizard's screen.
    /// </summary>
    public partial class TestSceneImportLazerSkins : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [Test]
        public void TestShow()
        {
            ScreenImportLazerSkins screen = null!;

            AddStep("show screen", () =>
            {
                var stack = new ScreenStack { RelativeSizeAxes = Axes.Both };
                Child = stack;
                stack.Push(screen = new ScreenImportLazerSkins());
            });

            AddAssert("titled", () => screen.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == ScreenImportLazerSkins.Title.ToString()));
            AddAssert("button closes with none ticked", () => screen.NextStepText?.ToString() == "Close");

            // (No osu!lazer for the tests: it says so.)
            AddUntilStep("says what it found", () =>
            {
                var description = screen.ChildrenOfType<OsuTextFlowContainer>().FirstOrDefault();
                string text = string.Concat(description?.ChildrenOfType<SpriteText>().Select(s => s.Text.ToString()) ?? []);
                return text.Length > 0 && !text.StartsWith("Reading", System.StringComparison.Ordinal);
            });
        }
    }
}

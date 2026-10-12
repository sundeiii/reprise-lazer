// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osuTK;

namespace osu.Game.Tests.Visual.UserInterface
{
    /// <summary>
    /// osu!stable's layouts letterboxed to 16:9 in a 1920x1009 window (<see cref="ScalingContainer.SetForcedAspectRatio"/>).
    /// </summary>
    public partial class TestSceneStableLetterbox : OsuTestScene
    {
        private ScalingContainer scaling = null!;
        private Box content = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.CacheAs(new OsuConfigManager(LocalStorage));

            Child = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(1920, 1009) / 2,
                Child = scaling = new ScalingContainer(ScalingMode.ExcludeOverlays)
                {
                    Child = content = new Box { RelativeSizeAxes = Axes.Both },
                },
            };
        }

        [Test]
        public void TestLetterbox()
        {
            AddStep("16:9", () => scaling.SetForcedAspectRatio(16f / 9));
            AddUntilStep("content 16:9", () => Precision.AlmostEquals(content.ScreenSpaceDrawQuad.Width / content.ScreenSpaceDrawQuad.Height, 16f / 9, 0.01f));
            AddAssert("centred", () => Precision.AlmostEquals(content.ScreenSpaceDrawQuad.Centre, scaling.ScreenSpaceDrawQuad.Centre, 1));

            // 4:3, which stable was made for: whole, no margins.
            AddStep("4:3 window", () => scaling.Parent!.Size = new Vector2(1280, 960) / 2);
            AddUntilStep("content whole", () => Precision.AlmostEquals(content.ScreenSpaceDrawQuad.Width / content.ScreenSpaceDrawQuad.Height, 4f / 3, 0.01f));
            AddStep("back to 1920x1009", () => scaling.Parent!.Size = new Vector2(1920, 1009) / 2);

            AddStep("off", () => scaling.SetForcedAspectRatio(null));
            AddUntilStep("content fills again", () => Precision.AlmostEquals(content.ScreenSpaceDrawQuad.Width / content.ScreenSpaceDrawQuad.Height, 1920f / 1009, 0.01f));
        }
    }
}

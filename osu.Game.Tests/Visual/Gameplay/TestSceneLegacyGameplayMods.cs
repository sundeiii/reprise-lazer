// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// osu!stable's mod icons in gameplay: right of the middle near the top, fading out unless watching a replay.
    /// </summary>
    public partial class TestSceneLegacyGameplayMods : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private void create(bool replay)
        {
            AddStep("classic skin, stable layouts", () =>
            {
                skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo;
                config.SetValue(OsuSetting.StableLayouts, true);
            });
            AddStep("create", () => Child = new LegacyGameplayMods(new OsuRuleset().RulesetInfo, new Mod[] { new OsuModHidden(), new OsuModHardRock() }, () => replay)
            {
                RelativeSizeAxes = Axes.Both,
            });
        }

        [TearDownSteps]
        public void TearDownSteps() => AddStep("reset", () =>
        {
            config.SetValue(OsuSetting.StableLayouts, false);
            skins.CurrentSkinInfo.SetDefault();
        });

        [Test]
        public void TestShownThenFading()
        {
            create(false);
            AddUntilStep("two icons", () => icons().Length == 2);
            AddAssert("right of the middle, at the top", () => icons().All(i => i.ScreenSpaceDrawQuad.Centre.X > ScreenSpaceDrawQuad.Centre.X
                                                                           && i.ScreenSpaceDrawQuad.Centre.Y < ScreenSpaceDrawQuad.Centre.Y));
            AddUntilStep("faded out", () => icons().All(i => i.Alpha == 0));
        }

        [Test]
        public void TestPlacedLikeTheGamesModDisplay()
        {
            create(true);
            AddUntilStep("two icons", () => icons().Length == 2);
            // As the game's mod display: in the top right corner, the first (Hidden) rightmost.
            AddAssert("in the top right corner", () =>
            {
                var area = ScreenSpaceDrawQuad.AABBFloat;
                var first = icons()[0].ScreenSpaceDrawQuad.AABBFloat;
                return area.Right - first.Right < area.Width * 0.05f && first.Top - area.Top < area.Height * 0.1f;
            });
            AddAssert("the next one to its left", () => icons()[1].ScreenSpaceDrawQuad.Centre.X < icons()[0].ScreenSpaceDrawQuad.Centre.X);
        }

        [Test]
        public void TestReplayKeepsThem()
        {
            create(true);
            AddUntilStep("both shown", () => icons().Length == 2 && icons().All(i => i.Alpha == 1));
            AddWaitStep("a while", 20);
            AddAssert("still shown", () => icons().All(i => i.Alpha == 1));
        }

        private Drawable[] icons() => this.ChildrenOfType<LegacyGameplayMods>().Single().Icons.ToArray();
    }
}

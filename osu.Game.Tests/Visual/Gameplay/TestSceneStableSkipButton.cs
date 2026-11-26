// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// osu!stable's skip button (<see cref="OsuSetting.StableSkipButton"/>): with a legacy skin, the skin's play-skip in the bottom right,
    /// still shown when the mouse stays still; off, or without a legacy skin, the game's own.
    /// </summary>
    public partial class TestSceneStableSkipButton : PlayerTestScene
    {
        protected override bool HasCustomSteps => true;

        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            var beatmap = new Beatmap();
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 400 });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 20000 });
            return beatmap;
        }

        [Test]
        public void TestStableButton()
        {
            AddStep("classic skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("stable skip button", () => LocalConfig.SetValue(OsuSetting.StableSkipButton, true));
            CreateTest();

            AddUntilStep("stable button", () => Player.ChildrenOfType<SkipOverlay.LegacyButton>().Count(), () => Is.EqualTo(1));
            AddAssert("no game button", () => Player.ChildrenOfType<SkipOverlay.Button>().Count(), () => Is.Zero);
            AddUntilStep("bottom right", () =>
            {
                var button = Player.ChildrenOfType<SkipOverlay.LegacyButton>().Single();
                var screen = Player.ScreenSpaceDrawQuad;
                return button.ScreenSpaceDrawQuad.BottomRight.X >= screen.BottomRight.X - 2 && button.ScreenSpaceDrawQuad.BottomRight.Y >= screen.BottomRight.Y - 2
                       && button.DrawWidth > 0;
            });
            AddWaitStep("mouse still", 20);
            AddAssert("still shown", () => Player.ChildrenOfType<SkipOverlay>().First().IsButtonVisible);
        }

        [Test]
        public void TestOffKeepsTheGamesButton()
        {
            AddStep("classic skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("stable skip button off", () => LocalConfig.SetValue(OsuSetting.StableSkipButton, false));
            CreateTest();

            AddUntilStep("game button", () => Player.ChildrenOfType<SkipOverlay.Button>().Count(), () => Is.GreaterThan(0));
            AddAssert("no stable button", () => Player.ChildrenOfType<SkipOverlay.LegacyButton>().Count(), () => Is.Zero);
        }

        [Test]
        public void TestNotWithoutALegacySkin()
        {
            AddStep("default skin", () => skins.CurrentSkinInfo.SetDefault());
            AddStep("stable skip button", () => LocalConfig.SetValue(OsuSetting.StableSkipButton, true));
            CreateTest();

            AddUntilStep("game button", () => Player.ChildrenOfType<SkipOverlay.Button>().Count(), () => Is.GreaterThan(0));
        }
    }
}

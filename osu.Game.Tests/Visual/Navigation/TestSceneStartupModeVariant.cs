// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Development;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>Reprise: Relax/Autopilot is back on when the game starts (<see cref="ModeVariantMemory"/>).</summary>
    [TestFixture]
    public partial class TestSceneStartupModeVariant : OsuGameTestScene
    {
        protected override TestOsuGame CreateTestGame()
        {
            // Must be done in this function due to the RecycleLocalStorage call just before.
            var config = DebugUtils.IsDebugBuild
                ? new DevelopmentOsuConfigManager(LocalStorage)
                : new OsuConfigManager(LocalStorage);

            config.SetValue(OsuSetting.Ruleset, "osu");
            config.SetValue(OsuSetting.ModeVariant, "RX");
            config.Save();

            return base.CreateTestGame();
        }

        [Test]
        public void TestRelaxBackOn()
        {
            AddUntilStep("relax on", () => Game.SelectedMods.Value.Any(m => m.Acronym == "RX"));

            AddStep("to mania", () => Game.Ruleset.Value = new ManiaRuleset().RulesetInfo);
            AddUntilStep("no relax in mania", () => Game.SelectedMods.Value.All(m => m.Acronym != "RX"));
            AddAssert("still remembered", () => Game.LocalConfig.Get<string>(OsuSetting.ModeVariant), () => Is.EqualTo("RX"));

            AddStep("back to osu!", () => Game.Ruleset.Value = new OsuRuleset().RulesetInfo);
            AddUntilStep("relax back on", () => Game.SelectedMods.Value.Any(m => m.Acronym == "RX"));

            AddStep("relax off", () => Game.SelectedMods.Value = Game.SelectedMods.Value.Where(m => m.Acronym != "RX").ToArray());
            AddAssert("forgotten", () => Game.LocalConfig.Get<string>(OsuSetting.ModeVariant), () => Is.Empty);
        }
    }
}

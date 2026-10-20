// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.Visual.Menus
{
    /// <summary>The toolbar's VN/RX/AP switcher: shown for rulesets with Relax/Autopilot, hidden for the others, and back again after.</summary>
    public partial class TestSceneToolbarModeVariantSelector : OsuTestScene
    {
        private ToolbarModeVariantSelector selector = null!;

        [Test]
        public void TestShownAgainAfterAnotherRuleset()
        {
            AddStep("osu!", () => Ruleset.Value = new OsuRuleset().RulesetInfo);
            AddStep("create", () => Child = selector = new ToolbarModeVariantSelector
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Height = 40,
            });
            AddUntilStep("shown", () => selector.Alpha, () => Is.EqualTo(1));

            AddStep("mania", () => Ruleset.Value = new ManiaRuleset().RulesetInfo);
            AddUntilStep("hidden", () => selector.Alpha, () => Is.EqualTo(0));

            // It used to stay hidden: updates were scheduled on its own scheduler, which doesn't run while it's hidden.
            AddStep("osu! again", () => Ruleset.Value = new OsuRuleset().RulesetInfo);
            AddUntilStep("shown again", () => selector.Alpha, () => Is.EqualTo(1));
        }
    }
}

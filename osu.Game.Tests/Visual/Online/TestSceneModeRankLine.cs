// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Online;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Users;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>Reprise: the rank line under the 2023 interface's user card follows the mode, and Relax/Autopilot.</summary>
    public partial class TestSceneModeRankLine : OsuTestScene
    {
        private TestSceneUserPanel.TestUserStatisticsProvider statisticsProvider = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Ruleset.Value = new OsuRuleset().RulesetInfo;
            SelectedMods.Value = new Mod[] { };

            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (System.Type, object)[]
                {
                    (typeof(LocalUserStatisticsProvider), statisticsProvider = new TestSceneUserPanel.TestUserStatisticsProvider()),
                },
                Children = new Drawable[]
                {
                    statisticsProvider,
                    new ModeRankLine { Width = 300, Anchor = Anchor.Centre, Origin = Anchor.Centre },
                }
            };
        });

        [Test]
        public void TestFollowsRelax()
        {
            AddStep("set statistics", () =>
            {
                var osu = new OsuRuleset().RulesetInfo;
                statisticsProvider.UpdateStatistics(new UserStatistics { GlobalRank = 1234, CountryRank = 12, PP = 4321 }, osu);
                statisticsProvider.UpdateStatistics(new UserStatistics { GlobalRank = 56, CountryRank = 3, PP = 9876 }, osu.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID));
            });

            AddUntilStep("vanilla rank", () => texts(), () => Does.Contain("#1,234"));
            AddAssert("vanilla pp", () => texts(), () => Does.Contain("4,321pp"));

            AddStep("relax on", () => SelectedMods.Value = new Mod[] { new OsuModRelax() });
            AddUntilStep("relax rank", () => texts(), () => Does.Contain("#56"));
            AddAssert("relax pp", () => texts(), () => Does.Contain("9,876pp"));
            AddAssert("relax mode named", () => texts(), () => Does.Contain("osu! (RX)"));
        }

        private string[] texts() => this.ChildrenOfType<ModeRankLine>().Single().ChildrenOfType<OsuSpriteText>().Select(t => t.Text.ToString()).ToArray();
    }
}

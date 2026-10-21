// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Online
{
    /// <summary>Reprise: "Use these mods" on a score of Song Select V1's leaderboard (<see cref="LeaderboardScore.UseMods"/>).</summary>
    [TestFixture]
    public class LeaderboardScoreUseModsTest
    {
        [Test]
        public void TestUseTheseMods()
        {
            var score = TestResources.CreateTestScoreInfo(new OsuRuleset().RulesetInfo);
            score.OnlineID = 0;
            score.Mods = new Mod[] { new OsuModHidden(), new OsuModRelax(), new ModScoreV2() };

            Mod[]? used = null;
            var drawable = new LeaderboardScore(score, 1) { UseMods = m => used = m };

            var item = drawable.ContextMenuItems.OfType<OsuMenuItem>().Single(i => i.Text.Value == SongSelectStrings.UseTheseMods);
            item.Action.Value!.Invoke();

            // The score's mods, without system ones (Score V2).
            Assert.That(used?.Select(m => m.Acronym), Is.EquivalentTo(new[] { "HD", "RX" }));
        }

        [Test]
        public void TestNotOfferedOutsideSongSelect()
        {
            var score = TestResources.CreateTestScoreInfo(new OsuRuleset().RulesetInfo);
            score.OnlineID = 0;
            score.Mods = new Mod[] { new OsuModHidden() };

            var drawable = new LeaderboardScore(score, 1);

            Assert.That(drawable.ContextMenuItems.OfType<OsuMenuItem>().Any(i => i.Text.Value == SongSelectStrings.UseTheseMods), Is.False);
        }
    }
}

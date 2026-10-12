// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Skinning.Components;
using osu.Game.Screens.Ranking.Statistics.Session;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneAttemptCounter : PlayerTestScene
    {
        private SessionStatsStore store = null!;

        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        [BackgroundDependencyLoader]
        private void loadStore()
        {
            Dependencies.Cache(store = new SessionStatsStore(LocalStorage));
        }

        private string beatmapKey => Player.GameplayState.Beatmap.BeatmapInfo.ToString();

        // the store is shared by every test in this fixture, so attempts from an earlier test may already be counted.

        [Test]
        public void TestAttemptIsCountedOnceGameplayStarts()
        {
            AddUntilStep("attempt counted", () => store.GetAttempts(beatmapKey) >= 1);

            // the counting happens whatever the skin looks like; the display is a separate skin component.
            AddAssert("nothing is drawn by the player itself", () => !Player.ChildrenOfType<AttemptCounter>().Any());
        }

        [Test]
        public void TestRetryingIsCountedAsAnotherAttempt()
        {
            int attempts = 0;

            AddUntilStep("first attempt counted", () => (attempts = store.GetAttempts(beatmapKey)) >= 1);

            AddStep("retry", LoadPlayer);
            AddUntilStep("player loaded", () => Player.IsLoaded);
            AddUntilStep("exactly one more attempt counted", () => store.GetAttempts(beatmapKey) == attempts + 1);
        }
    }

    public partial class TestSceneAttemptCounterAutoplay : PlayerTestScene
    {
        private SessionStatsStore store = null!;

        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        protected override bool Autoplay => true;

        [BackgroundDependencyLoader]
        private void loadStore()
        {
            Dependencies.Cache(store = new SessionStatsStore(LocalStorage));
        }

        [Test]
        public void TestAutoplayIsNotCounted()
        {
            AddUntilStep("gameplay running", () => Player.GameplayClockContainer.CurrentTime > 500);

            AddAssert("no attempt counted", () => store.GetAttempts(Player.GameplayState.Beatmap.BeatmapInfo.ToString()) == 0);
        }
    }
}

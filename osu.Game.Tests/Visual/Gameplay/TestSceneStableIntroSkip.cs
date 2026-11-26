// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// osu!stable's intro skip (<see cref="OsuSetting.StableIntroSkip"/>): at 150 BPM (400ms a beat) it lands 8 beats before the first object;
    /// an intro no longer than 1.2 seconds and the approach time (AR 5: 1200ms) has none.
    /// </summary>
    public partial class TestSceneStableIntroSkip : PlayerTestScene
    {
        protected override bool HasCustomSteps => true;
        protected override bool AllowFail => true;

        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        private double firstObjectStartTime;

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            var beatmap = new Beatmap();
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 400 });
            beatmap.Difficulty.ApproachRate = 5;
            beatmap.HitObjects.Add(new HitCircle { StartTime = firstObjectStartTime });
            return beatmap;
        }

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("stable intro skip", () => LocalConfig.SetValue(OsuSetting.StableIntroSkip, true));
        }

        [Test]
        public void TestLandsEightBeatsBefore()
        {
            AddStep("first object at 10000", () => firstObjectStartTime = 10000);
            CreateTest();

            skip();
            // 10000 - 8 x 400 (the game's own would be 10000 - 2000 - 1000 = 7000).
            AddUntilStep("at 6800", () => Player.GameplayClockContainer.CurrentTime, () => Is.EqualTo(6800).Within(100));
        }

        [Test]
        public void TestNoneForAShortIntro()
        {
            // Stable's point is 1300, not more than 1200 + 1200 into the map: no skip (the game's own would skip to 1500).
            AddStep("first object at 4500", () => firstObjectStartTime = 4500);
            CreateTest();

            skip();
            AddAssert("no skip offered", () => Player.ChildrenOfType<SkipOverlay>().All(s => s.SkipCount == 0 && !s.IsPresent));
            AddAssert("not skipped", () => Player.GameplayClockContainer.CurrentTime, () => Is.LessThan(timeBefore + 1));
        }

        [Test]
        public void TestOffKeepsTheGamesSkip()
        {
            AddStep("stable intro skip off", () => LocalConfig.SetValue(OsuSetting.StableIntroSkip, false));
            AddStep("first object at 10000", () => firstObjectStartTime = 10000);
            CreateTest();

            skip();
            AddUntilStep("at 7000", () => Player.GameplayClockContainer.CurrentTime, () => Is.EqualTo(7000).Within(100));
        }

        private double timeBefore;

        // Stopped first: the test track runs on fast after a seek.
        private void skip()
        {
            AddUntilStep("loaded", () => Player.IsLoaded);
            AddStep("stop the clock", () => Player.GameplayClockContainer.Stop());
            AddStep("skip", () =>
            {
                timeBefore = Player.GameplayClockContainer.CurrentTime;
                Player.ChildrenOfType<SkipOverlay>().FirstOrDefault()?.RequestSkip?.Invoke();
            });
        }
    }
}

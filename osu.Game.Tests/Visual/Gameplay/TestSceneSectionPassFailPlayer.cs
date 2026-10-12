// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Play;
using osu.Game.Storyboards;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// osu!stable's section pass / fail in a real player's break.
    /// </summary>
    public partial class TestSceneSectionPassFailPlayer : OsuPlayerTestScene
    {
        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override bool Autoplay => true;

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            var beatmap = new OsuBeatmap();
            beatmap.HitObjects.Add(new HitCircle { StartTime = 0 });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 1000 });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 12000 });
            beatmap.Breaks.Add(new BreakPeriod(1500, 11000));
            return beatmap;
        }

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null) =>
            new ClockBackedTestWorkingBeatmap(beatmap, storyboard, new FramedClock(new ManualClock { Rate = 1 }), audioManager);

        [SetUpSteps]
        public override void SetUpSteps()
        {
            AddStep("setting on", () => LocalConfig.SetValue(OsuSetting.SectionPassFail, true));

            base.SetUpSteps();

            AddUntilStep("wait for track to start running", () => Beatmap.Value.Track.IsRunning);
        }

        [Test]
        public void TestShownInBreak()
        {
            addSeekStep(2000);
            // The break's middle is 6250: its last blink is on from 6530 to 7530 (this clock runs only when seeked).
            addSeekStep(7000);
            AddUntilStep("pass shown", () => this.ChildrenOfType<SectionPassFailOverlay>().Single().ChildrenOfType<Sprite>().First().Alpha > 0);
            AddAssert("the game's break display hidden", () => this.ChildrenOfType<BreakOverlay>().Single().Parent!.Alpha == 0);
        }

        [Test]
        public void TestOffKeepsTheGamesBreakDisplay()
        {
            AddStep("setting off", () => LocalConfig.SetValue(OsuSetting.SectionPassFail, false));
            AddAssert("the game's break display shown", () => this.ChildrenOfType<BreakOverlay>().Single().Parent!.Alpha == 1);
        }

        private void addSeekStep(double time)
        {
            AddStep($"seek to {time}", () => Beatmap.Value.Track.Seek(time));
            AddUntilStep("wait for seek to finish", () => Precision.AlmostEquals(time, Player.DrawableRuleset.FrameStableClock.CurrentTime, 100));
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps.Timing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// osu!stable's section pass / fail in breaks: a 6 second break from 1000, so shown from its middle (4000).
    /// </summary>
    public partial class TestSceneSectionPassFail : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private ManualClock clock = null!;
        private DrainingHealthProcessor health = null!;

        private void create(bool enabled, double healthValue)
        {
            AddStep("setting", () => config.SetValue(OsuSetting.SectionPassFail, enabled));
            AddStep("create", () =>
            {
                clock = new ManualClock();
                health = new DrainingHealthProcessor(0);
                health.Health.Value = healthValue;

                Child = new SectionPassFailOverlay(new[] { new BreakPeriod(1000, 7000) }, health)
                {
                    Clock = new FramedClock(clock),
                };
            });
        }

        [TearDownSteps]
        public void TearDownSteps() => AddStep("reset", () => config.SetValue(OsuSetting.SectionPassFail, false));

        [Test]
        public void TestPass()
        {
            create(true, 0.5);
            AddStep("break starts", () => clock.CurrentTime = 1500);
            AddStep("its middle", () => clock.CurrentTime = 4030);
            AddAssert("pass shown", () => sprite(0).Alpha == 1 && sprite(1).Alpha == 0);
            // The game's own skin has none: the classic skin's.
            AddAssert("classic images", () => sprite(0).Texture != null && sprite(1).Texture != null);
            AddStep("blink off", () => clock.CurrentTime = 4120);
            AddAssert("pass hidden", () => sprite(0).Alpha == 0);
            AddStep("on again", () => clock.CurrentTime = 4300);
            AddAssert("pass shown", () => sprite(0).Alpha == 1);
            AddStep("faded", () => clock.CurrentTime = 5500);
            AddAssert("pass gone", () => sprite(0).Alpha == 0);
        }

        [Test]
        public void TestShortBreak()
        {
            // Stable had none in breaks this short: shown from the break's start.
            AddStep("setting", () => config.SetValue(OsuSetting.SectionPassFail, true));
            AddStep("create", () =>
            {
                clock = new ManualClock();
                health = new DrainingHealthProcessor(0);
                health.Health.Value = 1;

                Child = new SectionPassFailOverlay(new[] { new BreakPeriod(1000, 2500) }, health)
                {
                    Clock = new FramedClock(clock),
                };
            });
            AddStep("break starts", () => clock.CurrentTime = 1010);
            AddStep("just after", () => clock.CurrentTime = 1030);
            AddAssert("pass shown", () => sprite(0).Alpha == 1);
        }

        [Test]
        public void TestGradeBesideThePie()
        {
            AddStep("setting", () => config.SetValue(OsuSetting.SectionPassFail, true));
            AddStep("create", () =>
            {
                clock = new ManualClock();
                health = new DrainingHealthProcessor(0);
                health.Health.Value = 1;

                var pie = new osu.Framework.Graphics.Shapes.Box { Size = new osuTK.Vector2(30), Anchor = osu.Framework.Graphics.Anchor.TopRight, Origin = osu.Framework.Graphics.Anchor.TopRight };

                Children = new osu.Framework.Graphics.Drawable[]
                {
                    pie,
                    new SectionPassFailOverlay(new[] { new BreakPeriod(1000, 7000) }, health)
                    {
                        Clock = new FramedClock(clock),
                        Rank = new osu.Framework.Bindables.Bindable<osu.Game.Scoring.ScoreRank>(osu.Game.Scoring.ScoreRank.A),
                        FindProgressPie = () => pie,
                    },
                };
            });
            AddStep("break starts", () => clock.CurrentTime = 1010);
            AddStep("faded in", () => clock.CurrentTime = 1800);
            AddAssert("grade shown with the A", () => sprite(3).Alpha == 1 && sprite(3).Texture != null);
            AddStep("near the end", () => clock.CurrentTime = 6500);
            AddAssert("grade gone", () => sprite(3).Alpha == 0);
        }

        [Test]
        public void TestFail()
        {
            create(true, 0.3);
            AddStep("break starts", () => clock.CurrentTime = 1500);
            AddStep("its middle", () => clock.CurrentTime = 4150);
            AddAssert("fail shown", () => sprite(1).Alpha == 1 && sprite(0).Alpha == 0);
        }

        [Test]
        public void TestOff()
        {
            create(false, 0.5);
            AddStep("break starts", () => clock.CurrentTime = 1500);
            AddStep("its middle", () => clock.CurrentTime = 4030);
            AddAssert("nothing", () => sprite(0).Alpha == 0 && sprite(1).Alpha == 0);
        }

        private Sprite sprite(int index) => this.ChildrenOfType<SectionPassFailOverlay>().Single().ChildrenOfType<Sprite>().ElementAt(index);
    }
}

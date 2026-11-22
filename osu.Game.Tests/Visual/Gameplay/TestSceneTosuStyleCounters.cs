// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD.Counters;
using osu.Game.Tests.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>Reprise: the Simplistic (jassper0) and DarkAndWhite (cyperdark) tosu pp counters, in the game.</summary>
    public partial class TestSceneTosuStyleCounters : OsuTestScene
    {
        /// <summary>Set by a screenshot runner: called with a name once the counters are shown.</summary>
        public static Action<string>? Screenshot;

        [Cached(typeof(ScoreProcessor))]
        private readonly ScoreProcessor scoreProcessor = new OsuScoreProcessor();

        [Cached]
        private readonly GameplayState gameplayState = TestGameplayState.Create(new OsuRuleset());

        private Bindable<JudgementResult> lastJudgement => (Bindable<JudgementResult>)gameplayState.LastJudgementResult;

        private double time;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("set up", () =>
            {
                var ruleset = new OsuRuleset();
                scoreProcessor.ApplyBeatmap(CreateWorkingBeatmap(ruleset.RulesetInfo).GetPlayableBeatmap(ruleset.RulesetInfo));
                lastJudgement.SetDefault();
                time = 0;

                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = ColourInfo2() },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 60),
                            Children = new Drawable[]
                            {
                                new SimplisticPerformancePointsCounter { Scale = Vector2.One },
                                new DarkAndWhitePerformancePointsCounter { Scale = Vector2.One },
                            },
                        },
                    },
                };
            });
        }

        // Something like a dimmed gameplay background, to judge the counters on.
        private static osu.Framework.Graphics.Colour.ColourInfo ColourInfo2() =>
            osu.Framework.Graphics.Colour.ColourInfo.GradientVertical(Color4Extensions.FromHex("3a4a6b"), Color4Extensions.FromHex("1b2233"));

        [Test]
        public void TestPlay()
        {
            AddStep("play from the first note", () => setProgress(0.62));
            AddStep("hit 120 greats, 4 100s, a 50", () =>
            {
                apply(HitResult.Great, 120);
                apply(HitResult.Ok, 4);
                apply(HitResult.Meh, 1);
            });

            AddUntilStep("pp counted", () => stats().All(s => s.PerformancePoints.Value > 0));
            AddAssert("full combo pp the same, unbroken", () => stats().All(s => s.FullComboPerformancePoints.Value >= s.PerformancePoints.Value));
            AddAssert("no badges yet", () => this.ChildrenOfType<SimplisticPerformancePointsCounter>().Single()
                                                  .ChildrenOfType<CountingSpriteText>().Count(t => t.Text.ToString().EndsWith("xSB", StringComparison.Ordinal)
                                                                                                  && t.Parent!.Parent!.Alpha > 0) == 0);

            AddStep("miss one, break a slider", () =>
            {
                apply(HitResult.Miss, 1);
                applyTickMiss();
            });

            AddUntilStep("counts shown", () => stats().All(s => s.Oks.Value == 4 && s.Mehs.Value == 1 && s.Misses.Value == 1 && s.SliderBreaks.Value == 1));
            AddUntilStep("full combo pp above pp", () => stats().All(s => s.FullComboPerformancePoints.Value > s.PerformancePoints.Value));
            AddUntilStep("DarkAndWhite shows them", () => this.ChildrenOfType<DarkAndWhitePerformancePointsCounter>().Single()
                                                              .ChildrenOfType<osu.Game.Graphics.Sprites.OsuSpriteText>()
                                                              .Select(t => t.Text.ToString()).ToList() is var texts
                                                          && texts.Contains("4") && texts.Contains("1") && texts.Any(t => t.EndsWith("PP", StringComparison.Ordinal) && t != "0PP"));

            int fullComboBefore = 0;
            AddStep("miss 20 more", () =>
            {
                fullComboBefore = stats()[0].FullComboPerformancePoints.Value;
                apply(HitResult.Miss, 20);
            });
            AddUntilStep("misses don't change the full combo pp (as tosu's: a miss counts as a 300)",
                () => stats().All(s => s.Misses.Value == 21 && s.FullComboPerformancePoints.Value == fullComboBefore));
            AddStep("another 100", () => apply(HitResult.Ok, 1));
            AddUntilStep("a 100 lowers it", () => stats().All(s => s.Oks.Value == 5 && s.FullComboPerformancePoints.Value < fullComboBefore));
            AddWaitStep("let the numbers count up", 10);
            AddStep("screenshot", () => Screenshot?.Invoke("tosu-counters"));
        }

        private LivePlayStats[] stats() => this.ChildrenOfType<LivePlayStats>().ToArray();

        private void setProgress(double progress)
        {
            foreach (var s in stats())
            {
                s.Started.Value = true;
                s.SongProgress.Value = progress;
                s.MapProgress.Value = progress;
            }
        }

        private void apply(HitResult result, int count)
        {
            for (int i = 0; i < count; i++)
            {
                time += 300;
                var circle = new HitCircle { StartTime = time };
                lastJudgement.Value = new OsuJudgementResult(circle, circle.CreateJudgement()) { Type = result };
                scoreProcessor.ApplyResult(lastJudgement.Value);
            }
        }

        private void applyTickMiss()
        {
            time += 300;
            var tick = new SliderTick { StartTime = time };
            lastJudgement.Value = new OsuJudgementResult(tick, tick.CreateJudgement()) { Type = HitResult.LargeTickMiss };
            scoreProcessor.ApplyResult(lastJudgement.Value);
        }
    }
}

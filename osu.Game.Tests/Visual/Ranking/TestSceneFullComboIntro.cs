// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Modern;
using osu.Game.Tests.Resources;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Ranking
{
    /// <summary>Reprise: the redesign's full combo moment before the results, and when a play gets it.</summary>
    public partial class TestSceneFullComboIntro : OsuManualInputManagerTestScene
    {
        /// <summary>Set by a screenshot runner: called with a name at moments of it.</summary>
        public static Action<string>? Screenshot;

        private FullComboIntro intro = null!;
        private bool finished;

        private void show() => AddStep("show it", () =>
        {
            finished = false;
            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // Something colourful to darken, as a beatmap's background would be.
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientHorizontal(Color4.DeepPink, Color4.SeaGreen),
                    },
                    intro = new FullComboIntro { Finished = () => finished = true },
                }
            };
        });

        [Test]
        public void TestPlaysThrough()
        {
            show();
            AddWaitStep("mid-sweep", 1);
            AddStep("screenshot", () => Screenshot?.Invoke("fc-sweep"));
            AddUntilStep("stars in", () => intro.Time.Current > 650);
            AddStep("screenshot", () => Screenshot?.Invoke("fc-done"));
            AddUntilStep("finished", () => finished);
            AddAssert("within about a second", () => FullComboIntro.DURATION, () => Is.LessThan(1200));
        }

        [Test]
        public void TestSpaceSkips()
        {
            show();
            AddStep("press space", () => InputManager.Key(Key.Space));
            AddAssert("finished at once", () => finished);
        }

        [Test]
        public void TestWhichPlaysGetIt()
        {
            AddAssert("full combo", () => FullComboIntro.IsFullCombo(play(maxCombo: 100, misses: 0)));
            AddAssert("a combo break", () => !FullComboIntro.IsFullCombo(play(maxCombo: 99, misses: 0)));
            AddAssert("a miss", () => !FullComboIntro.IsFullCombo(play(maxCombo: 100, misses: 1)));
            AddAssert("failed", () => !FullComboIntro.IsFullCombo(play(maxCombo: 100, misses: 0, passed: false)));
        }

        private static ScoreInfo play(int maxCombo, int misses, bool passed = true)
        {
            var score = TestResources.CreateTestScoreInfo(new OsuRuleset().RulesetInfo);
            score.MaxCombo = maxCombo;
            score.Passed = passed;
            score.Rank = passed ? ScoreRank.A : ScoreRank.F;
            score.Statistics = new System.Collections.Generic.Dictionary<HitResult, int> { [HitResult.Great] = 100 - misses, [HitResult.Miss] = misses };
            score.MaximumStatistics = new System.Collections.Generic.Dictionary<HitResult, int> { [HitResult.Great] = 100 };
            return score;
        }
    }
}

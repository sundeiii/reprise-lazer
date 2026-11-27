// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests
{
    /// <summary>Reprise: osu!stable's ScoreV1 and ScoreV2 as the game keeps them (<see cref="OsuStableScoring"/>).</summary>
    [TestFixture]
    public class StableScoringTest
    {
        private static (OsuScoreProcessor Processor, IBeatmap Playable, IWorkingBeatmap Working) play(Func<HitObject, JudgementResult, HitResult>? judge = null)
        {
            var ruleset = new OsuRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo);
            // No spinners: stable's spinner bonus depends on how it's spun, which the simulator guesses.
            beatmap.HitObjects.RemoveAll(h => h is IHasDuration && h is not IHasPath);
            var working = new TestWorkingBeatmap(beatmap);
            var playable = working.GetPlayableBeatmap(ruleset.RulesetInfo, Array.Empty<Mod>());

            var processor = new OsuScoreProcessor();
            processor.ApplyBeatmap(playable);

            foreach (var obj in playable.HitObjects)
                apply(processor, obj, judge);

            return (processor, playable, working);
        }

        private static void apply(ScoreProcessor processor, HitObject obj, Func<HitObject, JudgementResult, HitResult>? judge)
        {
            foreach (var nested in obj.NestedHitObjects)
                apply(processor, nested, judge);

            var result = new OsuJudgementResult(obj, obj.CreateJudgement());
            result.Type = judge?.Invoke(obj, result) ?? result.Judgement.MaxResult;
            processor.ApplyResult(result);
        }

        [Test]
        public void AnSS_IsAMillionInScoreV2()
        {
            var (processor, _, _) = play();
            Assert.That(processor.GetDisplayScore(ScoringMode.ScoreV2), Is.EqualTo(1_000_000).Within(1));
        }

        [Test]
        public void AnSS_IsStablesScoreV1()
        {
            var (processor, playable, working) = play();
            var attributes = new OsuLegacyScoreSimulator().Simulate(working, playable);

            Assert.That((double)processor.GetDisplayScore(ScoringMode.ScoreV1), Is.EqualTo((double)(attributes.AccuracyScore + attributes.ComboScore)).Within(0.002).Percent);
        }

        [Test]
        public void AMiss_CostsMoreInTheMiddle()
        {
            var circles = play().Playable.HitObjects.OfType<HitCircle>().Select(c => c.StartTime).ToList();
            var (start, _, _) = play(missAt(circles[0]));
            var (middle, _, _) = play(missAt(circles[circles.Count / 2]));

            Assert.That(middle.GetDisplayScore(ScoringMode.ScoreV1), Is.LessThan(start.GetDisplayScore(ScoringMode.ScoreV1)));
            Assert.That(middle.GetDisplayScore(ScoringMode.ScoreV2), Is.LessThan(start.GetDisplayScore(ScoringMode.ScoreV2)));

            static Func<HitObject, JudgementResult, HitResult> missAt(double time) =>
                (obj, r) => obj is HitCircle && !(obj is SliderHeadCircle) && obj.StartTime == time ? HitResult.Miss : r.Judgement.MaxResult;
        }

        [Test]
        public void TheResultsScreen_GetsThePlaysOwnScores()
        {
            var (processor, _, _) = play();
            var score = new ScoreInfo { Ruleset = new OsuRuleset().RulesetInfo };
            processor.PopulateScore(score);

            Assert.That(score.GetDisplayScore(ScoringMode.ScoreV2), Is.EqualTo(processor.GetDisplayScore(ScoringMode.ScoreV2)));
            Assert.That(score.GetDisplayScore(ScoringMode.ScoreV1), Is.EqualTo(processor.GetDisplayScore(ScoringMode.ScoreV1)));
        }

        [Test]
        public void AStoredSS_IsEstimatedAsAMillion()
        {
            var (processor, _, _) = play();
            var score = new ScoreInfo { Ruleset = new OsuRuleset().RulesetInfo };
            processor.PopulateScore(score);
            score.StableScoreV2 = null;

            Assert.That(score.GetDisplayScore(ScoringMode.ScoreV2), Is.EqualTo(1_000_000).Within(1000));
        }

        [Test]
        public void ScoreV2Mods_AreStables()
        {
            Assert.That(OsuStableScoring.ModMultiplier(new Mod[] { new osu.Game.Rulesets.Osu.Mods.OsuModHardRock(), new osu.Game.Rulesets.Osu.Mods.OsuModDoubleTime() }, true), Is.EqualTo(1.32).Within(1e-9));
            Assert.That(OsuStableScoring.ModMultiplier(new Mod[] { new osu.Game.Rulesets.Osu.Mods.OsuModHardRock(), new osu.Game.Rulesets.Osu.Mods.OsuModDoubleTime() }, false), Is.EqualTo(1.06 * 1.12).Within(1e-9));
            Assert.That(OsuStableScoring.ModMultiplier(new Mod[] { new osu.Game.Rulesets.Osu.Mods.OsuModRelax() }, false), Is.EqualTo(1));
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Scoring
{
    public partial class OsuScoreProcessor : ScoreProcessor
    {
        public OsuScoreProcessor()
            : base(new OsuRuleset())
        {
        }

        // Reprise: osu!stable's ScoreV1 and ScoreV2 of the play, hit by hit (ScoringMode.ScoreV1 / ScoreV2).
        private readonly OsuStableScoring stable = new OsuStableScoring();

        public override void ApplyBeatmap(IBeatmap beatmap)
        {
            stable.ApplyBeatmap(beatmap);
            base.ApplyBeatmap(beatmap);
        }

        protected override void ApplyScoreChange(JudgementResult result)
        {
            base.ApplyScoreChange(result);
            if (!result.FailedAtJudgement || ApplyNewJudgementsWhenFailed)
                stable.Apply(result);
        }

        protected override void RemoveScoreChange(JudgementResult result)
        {
            base.RemoveScoreChange(result);
            stable.Revert(result);
        }

        protected override void Reset(bool storeResults)
        {
            base.Reset(storeResults);
            stable.Reset(storeResults);
        }

        public override long? GetStableScore(ScoringMode mode) => mode switch
        {
            ScoringMode.ScoreV1 => stable.ScoreV1(Mods.Value),
            ScoringMode.ScoreV2 => stable.ScoreV2(Mods.Value),
            _ => null,
        };

        public override void PopulateScore(ScoreInfo score)
        {
            base.PopulateScore(score);
            score.StableScoreV1 = stable.ScoreV1(Mods.Value);
            score.StableScoreV2 = stable.ScoreV2(Mods.Value);
        }

        public override ScoreRank RankFromScore(double accuracy, IReadOnlyDictionary<HitResult, int> results)
        {
            ScoreRank rank = base.RankFromScore(accuracy, results);

            switch (rank)
            {
                case ScoreRank.S:
                case ScoreRank.X:
                    if (results.GetValueOrDefault(HitResult.Miss) > 0)
                        rank = ScoreRank.A;
                    break;
            }

            return rank;
        }

        protected override HitEvent CreateHitEvent(JudgementResult result)
            => base.CreateHitEvent(result).With((result as OsuHitCircleJudgementResult)?.CursorPositionAtHit);
    }
}

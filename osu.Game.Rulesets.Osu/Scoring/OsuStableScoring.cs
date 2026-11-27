// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Scoring
{
    /// <summary>
    /// Reprise: osu!stable's ScoreV1 and ScoreV2 of a play, worked out hit by hit as stable does (its values for each part of
    /// a slider, its difficulty multiplier, its mod multipliers), for <see cref="ScoringMode.ScoreV1"/> and
    /// <see cref="ScoringMode.ScoreV2"/>.
    /// </summary>
    /// <remarks>
    /// ScoreV1: each hit gives its value, plus <c>max(0, combo − 1) × (value / 25) × difficulty × mods</c> for circles, sliders
    /// and spinners. ScoreV2: <c>700,000 × combo part ÷ its maximum + 300,000 × accuracy¹⁰ × judged ÷ all + spinner bonus</c>,
    /// times the mods, each hit adding <c>value × (1 + combo ÷ 10)</c> to the combo part. Relax and Autopilot count as ×1
    /// (stable's ×0 doesn't suit a server that ranks them).
    /// </remarks>
    public class OsuStableScoring
    {
        private long v1Score;
        private long v1ComboBonus;
        private double v2ComboPortion;
        private double v2BonusPortion;
        private double basicValue;
        private int basicJudged;

        private double maximumV2ComboPortion;
        private int maximumBasicJudged;

        private int difficultyMultiplier = 1;

        /// <summary>Stable's difficulty multiplier ("peppy stars") of the map, from its own settings, objects and drain time.</summary>
        public void ApplyBeatmap(IBeatmap beatmap)
        {
            int objectCount = beatmap.HitObjects.Count;
            int drainLength = 0;

            if (objectCount > 0)
            {
                int breakLength = beatmap.Breaks.Select(b => (int)Math.Round(b.EndTime) - (int)Math.Round(b.StartTime)).Sum();
                drainLength = ((int)Math.Round(beatmap.HitObjects[^1].StartTime) - (int)Math.Round(beatmap.HitObjects[0].StartTime) - breakLength) / 1000;
            }

            difficultyMultiplier = LegacyRulesetExtensions.CalculateDifficultyPeppyStars(beatmap.BeatmapInfo.Difficulty, objectCount, drainLength);
        }

        public void Apply(JudgementResult result) => change(result, 1);

        public void Revert(JudgementResult result) => change(result, -1);

        /// <summary>Zeroes the play; with <paramref name="storeMaximum"/>, what was played (an autoplay run) is the maximum first.</summary>
        public void Reset(bool storeMaximum)
        {
            if (storeMaximum)
            {
                maximumV2ComboPortion = v2ComboPortion;
                maximumBasicJudged = basicJudged;
            }

            v1Score = 0;
            v1ComboBonus = 0;
            pendingSliderJudgement = -1;
            sliderJudgementAtTail.Clear();
            v2ComboPortion = 0;
            v2BonusPortion = 0;
            basicValue = 0;
            basicJudged = 0;
        }

        public long ScoreV1(IReadOnlyList<Mod> mods) => v1Score + (long)(v1ComboBonus * ModMultiplier(mods, false));

        public long ScoreV2(IReadOnlyList<Mod> mods)
        {
            if (maximumBasicJudged == 0 || maximumV2ComboPortion <= 0)
                return 0;

            double accuracy = basicJudged > 0 ? basicValue / (300.0 * basicJudged) : 1;
            double total = v2ComboPortion / maximumV2ComboPortion * 700000
                           + Math.Pow(accuracy, 10) * ((double)basicJudged / maximumBasicJudged) * 300000
                           + v2BonusPortion;

            return (long)(total * ModMultiplier(mods, true));
        }

        /// <summary>Stable's mod multiplier for osu! (ScoreV2: HR ×1.10 and DT ×1.20 instead of ×1.06 and ×1.12).</summary>
        public static double ModMultiplier(IEnumerable<Mod> mods, bool scoreV2)
        {
            double multiplier = 1;

            foreach (var mod in mods)
            {
                switch (mod)
                {
                    case OsuModNoFail:
                    case OsuModEasy:
                        multiplier *= 0.5;
                        break;

                    case OsuModHalfTime:
                    case OsuModDaycore:
                        multiplier *= 0.3;
                        break;

                    case OsuModHidden:
                        multiplier *= 1.06;
                        break;

                    case OsuModHardRock:
                        multiplier *= scoreV2 ? 1.10 : 1.06;
                        break;

                    case OsuModDoubleTime:
                    case OsuModNightcore:
                        multiplier *= scoreV2 ? 1.20 : 1.12;
                        break;

                    case OsuModFlashlight:
                        multiplier *= 1.12;
                        break;

                    case OsuModSpunOut:
                        multiplier *= 0.9;
                        break;
                }
            }

            return multiplier;
        }

        // lazer's sliders judge their accuracy at the head; stable counts it at the slider's end, with the combo there:
        // the head's judgement waits for the tail (what each tail added is kept, for rewinding).
        private int pendingSliderJudgement = -1;
        private readonly Dictionary<JudgementResult, int> sliderJudgementAtTail = new Dictionary<JudgementResult, int>();

        private void change(JudgementResult result, int sign)
        {
            var hit = Hit.Of(result);

            if (hit.Bonus)
            {
                v1Score += sign * hit.V1Value;
                v2BonusPortion += sign * hit.V2Value;
                return;
            }

            v1Score += sign * hit.V1Value;

            if (hit.ComboMultiplier)
                v1ComboBonus += sign * comboBonus(result.ComboAtJudgement, hit.V1Value);

            if (hit.V2Value > 0)
                v2ComboPortion += sign * hit.V2Value * (1 + result.ComboAfterJudgement / 10.0);

            if (hit.Basic)
            {
                basicJudged += sign;
                basicValue += sign * hit.AccuracyValue;
            }

            if (result.HitObject is SliderHeadCircle && result.Type.IsBasic())
                pendingSliderJudgement = sign > 0 ? hit.AccuracyValue : -1;

            if (result.HitObject is SliderTailCircle)
            {
                int judgement;

                if (sign > 0)
                {
                    judgement = pendingSliderJudgement;
                    pendingSliderJudgement = -1;
                    if (judgement < 0)
                        return;

                    sliderJudgementAtTail[result] = judgement;
                }
                else if (!sliderJudgementAtTail.Remove(result, out judgement))
                    return;

                // The slider's own judgement: its value, the combo bonus at its end (no combo of its own).
                v1Score += sign * judgement;
                v1ComboBonus += sign * comboBonus(result.ComboAfterJudgement, judgement);
                v2ComboPortion += sign * judgement * (1 + result.ComboAfterJudgement / 10.0);
            }
        }

        private long comboBonus(int combo, int value) => (long)Math.Max(0, combo - 1) * (value / 25) * difficultyMultiplier;

        /// <summary>One judgement as stable scores it.</summary>
        private readonly record struct Hit(int V1Value, int V2Value, bool ComboMultiplier, bool Bonus, bool Basic, int AccuracyValue)
        {
            public static Hit Of(JudgementResult result)
            {
                bool hit = result.IsHit;
                int basic = basicValue(result.Type);

                switch (result.HitObject)
                {
                    case SpinnerBonusTick:
                        return new Hit(hit ? 1100 : 0, hit ? 500 : 0, false, true, false, 0);

                    case SpinnerTick:
                        return new Hit(hit ? 100 : 0, hit ? 100 : 0, false, true, false, 0);

                    case SliderTick:
                        return new Hit(hit ? 10 : 0, hit ? 10 : 0, false, false, false, 0);

                    case SliderRepeat:
                    case SliderTailCircle:
                        return new Hit(hit ? 30 : 0, hit ? 30 : 0, false, false, false, 0);

                    case SliderHeadCircle when !result.Type.IsBasic():
                        // Classic sliders: the head is a 30, the slider itself gets the judgement.
                        return new Hit(hit ? 30 : 0, hit ? 30 : 0, false, false, false, 0);

                    case SliderHeadCircle:
                        // lazer's sliders: the head is stable's 30, and its judgement is the slider's (counted at the tail).
                        return new Hit(hit ? 30 : 0, hit ? 30 : 0, false, false, true, basic);

                    case Slider:
                    case HitCircle:
                    case Spinner:
                    default:
                        if (!result.Type.IsBasic())
                            return default;

                        return new Hit(basic, basic, basic > 0, false, true, basic);
                }
            }

            private static int basicValue(HitResult type)
            {
                switch (type)
                {
                    case HitResult.Great:
                    case HitResult.Perfect:
                        return 300;

                    case HitResult.Ok:
                    case HitResult.Good:
                        return 100;

                    case HitResult.Meh:
                        return 50;

                    default:
                        return 0;
                }
            }
        }

        /// <summary>
        /// A stored play's ScoreV2, estimated from its counts and max combo (the order of its hits isn't kept): its longest
        /// combo once, the rest of its combo in streaks as long as that.
        /// </summary>
        public static long EstimateScoreV2(IReadOnlyDictionary<HitResult, int> statistics, IReadOnlyDictionary<HitResult, int> maximumStatistics,
                                           int maxCombo, IEnumerable<Mod> mods)
        {
            int great = statistics.GetValueOrDefault(HitResult.Great), ok = statistics.GetValueOrDefault(HitResult.Ok);
            int meh = statistics.GetValueOrDefault(HitResult.Meh), miss = statistics.GetValueOrDefault(HitResult.Miss);
            int judged = great + ok + meh + miss;
            int all = maximumStatistics.Where(s => s.Key.IsBasic()).Sum(s => s.Value);
            if (all == 0 || judged == 0)
                return 0;

            double accuracy = (300.0 * great + 100 * ok + 50 * meh) / (300.0 * judged);

            // The map's combo (a stable play's maximum counts its slider ticks as legacy_combo_increase, which its own counts
            // leave out): the play is taken to have all of it but what its misses broke, its longest combo once.
            int maximumCombo = Math.Max(1, maximumStatistics.Where(s => s.Key.IncreasesCombo()).Sum(s => s.Value));
            int comboHits = Math.Clamp((int)Math.Round(maximumCombo * (1 - (double)miss / all)), 0, maximumCombo);
            int longest = Math.Clamp(maxCombo, 0, maximumCombo);
            comboHits = Math.Max(comboHits, longest);
            int rest = comboHits - longest;

            double streaks = streak(longest);
            if (longest > 0)
                streaks += rest / longest * streak(longest) + streak(rest % longest);

            double comboPart = streaks / streak(maximumCombo) * accuracy;
            double total = 700000 * comboPart + 300000 * Math.Pow(accuracy, 10) * Math.Min(1, (double)judged / all);
            return (long)(total * ModMultiplier(mods, true));

            static double streak(int n) => n + n * (n + 1) / 20.0;
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
// This file is originally created by GooGuTeam.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Framework.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to aim patterns when the Relax mod is enabled.
    /// </summary>
    public class Relax : StrainSkill
    {
        public readonly bool IncludeSliders;

        public Relax(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private const double skill_multiplier = 24.16;
        private const double strain_decay_base = 0.15;

        private double currentStrain;

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecay(double ms) => DiffUtils.Pow(strain_decay_base, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous().StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            var osuCurrent = (OsuDifficultyHitObject)current;

            currentStrain *= strainDecay(osuCurrent.DeltaTime);
            currentStrain += RelaxAimEvaluator.EvaluateDifficultyOf(osuCurrent, IncludeSliders) * skill_multiplier;

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);

            return currentStrain;
        }

        public override double DifficultyValue()
        {
            const int reduced_section_count = 10;
            const double reduced_strain_baseline = 0.75;

            double difficulty = 0;
            double weight = 1;

            // Sections with 0 strain are excluded as they do not contribute to difficulty.
            List<double> strains = GetCurrentStrainPeaks()
                                   .Where(p => p > 0)
                                   .OrderDescending()
                                   .ToList();

            // Reduce the highest strains to account for extreme difficulty spikes.
            for (int i = 0; i < Math.Min(strains.Count, reduced_section_count); i++)
            {
                double scale = Math.Log10(
                    Interpolation.Lerp(
                        1,
                        10,
                        Math.Clamp((float)i / reduced_section_count, 0, 1)
                    )
                );

                strains[i] *= Interpolation.Lerp(
                    reduced_strain_baseline,
                    1.0,
                    scale
                );
            }

            // Weighted sum from highest to lowest strain.
            foreach (double strain in strains.OrderDescending())
            {
                difficulty += strain * weight;
                weight *= DecayWeight;
            }

            return difficulty;
        }

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();
            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => DiffUtils.Logistic(strain / maxSliderStrain, 0.5, 12.0));
        }
    }
}

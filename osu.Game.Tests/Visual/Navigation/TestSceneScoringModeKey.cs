// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Configuration;
using osu.Game.Rulesets.Scoring;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    /// <summary>Reprise: F7 (by default) switches between standardised and classic scoring, anywhere in the game.</summary>
    public partial class TestSceneScoringModeKey : OsuGameTestScene
    {
        [Test]
        public void TestF7TogglesScoringMode()
        {
            AddStep("standardised", () => Game.LocalConfig.SetValue(OsuSetting.ScoreDisplayMode, ScoringMode.Standardised));

            AddStep("press F7", () => InputManager.Key(Key.F7));
            AddAssert("classic", () => Game.LocalConfig.Get<ScoringMode>(OsuSetting.ScoreDisplayMode), () => Is.EqualTo(ScoringMode.Classic));

            AddStep("press F7", () => InputManager.Key(Key.F7));
            AddAssert("standardised again", () => Game.LocalConfig.Get<ScoringMode>(OsuSetting.ScoreDisplayMode), () => Is.EqualTo(ScoringMode.Standardised));
        }
    }
}

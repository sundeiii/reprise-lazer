// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Modern;
using osu.Game.Skinning.Ranking;

namespace osu.Game.Tests.Visual.Ranking
{
    /// <summary>The redesigned results screen's main score screen with a full play, to compare with the design (1920x1080).</summary>
    public partial class TestSceneModernResultsScreen : OsuTestScene
    {
        /// <summary>Set by a screenshot runner: called with a name once the screen is shown.</summary>
        public static Action<string>? Screenshot;

        private bool exported;
        private readonly Bindable<bool> favourited = new Bindable<bool>();

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("load screen", () =>
            {
                var score = TestSceneLegacyRankingPanel.CreateScore();
                score.TotalScore = 956_789;
                score.Accuracy = 0.9768;
                score.MaxCombo = 1_402;
                score.Rank = ScoreRank.S;
                score.Position = 1;
                score.PP = 284;
                score.Mods = new osu.Game.Rulesets.Mods.Mod[] { new OsuModHardRock(), new OsuModHidden(), new OsuModDoubleTime { SpeedChange = { Value = 1.16 } } };
                score.Statistics[HitResult.Great] = 1_201;
                score.Statistics[HitResult.Ok] = 41;
                score.Statistics[HitResult.Meh] = 3;
                score.Statistics[HitResult.Miss] = 0;
                score.Statistics[HitResult.SliderTailHit] = 300;
                score.Statistics[HitResult.LargeBonus] = 4;
                score.Statistics[HitResult.SmallBonus] = 40;
                score.MaximumStatistics[HitResult.Great] = 1_245;
                score.MaximumStatistics[HitResult.LargeTickHit] = 0;
                score.MaximumStatistics[HitResult.SliderTailHit] = 302;
                score.MaximumStatistics[HitResult.LargeBonus] = 10;
                score.MaximumStatistics[HitResult.SmallBonus] = 60;

                exported = false;
                favourited.Value = false;

                var details = new LegacyRankingDetails
                {
                    HasSecondPage = false,
                    OnlineBeatmap = new Bindable<APIBeatmap?>(new APIBeatmap { BeatmapSet = new APIBeatmapSet { FavouriteCount = 4_231 } }),
                    ExportReplay = () => exported = true,
                    ToggleFavourite = () => favourited.Value = !favourited.Value,
                    Favourited = favourited,
                    Back = () => { },
                };

                Children = new Drawable[]
                {
                    // Something colourful behind, as a beatmap's background would be.
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(Color4Extensions.FromHex("e05a7a"), Color4Extensions.FromHex("6a3a8c")),
                    },
                    new ModernResultsScreen(score, () => { }, () => { }, details)
                    {
                        ShowAdvancedInfo = () => { },
                        ShowRanking = () => { },
                    },
                };
            });

            AddUntilStep("screen loaded", () => this.ChildrenOfType<ModernResultsScreen>().SingleOrDefault()?.IsLoaded == true);
        }

        [Test]
        public void TestShown()
        {
            AddWaitStep("let it settle", 15);
            AddStep("screenshot", () => Screenshot?.Invoke("modern-main"));
            AddWaitStep("saved", 5);
        }

        [Test]
        public void TestFavouriteAndExport()
        {
            AddAssert("not exported", () => !exported);
            AddStep("toggle favourite", () => favourited.Value = !favourited.Value);
            AddAssert("favourited", () => favourited.Value);
        }
    }
}

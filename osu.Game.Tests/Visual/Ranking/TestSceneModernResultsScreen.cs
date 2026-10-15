// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
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
                score.HitEvents = createHitEvents();

                exported = false;
                favourited.Value = false;

                var details = new LegacyRankingDetails
                {
                    HasSecondPage = false,
                    PreviousBest = new Bindable<SoloScoreInfo?>(new SoloScoreInfo { TotalScore = 912_345, Accuracy = 0.9681, MaxCombo = 1_120, PP = 261 }),
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
                        ShowRanking = () => { },
                    },
                };
            });

            AddUntilStep("screen loaded", () => this.ChildrenOfType<ModernResultsScreen>().SingleOrDefault()?.IsLoaded == true);
        }

        /// <summary>A play's worth of hits on a 3-minute map: mostly on time, a little late, a few 100s and 50s and two misses.</summary>
        private static List<HitEvent> createHitEvents()
        {
            var random = new Random(1);
            var events = new List<HitEvent>();
            HitCircle? last = null;

            for (int i = 0; i < 1_245; i++)
            {
                var circle = new HitCircle { StartTime = i * 145 };
                circle.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { OverallDifficulty = 8 });

                // A normal spread (Box-Muller) around 3ms late, wider through the middle of the map.
                double spread = 12 + 8 * Math.Sin(Math.PI * i / 1_245);
                double offset = 3 + spread * Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
                var result = circle.HitWindows.ResultFor(offset);

                if (i is 400 or 901 || result == HitResult.None)
                    events.Add(new HitEvent(0, 1, HitResult.Miss, circle, last, null));
                else
                    events.Add(new HitEvent(offset, 1, result, circle, last, null));

                last = circle;
            }

            return events;
        }

        [Test]
        public void TestShown()
        {
            AddWaitStep("let it settle", 15);
            AddStep("screenshot", () => Screenshot?.Invoke("modern-main"));
            AddWaitStep("saved", 5);
        }

        [Test]
        public void TestAdvancedInfo()
        {
            AddStep("open advanced info", () => this.ChildrenOfType<OsuClickableContainer>()
                                                     .First(b => b.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == "More Info"))
                                                     .TriggerClick());
            AddUntilStep("advanced info shown", () => this.ChildrenOfType<AdvancedInfoPage>().SingleOrDefault()?.Alpha == 1);
            AddWaitStep("let it settle", 15);
            AddStep("screenshot", () => Screenshot?.Invoke("modern-advanced"));
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

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
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Users;
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
            AddStep("load screen", () => loadScreen(false));
            AddUntilStep("screen loaded", () => this.ChildrenOfType<ModernResultsScreen>().SingleOrDefault()?.IsLoaded == true);
        }

        /// <param name="justSet">As a play just set (its numbers count up), rather than one looked at later.</param>
        private void loadScreen(bool justSet)
        {
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
                score.BeatmapInfo!.OnlineID = 1_234;
                score.BeatmapInfo.Status = BeatmapOnlineStatus.Ranked;
                // A long difficulty name on a high star map (cut short, readable).
                score.BeatmapInfo.DifficultyName = "Ascending from Fallen Elegance, the Reign of the Kingdom Roams Free Forevermore";
                score.BeatmapInfo.StarRating = 7.98;

                // The leaderboard: a page of players, the player's own best among them.
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is not GetScoresRequest scores)
                        return false;

                    var top = Enumerable.Range(0, 12).Select(i => createOnlineScore(i)).ToList();
                    scores.TriggerSuccess(new APIScoresCollection
                    {
                        Scores = top,
                        ScoresCount = 12,
                        UserScore = new APIScoreWithPosition { Position = 4, Score = top[3] },
                    });
                    return true;
                };

                exported = false;
                favourited.Value = false;

                var details = new LegacyRankingDetails
                {
                    HasSecondPage = justSet,
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
                    new ModernResultsScreen(score, () => { }, () => { }, details),
                };
            }
        }

        private static readonly string[] names = { "Alice", "mrekk", "Lilian", "Accolibed", "lifeline", "aetrna", "Mrekk2", "Rafis", "Cookiezi", "WhiteCat", "Vaxei", "Chicony" };
        private static readonly string[] countries = { "MY", "AU", "TH", "KR", "US", "DE", "AU", "PL", "KR", "DE", "US", "TW" };

        private static SoloScoreInfo createOnlineScore(int i) => new SoloScoreInfo
        {
            ID = 100 + (ulong)i,
            BeatmapID = 1_234,
            RulesetID = 0,
            TotalScore = 1_000_000 - i * 23_456,
            Accuracy = 0.9950 - i * 0.004,
            MaxCombo = 1_402 - i * 37,
            Rank = i < 2 ? ScoreRank.X : i < 7 ? ScoreRank.S : ScoreRank.A,
            PP = 420 - i * 13.7,
            Passed = true,
            EndedAt = DateTimeOffset.Now.AddDays(-i * i * 9),
            User = new APIUser { Id = 2 + i, Username = names[i], CountryCode = Enum.Parse<CountryCode>(countries[i]) },
            Statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 1_240 - i * 6,
                [HitResult.Ok] = 5 + i * 5,
                [HitResult.Meh] = i / 3,
                [HitResult.Miss] = i / 4,
            },
            Mods = i % 3 == 0 ? new[] { new APIMod(new OsuModHidden()), new APIMod(new OsuModDoubleTime()) } : i % 3 == 1 ? new[] { new APIMod(new OsuModHardRock()) } : Array.Empty<APIMod>(),
        };

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
        public void TestCountUp()
        {
            AddStep("load as just set", () => loadScreen(true));
            AddUntilStep("screen loaded", () => this.ChildrenOfType<ModernResultsScreen>().SingleOrDefault()?.IsLoaded == true);
            AddWaitStep("partway", 6);
            AddStep("screenshot", () => Screenshot?.Invoke("modern-counting"));
            AddUntilStep("score counted up", () => this.ChildrenOfType<ModernResultsScreen.CountingText>().All(t => t.Transforms.Count() == 0));
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
        public void TestRanking()
        {
            AddStep("open ranking", () => this.ChildrenOfType<OsuClickableContainer>()
                                              .First(b => b.ChildrenOfType<OsuSpriteText>().Any(t => t.Text == "Ranking") && b.Width == 150)
                                              .TriggerClick());
            AddUntilStep("ranking shown", () => this.ChildrenOfType<RankingPage>().SingleOrDefault()?.Alpha == 1);
            AddWaitStep("let it settle", 15);
            AddStep("screenshot", () => Screenshot?.Invoke("modern-ranking"));
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

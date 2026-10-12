// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Skinning;
using osu.Game.Skinning.Ranking;
using osu.Game.Tests.Resources;
using osuTK;

namespace osu.Game.Tests.Visual.Ranking
{
    public partial class TestSceneLegacyRankingPanel : SkinnableTestScene
    {
        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        protected override Ruleset CreateRulesetForSkinProvider() => new OsuRuleset();

        [Test]
        public void TestNoPerfectWithoutFullCombo()
        {
            TestTypicalScore();
            AddAssert("no perfect", () => this.ChildrenOfType<LegacyRankingPerfect>().Any() && this.ChildrenOfType<LegacyRankingPerfect>().All(p => p.DrawWidth == 0));
        }

        [Test]
        public void TestTypicalScore() => showScore(score =>
        {
            score.TotalScore = 1_234_567;
            score.MaxCombo = 567;
            score.MaximumStatistics[HitResult.Great] = 751;
            score.Accuracy = 0.9612;
            score.Rank = ScoreRank.A;
            score.Statistics[HitResult.Great] = 702;
            score.Statistics[HitResult.Ok] = 41;
            score.Statistics[HitResult.Meh] = 3;
            score.Statistics[HitResult.Miss] = 5;
        });

        [Test]
        public void TestScoreFollowsDisplayMode()
        {
            ScoreInfo shown = null!;

            AddStep("standardised", () => config.SetValue(OsuSetting.ScoreDisplayMode, ScoringMode.Standardised));
            showScore(score =>
            {
                score.TotalScore = 489_347;
                score.MaximumStatistics[HitResult.Great] = 751;
                score.Statistics[HitResult.Great] = 702;
                shown = score;
            });
            AddAssert("standardised score", () => scoreTexts().All(t => t == "00489347"));

            AddStep("classic", () => config.SetValue(OsuSetting.ScoreDisplayMode, ScoringMode.Classic));
            AddAssert("classic score", () => scoreTexts().All(t => t == shown.GetDisplayScore(ScoringMode.Classic).ToString("00000000", System.Globalization.CultureInfo.InvariantCulture)) && !scoreTexts().Contains("00489347"));

            AddStep("back to standardised", () => config.SetValue(OsuSetting.ScoreDisplayMode, ScoringMode.Standardised));

            string[] scoreTexts() => this.ChildrenOfType<LegacyRankingPanel>()
                                         .Select(p => p.ChildrenOfType<LegacySpriteText>().First().Text.ToString())
                                         .ToArray();
        }

        [Test]
        public void TestFullCombo()
        {
            showScore(score =>
            {
                score.TotalScore = 9_876_543;
                score.MaxCombo = 1204;
                score.MaximumStatistics[HitResult.Great] = 1204;
                score.Accuracy = 0.9934;
                score.Rank = ScoreRank.S;
                score.Statistics[HitResult.Great] = 949;
                score.Statistics[HitResult.Ok] = 9;
                score.Statistics[HitResult.Meh] = 0;
                score.Statistics[HitResult.Miss] = 0;
            });

            // Older game resources (as this client's) have no ranking-perfect in the default legacy skin.
            AddAssert("perfect shown", () => this.ChildrenOfType<LegacyRankingPerfect>().Any()
                                             && (skins.DefaultClassicSkin.GetTexture(@"ranking-perfect") == null || this.ChildrenOfType<LegacyRankingPerfect>().All(p => p.DrawWidth > 0)));
        }

        [Test]
        public void TestManyMisses() => showScore(score =>
        {
            score.TotalScore = 12_345;
            score.MaxCombo = 12;
            score.MaximumStatistics[HitResult.Great] = 1268;
            score.Accuracy = 0.4521;
            score.Rank = ScoreRank.D;
            score.Statistics[HitResult.Great] = 120;
            score.Statistics[HitResult.Ok] = 88;
            score.Statistics[HitResult.Meh] = 61;
            score.Statistics[HitResult.Miss] = 999;
        });

        // Full at first, a long dip below half in the middle, then recovering.
        private static readonly Vector2[] health = Enumerable.Range(0, 101)
                                                             .Select(i => i / 100f)
                                                             .Select(x => new Vector2(x, Math.Clamp(1 - 0.7f * MathF.Sin(MathF.PI * x) * (0.75f + 0.25f * MathF.Sin(x * 40)), 0, 1)))
                                                             .ToArray();

        private void showScore(Action<ScoreInfo> setUp)
        {
            AddStep("load panel", () =>
            {
                var score = CreateScore();
                setUp(score);

                SetContents(s => s is LegacySkin ? CreateScreen(score, skins.DefaultClassicSkin, s) : Empty());
            });

            AddUntilStep("panels loaded", () => this.ChildrenOfType<LegacyRankingPanel>().Any() && this.ChildrenOfType<LegacyRankingPanel>().All(p => p.IsLoaded));
        }

        /// <summary>
        /// An osu! score on a beatmap with an ordinary title, mapper and player (the test resources' ones are much longer).
        /// </summary>
        public static ScoreInfo CreateScore()
        {
            var score = TestResources.CreateTestScoreInfo(new OsuRuleset().RulesetInfo);
            score.Statistics.Clear();
            score.MaximumStatistics.Clear();
            score.BeatmapInfo!.Metadata.Artist = "A-One";
            score.BeatmapInfo.Metadata.Title = "Side by Side";
            score.BeatmapInfo.Metadata.Author.Username = "bsm";
            score.BeatmapInfo.DifficultyName = "OSUDriver's Normal";
            score.User.Username = "Lilian";
            return score;
        }

        /// <summary>
        /// The legacy ranking screen in the given skin.
        /// </summary>
        public static Drawable CreateScreen(ScoreInfo score, ISkin fallback, ISkin skin, bool canRetry = true, LegacyRankingDetails? details = null) =>
            new SkinProvidingContainer(fallback)
            {
                Child = new SkinProvidingContainer(skin)
                {
                    Child = new LegacyRankingScreen(score, health, canRetry ? () => { } : null, () => { }, details),
                }
            };
    }
}

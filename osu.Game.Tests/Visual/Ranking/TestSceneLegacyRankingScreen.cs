// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Online;
using osu.Game.Rulesets.UI;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Skinning.Ranking;
using osu.Game.Users;

namespace osu.Game.Tests.Visual.Ranking
{
    /// <summary>
    /// The legacy ranking screen in the default legacy skin at full size, to compare with stable's (1024x768).
    /// </summary>
    public partial class TestSceneLegacyRankingScreen : OsuTestScene
    {
        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private bool exported;
        private readonly Bindable<bool> favourited = new Bindable<bool>();

        private LegacyRankingScreen screen => this.ChildrenOfType<LegacyRankingScreen>().Single();

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("load screen", () =>
            {
                var score = TestSceneLegacyRankingPanel.CreateScore();
                score.TotalScore = 94_082;
                score.MaxCombo = 134;
                score.MaximumStatistics[HitResult.Great] = 200;
                score.Accuracy = 0.9456;
                score.Rank = ScoreRank.S;
                score.Position = 1;
                score.Statistics[HitResult.Great] = 45;
                score.Statistics[HitResult.Ok] = 4;
                score.Statistics[HitResult.Meh] = 0;
                score.Statistics[HitResult.Miss] = 0;

                // As in the stable screenshots: before and after this score.
                var before = new UserStatistics { GlobalRank = 1, Accuracy = 94.24, RankedScore = 3_093_780, TotalScore = 12_260_308, PP = 1292 };
                var after = new UserStatistics
                {
                    GlobalRank = 1, Accuracy = 94.24, RankedScore = 3_187_862, TotalScore = 12_354_390, PP = 1292,
                    Level = new UserStatistics.LevelInfo { Current = 31, Progress = 55 },
                };

                exported = false;
                favourited.Value = false;

                var details = new LegacyRankingDetails
                {
                    LocalRank = 4,
                    StatisticsUpdate = new Bindable<ScoreBasedUserStatisticsUpdate?>(new ScoreBasedUserStatisticsUpdate(score, before, after)),
                    OnlineBeatmap = new Bindable<APIBeatmap?>(new APIBeatmap
                    {
                        PlayCount = 1,
                        PassCount = 1,
                        BeatmapSet = new APIBeatmapSet { Ranked = new DateTimeOffset(2025, 1, 12, 0, 0, 0, TimeSpan.Zero) },
                    }),
                    ExportReplay = () => exported = true,
                    ToggleFavourite = () => favourited.Value = !favourited.Value,
                    Favourited = favourited,
                };

                Child = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    // Stable's 4:3 window, as large as fits.
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    FillAspectRatio = 4 / 3f,
                    Masking = true,
                    Child = TestSceneLegacyRankingPanel.CreateScreen(score, skins.DefaultClassicSkin, skins.DefaultClassicSkin, details: details),
                };
            });

            AddUntilStep("screen loaded", () => this.ChildrenOfType<LegacyRankingPanel>().Any(p => p.IsLoaded));
        }

        [Test]
        public void TestFirstPage()
        {
            AddAssert("first page", () => !screen.ShowingSecondPage);
        }

        [Test]
        public void TestSecondPage()
        {
            AddStep("show second page", () => screen.ShowSecondPage());
            AddAssert("second page", () => screen.ShowingSecondPage);
            AddUntilStep("local section loaded", () => this.ChildrenOfType<LegacyRankingLocalSection>().Single().IsLoaded);
            AddUntilStep("online section loaded", () => this.ChildrenOfType<LegacyRankingOnlineSection>().Single().IsLoaded);
            AddUntilStep("beatmap section loaded", () => this.ChildrenOfType<LegacyRankingBeatmapSection>().Single().IsLoaded);
            AddStep("back to first page", () => screen.ShowFirstPage());
            AddAssert("first page", () => !screen.ShowingSecondPage);
        }

        [Test]
        public void TestOnlineRankingButtonOnlyOnFirstPage()
        {
            AddAssert("button shown", () => onlineRankingButton.Alpha == 1);
            AddStep("show second page", () => screen.ShowSecondPage());
            // On the second page it would show through the top bar.
            AddUntilStep("button hidden", () => onlineRankingButton.Alpha == 0);
            AddStep("back to first page", () => screen.ShowFirstPage());
            AddUntilStep("button shown", () => onlineRankingButton.Alpha == 1);
        }

        [Test]
        public void TestWideScreenWithoutStatisticsUpdate()
        {
            AddStep("load 16:9 screen without an update from the server", () =>
            {
                var score = TestSceneLegacyRankingPanel.CreateScore();

                var details = new LegacyRankingDetails
                {
                    CurrentStatistics = new Bindable<UserStatistics?>(new UserStatistics
                    {
                        GlobalRank = 1, Accuracy = 94.24, PP = 1292, RankedScore = 3_187_862, TotalScore = 12_354_390,
                        Level = new UserStatistics.LevelInfo { Current = 31, Progress = 55 },
                    }),
                };

                Child = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    FillAspectRatio = 16 / 9f,
                    Masking = true,
                    Child = TestSceneLegacyRankingPanel.CreateScreen(score, skins.DefaultClassicSkin, skins.DefaultClassicSkin, details: details),
                };
            });

            AddStep("show second page", () => screen.ShowSecondPage());
            AddUntilStep("online section loaded", () => this.ChildrenOfType<LegacyRankingOnlineSection>().Any(s => s.IsLoaded));

            // Stable centres the page on wide screens: the 1024px layout starts (1366 - 1024) / 2 = 171 in, so "Overall" (165 + 110 / 2) is at 391 of 1366.
            AddAssert("content centred", () =>
            {
                var header = this.ChildrenOfType<LegacyRankingOnlineSection>().Single().ChildrenOfType<Graphics.Sprites.OsuSpriteText>().First(t => t.Text == "Overall");
                var quad = screen.ScreenSpaceDrawQuad;
                return Math.Abs((header.ScreenSpaceDrawQuad.Centre.X - quad.TopLeft.X) / quad.Width - 391 / 1366f) < 0.005f;
            });

            // The local band stays with the right side (measured in stable at 1920x1080): "Local player name:" at 195 + 342 of 1366.
            AddAssert("local band with the right side", () =>
            {
                var label = this.ChildrenOfType<LegacyRankingLocalSection>().Single().ChildrenOfType<Graphics.Sprites.OsuSpriteText>().First(t => t.Text == "Local player name:");
                var quad = screen.ScreenSpaceDrawQuad;
                return Math.Abs((label.ScreenSpaceDrawQuad.TopLeft.X - quad.TopLeft.X) / quad.Width - 537 / 1366f) < 0.005f;
            });

            // Scores don't carry their player's statistics: the current ones are shown.
            AddUntilStep("card shows current statistics", () => this.ChildrenOfType<LegacyRankingLocalSection>().Single()
                                                                  .ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Any(t => t.Text == "Performance:1,292pp"));
            AddAssert("overall rank shown", () => this.ChildrenOfType<LegacyRankingOnlineSection>().Single()
                                                     .ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Any(t => t.Text == "#1"));
        }

        [Test]
        public void TestNoSecondPageForViewedScore()
        {
            AddStep("load screen for a score not just played", () =>
            {
                Child = TestSceneLegacyRankingPanel.CreateScreen(TestSceneLegacyRankingPanel.CreateScore(), skins.DefaultClassicSkin, skins.DefaultClassicSkin,
                    details: new LegacyRankingDetails { HasSecondPage = false });
            });

            // Stable shows the rankings page (and its button) only after playing.
            AddUntilStep("screen loaded", () => this.ChildrenOfType<LegacyRankingPanel>().Any(p => p.IsLoaded));
            AddAssert("no online ranking button", () => !screen.ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Any(t => t.Text.ToString().Contains("Online Ranking")));
            AddStep("try the second page", () => screen.ShowSecondPage());
            AddAssert("still the first page", () => !screen.ShowingSecondPage);
        }

        [Test]
        public void TestBeatmapRanking()
        {
            AddStep("load screen: #2 on the map, a previous best, 150pp", () =>
            {
                var score = TestSceneLegacyRankingPanel.CreateScore();
                score.Position = 2;
                score.Accuracy = 0.9683;
                score.MaxCombo = 177;

                Child = TestSceneLegacyRankingPanel.CreateScreen(score, skins.DefaultClassicSkin, skins.DefaultClassicSkin, details: new LegacyRankingDetails
                {
                    PreviousBest = new Bindable<SoloScoreInfo?>(new SoloScoreInfo { Accuracy = 0.9048, MaxCombo = 99, TotalScore = 100, PP = 120 }),
                    Performance = new Bindable<double?>(150),
                });
            });

            AddStep("show second page", () => screen.ShowSecondPage());
            AddUntilStep("online section loaded", () => this.ChildrenOfType<LegacyRankingOnlineSection>().Any(s => s.IsLoaded));

            // Like modern stable's: this play against the best before it, with the changes.
            AddAssert("the place on the map", () => onlineTexts().Contains("#2"));
            AddAssert("accuracy and its change", () => onlineTexts().Contains(format(96.83, "0.00") + "%") && onlineTexts().Contains("(+" + format(6.35, "0.00") + "%)"));
            AddAssert("combo and its change", () => onlineTexts().Contains("177x") && onlineTexts().Contains("(+78x)"));
            AddAssert("pp and its change", () => onlineTexts().Contains("150pp") && onlineTexts().Contains("(+30pp)"));
            AddAssert("headers", () => new[] { "Overall", "Accuracy", "Max Combo", "Ranked Score", "Total Score", "Performance" }.All(onlineTexts().Contains));

            static string format(double value, string f) => value.ToString(f, System.Globalization.CultureInfo.CurrentCulture);
        }

        private string[] onlineTexts() => this.ChildrenOfType<LegacyRankingOnlineSection>().Single()
                                              .ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Select(t => t.Text.ToString()).ToArray();

        [Test]
        public void TestRating()
        {
            int? sentRating = null;

            AddStep("answer rating requests", () => ((osu.Game.Online.API.DummyAPIAccess)API).HandleRequest = request =>
            {
                switch (request)
                {
                    case osu.Game.Online.API.Requests.GetBeatmapsetRatingRequest get:
                        get.TriggerSuccess(new osu.Game.Online.API.Requests.GetBeatmapsetRatingRequest.Response { TotalRating = 7.5 });
                        return true;

                    case osu.Game.Online.API.Requests.PostBeatmapsetRatingRequest post:
                        sentRating = 8;
                        post.TriggerSuccess(new osu.Game.Online.API.Requests.PostBeatmapsetRatingRequest.Response { UpdatedRating = 7.75 });
                        return true;
                }

                return false;
            });

            AddStep("load screen", () =>
            {
                var score = TestSceneLegacyRankingPanel.CreateScore();
                score.BeatmapInfo!.BeatmapSet!.OnlineID = 1234;
                Child = TestSceneLegacyRankingPanel.CreateScreen(score, skins.DefaultClassicSkin, skins.DefaultClassicSkin, details: new LegacyRankingDetails());
            });

            AddStep("show second page", () => screen.ShowSecondPage());
            AddUntilStep("asks for a rating", () => ratingTexts().Any(t => t.StartsWith("Please take a second", StringComparison.Ordinal)));
            AddAssert("ten stars", () => this.ChildrenOfType<LegacyRankingRating>().Single().ChildrenOfType<Drawable>().Count(d => d.Size == new osuTK.Vector2(43)) >= 10);

            // Like stable's: clicking the 8th star votes 8, then the set's rating shows.
            AddStep("click 8th star", () => this.ChildrenOfType<LegacyRankingRating>().Single().ChildrenOfType<CompositeDrawable>()
                                                .Where(d => d.Size == new osuTK.Vector2(43) && d.GetType().Name == "Star").ElementAt(7).TriggerClick());
            AddUntilStep("voted 8", () => sentRating == 8);
            AddUntilStep("thanks with the new rating", () => ratingTexts().Any(t => t.Contains("7.75")));

            AddStep("stop answering", () => ((osu.Game.Online.API.DummyAPIAccess)API).HandleRequest = null);
        }

        private string[] ratingTexts() => this.ChildrenOfType<LegacyRankingRating>().SingleOrDefault()?
                                              .ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Select(t => t.Text.ToString()).ToArray() ?? Array.Empty<string>();

        [Test]
        public void TestOldSkinLayout()
        {
            AddStep("load screen with an old skin", () =>
                Child = TestSceneLegacyRankingPanel.CreateScreen(TestSceneLegacyRankingPanel.CreateScore(), skins.DefaultClassicSkin, new OldSkin(skins.DefaultClassicSkin)));

            AddUntilStep("loaded", () => this.ChildrenOfType<LegacyRankingGrade>().Any(g => g.IsLoaded));

            // Stable's for skins of Version 1.0 (its 640x480): the grade at 170, the graph at 360, the perfect stamp at 200 across.
            AddAssert("grade higher", () => this.ChildrenOfType<LegacyRankingGrade>().Single().Y == 170 * 1.6f);
            AddAssert("graph higher", () => this.ChildrenOfType<LegacyRankingGraph>().Single().Y == 360 * 1.6f);
            AddAssert("perfect further left", () => this.ChildrenOfType<LegacyRankingPerfect>().Single().X == 200 * 1.6f);
        }

        /// <summary>
        /// A skin saying it's skin.ini Version 1.0, otherwise the given one.
        /// </summary>
        private class OldSkin : ISkin
        {
            private readonly ISkin skin;

            public OldSkin(ISkin skin)
            {
                this.skin = skin;
            }

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => skin.GetDrawableComponent(lookup);

            public osu.Framework.Graphics.Textures.Texture? GetTexture(string componentName, osu.Framework.Graphics.Textures.WrapMode wrapModeS,
                                                                    osu.Framework.Graphics.Textures.WrapMode wrapModeT) => skin.GetTexture(componentName, wrapModeS, wrapModeT);

            public osu.Framework.Audio.Sample.ISample? GetSample(osu.Game.Audio.ISampleInfo sampleInfo) => skin.GetSample(sampleInfo);

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
            {
                if (lookup is SkinConfiguration.LegacySetting setting && setting == SkinConfiguration.LegacySetting.Version)
                    return (IBindable<TValue>)(object)new Bindable<decimal>(1.0m);

                return skin.GetConfig<TLookup, TValue>(lookup);
            }
        }

        [Test]
        public void TestModIcons()
        {
            AddStep("load screen with NC and HD", () =>
            {
                var score = TestSceneLegacyRankingPanel.CreateScore();
                score.Mods = new osu.Game.Rulesets.Mods.Mod[] { new osu.Game.Rulesets.Osu.Mods.OsuModNightcore(), new osu.Game.Rulesets.Osu.Mods.OsuModHidden() };

                Child = TestSceneLegacyRankingPanel.CreateScreen(score, skins.DefaultClassicSkin, skins.DefaultClassicSkin);
            });

            // Like stable: Nightcore without the Double Time it includes.
            // (Lazer's default skin has no "selection-mod-*": lazer's icons.)
            AddUntilStep("two icons", () => this.ChildrenOfType<LegacyRankingMods>().SingleOrDefault()?.ChildrenOfType<ModIcon>().Count() == 2);
            // Stable's: right of the middle (its 640x480 field stays centred), where skins draw their mods box.
            AddAssert("at the right", () => this.ChildrenOfType<LegacyRankingMods>().Single().ChildrenOfType<ModIcon>()
                                                .All(i => i.ScreenSpaceDrawQuad.Centre.X > this.ChildrenOfType<LegacyRankingMods>().Single().ScreenSpaceDrawQuad.Centre.X));
            AddAssert("nightcore, not double time", () => this.ChildrenOfType<ModIcon>().Any(i => i.Mod is osu.Game.Rulesets.Osu.Mods.OsuModNightcore)
                                                          && this.ChildrenOfType<ModIcon>().All(i => i.Mod.Acronym != "DT"));
            AddUntilStep("icons shown", () => this.ChildrenOfType<ModIcon>().All(s => s.Alpha == 1));
        }

        private Drawable onlineRankingButton => screen.ChildrenOfType<ClickableContainer>().Single(c => c.ChildrenOfType<Graphics.Sprites.OsuSpriteText>()
                                                                                                         .Any(t => t.Text.ToString().Contains("Online Ranking")));

        [Test]
        public void TestOptions()
        {
            AddStep("show second page", () => screen.ShowSecondPage());
            AddStep("export replay", () => clickOption("Export replay as .osr"));
            AddAssert("exported", () => exported);
            AddStep("favourite", () => clickOption("Online Favourite"));
            AddAssert("favourited", () => favourited.Value);
        }

        private void clickOption(string text) =>
            this.ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Single(t => t.Text == text).Parent!.TriggerClick();
    }
}

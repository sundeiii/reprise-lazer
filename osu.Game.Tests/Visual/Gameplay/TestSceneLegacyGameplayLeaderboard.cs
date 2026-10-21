// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Gameplay
{
    /// <summary>
    /// osu!stable's in-game leaderboard: 8 scores, the player's own last, so the 6 ending with it are shown.
    /// </summary>
    public partial class TestSceneLegacyGameplayLeaderboard : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        private TestProvider provider = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("classic skin, stable layouts", () =>
            {
                skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo;
                config.SetValue(OsuSetting.StableLayouts, true);
            });
            AddStep("create", () =>
            {
                provider = new TestProvider();

                for (int i = 0; i < 8; i++)
                {
                    var score = new GameplayLeaderboardScore(new ScoreInfo
                    {
                        User = new APIUser { Username = $"player{i}" },
                        TotalScore = 1_000_000 - i * 100_000,
                        MaxCombo = 500 - i,
                    }, tracked: i == 7, GameplayLeaderboardScore.ComboDisplayMode.Highest);
                    score.DisplayOrder.Value = i;
                    provider.Scores.Add(score);
                }

                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(IGameplayLeaderboardProvider), provider) },
                    Child = new LegacyGameplayLeaderboard(),
                };
            });
        }

        [TearDownSteps]
        public void TearDownSteps() => AddStep("reset", () =>
        {
            config.SetValue(OsuSetting.StableLayouts, false);
            skins.CurrentSkinInfo.SetDefault();
        });

        [Test]
        public void TestSixAroundTheOwnScore()
        {
            AddUntilStep("own score shown", () => entryAlpha("player7") == 1);
            AddAssert("the first two hidden", () => entryAlpha("player0") == 0 && entryAlpha("player1") == 0);
            AddAssert("the others fainter", () => entryAlpha("player2") > 0 && entryAlpha("player2") < 1);
            AddUntilStep("an avatar each shown", () => this.ChildrenOfType<osu.Game.Users.Drawables.DrawableAvatar>().Count() >= 6);
            AddAssert("its place written", () => this.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "8"));
            AddAssert("score and combo", () => this.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "1,000,000 (500x)"));

            AddStep("own score first", () => provider.Scores.Last().DisplayOrder.Value = -1);
            AddUntilStep("the first ones shown again", () => entryAlpha("player0") > 0);
            // Counted, not looked for: they're gone within 0.7s, which a slow CI runner's next step can come after.
            AddUntilStep("passing explodes", () => this.ChildrenOfType<LegacyGameplayLeaderboard>().Single().ExplosionsShown, () => Is.EqualTo(2));
            AddUntilStep("own entry light blue when first", () => this.ChildrenOfType<OsuSpriteText>().Single(t => t.Text.ToString() == "player7").Parent!
                                                                   .ChildrenOfType<osu.Framework.Graphics.Sprites.Sprite>().First().Colour.TopLeft.Linear.B > 0.5f);
        }

        [Test]
        public void TestOnlyWithStableLayouts()
        {
            AddStep("stable layouts off", () => config.SetValue(OsuSetting.StableLayouts, false));
            AddUntilStep("hidden", () => this.ChildrenOfType<LegacyGameplayLeaderboard>().Single().Alpha == 0);
        }

        private float entryAlpha(string name) => this.ChildrenOfType<OsuSpriteText>().Single(t => t.Text.ToString() == name).Parent!.Alpha;

        private class TestProvider : IGameplayLeaderboardProvider
        {
            public BindableList<GameplayLeaderboardScore> Scores { get; } = new BindableList<GameplayLeaderboardScore>();

            IBindableList<GameplayLeaderboardScore> IGameplayLeaderboardProvider.Scores => Scores;
        }
    }
}

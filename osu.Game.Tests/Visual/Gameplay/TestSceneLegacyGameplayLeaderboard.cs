// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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
    /// osu!stable's in-game leaderboard: 8 scores, the player's own last, so the #1 and the 5 ending with it are shown.
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
                config.SetValue(OsuSetting.StableGameplayLeaderboard, true);
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
                    Child = new LegacyGameplayLeaderboard { IsBreakTime = breakTime },
                };
            });
        }

        private readonly BindableBool breakTime = new BindableBool(true);

        [TearDownSteps]
        public void TearDownSteps() => AddStep("reset", () =>
        {
            config.SetValue(OsuSetting.StableGameplayLeaderboard, false);
            config.SetValue(OsuSetting.GameplayLeaderboard, true);
            breakTime.Value = true;
            skins.CurrentSkinInfo.SetDefault();
        });

        [Test]
        public void TestSixAroundTheOwnScore()
        {
            AddUntilStep("own score shown", () => entryAlpha("player7") == 1);
            AddAssert("the #1 kept at the top", () => entryAlpha("player0") > 0);
            AddAssert("the two after it hidden", () => entryAlpha("player1") == 0 && entryAlpha("player2") == 0);
            AddAssert("the others fainter", () => entryAlpha("player3") > 0 && entryAlpha("player3") < 1);
            AddUntilStep("an avatar each shown", () => this.ChildrenOfType<osu.Game.Users.Drawables.DrawableAvatar>().Count() >= 6);
            AddAssert("the #1's place written", () => this.ChildrenOfType<LegacySpriteText>().Any(t => t.Text.ToString() == "1"));
            AddAssert("score and combo", () => this.ChildrenOfType<LegacySpriteText>().Any(t => t.Text.ToString() == "1,000,000")
                                               && this.ChildrenOfType<LegacySpriteText>().Any(t => t.Text.ToString() == "500x"));

            AddStep("own score first", () => provider.Scores.Last().DisplayOrder.Value = -1);
            AddUntilStep("the first ones shown again", () => entryAlpha("player0") > 0);
            // Counted, not looked for: they're gone within 0.7s, which a slow CI runner's next step can come after.
            AddUntilStep("passing explodes", () => this.ChildrenOfType<LegacyGameplayLeaderboard>().Single().ExplosionsShown, () => Is.EqualTo(2));
            AddUntilStep("own entry white, as stable's", () => this.ChildrenOfType<OsuSpriteText>().Single(t => t.Text.ToString() == "player7").Parent!
                                                                  .ChildrenOfType<osu.Framework.Graphics.Sprites.Sprite>().First().Colour.TopLeft.Linear.R > 0.9f);
        }

        [Test]
        public void TestInBreaksUnlessToggledOn()
        {
            AddStep("toggled off, playing", () =>
            {
                config.SetValue(OsuSetting.GameplayLeaderboard, false);
                breakTime.Value = false;
            });
            AddUntilStep("faded out", () => contentAlpha, () => Is.EqualTo(0));
            AddStep("a break", () => breakTime.Value = true);
            AddUntilStep("shown in it", () => contentAlpha, () => Is.EqualTo(1));
            AddStep("playing, toggled on", () =>
            {
                breakTime.Value = false;
                config.SetValue(OsuSetting.GameplayLeaderboard, true);
            });
            AddWaitStep("a while", 5);
            AddAssert("still shown", () => contentAlpha, () => Is.EqualTo(1));
        }

        private float contentAlpha => this.ChildrenOfType<LegacyGameplayLeaderboard>().Single().ChildrenOfType<Container>()
                                          .First(c => c.Children.Count == 3).Alpha;

        [Test]
        public void TestOnlyWithStableLayouts()
        {
            AddStep("stable layouts off", () => config.SetValue(OsuSetting.StableGameplayLeaderboard, false));
            AddUntilStep("hidden", () => this.ChildrenOfType<LegacyGameplayLeaderboard>().Single().Alpha == 0);
            AddStep("stable layouts back on", () => config.SetValue(OsuSetting.StableGameplayLeaderboard, true));
            AddUntilStep("shown again", () => this.ChildrenOfType<LegacyGameplayLeaderboard>().Single().Alpha == 1);
        }

        private float entryAlpha(string name) => this.ChildrenOfType<OsuSpriteText>().Single(t => t.Text.ToString() == name).Parent!.Alpha;

        private class TestProvider : IGameplayLeaderboardProvider
        {
            public BindableList<GameplayLeaderboardScore> Scores { get; } = new BindableList<GameplayLeaderboardScore>();

            IBindableList<GameplayLeaderboardScore> IGameplayLeaderboardProvider.Scores => Scores;
        }
    }
}

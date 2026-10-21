// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Select.Filter;
using osu.Game.Screens.Select;
using osu.Game.Skinning.Select;
using osu.Game.Skinning;
using osu.Game.Tests.Resources;
using osu.Game.Users;
using osuTK.Input;

namespace osu.Game.Tests.Visual.SongSelect
{
    /// <summary>
    /// Song select with "Use osu!stable layouts": osu!stable's footer instead of this screen's, with a legacy skin.
    /// </summary>
    public partial class TestSceneSongSelectStableLayouts : SongSelectTestScene
    {
        [Resolved]
        private SkinManager skins { get; set; } = null!;

        // The game provides this; the stable footer's player card uses it.
        [Cached(typeof(LocalUserStatisticsProvider))]
        private readonly TestSceneLegacyFooter.TestUserStatisticsProvider userStatisticsProvider = new TestSceneLegacyFooter.TestUserStatisticsProvider();

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Add(userStatisticsProvider);
        }

        [Test]
        public void TestStableFooter()
        {
            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));

            LoadSongSelect();
            ImportBeatmapForRuleset(0);

            AddUntilStep("stable footer shown", () => SongSelect.ChildrenOfType<LegacyFooter>().Any(f => f.IsLoaded));
            AddAssert("screen footer off", () => SongSelect.FooterContent.Value.Mode != ScreenFooterContent.FooterMode.Argon);
            AddUntilStep("screen footer hidden", () => ScreenFooter.State.Value == Visibility.Hidden);

            AddStep("stop using stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, false));
            AddUntilStep("stable footer gone", () => !SongSelect.ChildrenOfType<LegacyFooter>().Any());
            AddAssert("screen footer on", () => SongSelect.FooterContent.Value.Mode == ScreenFooterContent.FooterMode.Argon);
            AddUntilStep("screen footer shown", () => ScreenFooter.State.Value == Visibility.Visible);

            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));
            AddUntilStep("stable footer shown", () => SongSelect.ChildrenOfType<LegacyFooter>().Any(f => f.IsLoaded));

            AddStep("set lazer skin", () => skins.CurrentSkinInfo.Value = skins.CurrentSkinInfo.Default);
            AddUntilStep("stable footer gone with a lazer skin", () => !SongSelect.ChildrenOfType<LegacyFooter>().Any());

            AddStep("reset", () =>
            {
                Config.SetValue(OsuSetting.StableLayouts, false);
                skins.CurrentSkinInfo.SetDefault();
            });
        }

        [Test]
        public void TestStatisticsWithoutUser()
        {
            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));

            LoadSongSelect();
            ImportBeatmapForRuleset(0);

            AddUntilStep("stable footer shown", () => SongSelect.ChildrenOfType<LegacyFooter>().Any(f => f.IsLoaded));

            // Not every server sends the user with their statistics (reprise.pw doesn't).
            AddStep("statistics without user", () => userStatisticsProvider.UpdateStatistics(new UserStatistics
            {
                PP = 1292,
                Accuracy = 94.24,
                GlobalRank = 1,
                Level = new UserStatistics.LevelInfo { Current = 31, Progress = 55 },
            }, Ruleset.Value));

            AddUntilStep("player card shows the logged in user", () => SongSelect.ChildrenOfType<LegacyFooterUser>().Single()
                                                                                 .ChildrenOfType<Graphics.Sprites.OsuSpriteText>()
                                                                                 .Any(t => t.Text == API.LocalUser.Value.Username));

            // The bar (the footer's first part) goes down to the bottom of the screen, though the player card makes the footer taller.
            AddAssert("bar at the bottom", () =>
            {
                var bar = SongSelect.ChildrenOfType<LegacyFooter>().Single().ChildrenOfType<Drawable>().First();
                return System.Math.Abs(bar.ScreenSpaceDrawQuad.BottomLeft.Y - SongSelect.ScreenSpaceDrawQuad.BottomLeft.Y) < 1;
            });

            AddStep("reset", () =>
            {
                Config.SetValue(OsuSetting.StableLayouts, false);
                skins.CurrentSkinInfo.SetDefault();
            });
        }

        [Test]
        public void TestStableTop()
        {
            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));
            AddStep("no grouping", () => Config.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.None));

            LoadSongSelect();
            ImportBeatmapForRuleset(0);

            AddUntilStep("stable top shown", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().Any(t => t.IsLoaded));
            AddAssert("title wedge hidden", () => !SongSelect.ChildrenOfType<BeatmapTitleWedge>().Single().Parent!.IsPresent);

            // The tabs are 116px wide: "Recently Played", "By Artist", "By Difficulty", "No Grouping".
            AddStep("click \"By Artist\" tab", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().Single()
                                                           .ChildrenOfType<ClickableContainer>().Where(c => c.Width == 116).ElementAt(1).TriggerClick());
            AddAssert("grouped by artist", () => Config.Get<GroupMode>(OsuSetting.SongSelectGroupMode) == GroupMode.Artist);

            // Like stable's, the choice boxes are above the footer (skins draw the top's art with the footer's images).
            AddAssert("choice boxes above footer", () =>
            {
                Drawable dropdownsLayer = SongSelect.ChildrenOfType<LegacySongSelectTop>().Single().Dropdowns.Parent!;
                var layers = (Container)dropdownsLayer.Parent!;

                // The layer the footer is in, among the same layers.
                Drawable footerLayer = SongSelect.ChildrenOfType<LegacyFooter>().Single();
                while (footerLayer.Parent != layers)
                    footerLayer = footerLayer.Parent!;

                return layers.IndexOf(dropdownsLayer) > layers.IndexOf(footerLayer);
            });

            AddStep("open group choices", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().Single().Dropdowns
                                                         .ChildrenOfType<ClickableContainer>().First().TriggerClick());
            AddStep("choose \"By Difficulty\"", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().Single().Dropdowns
                                                               .ChildrenOfType<ClickableContainer>()
                                                               .First(c => c.ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Any(t => t.Text == "By Difficulty")
                                                                           && c.Width != 192).TriggerClick());
            AddAssert("grouped by difficulty", () => Config.Get<GroupMode>(OsuSetting.SongSelectGroupMode) == GroupMode.Difficulty);

            AddStep("search", () => SongSelect.ChildrenOfType<FilterControl>().Single().Search("side by side"));
            // What's typed is above the skin's art, with the choice boxes.
            AddUntilStep("search shown", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().Single().Dropdowns
                                                         .ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Any(t => t.Text == "side by side" && t.Alpha == 1));

            AddStep("stop using stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, false));
            AddUntilStep("stable top gone", () => !SongSelect.ChildrenOfType<LegacySongSelectTop>().Any());
            AddAssert("title wedge shown", () => SongSelect.ChildrenOfType<BeatmapTitleWedge>().Single().Parent!.IsPresent);

            AddStep("reset", () =>
            {
                Config.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.None);
                skins.CurrentSkinInfo.SetDefault();
            });
        }

        [Test]
        public void TestStableLeaderboard()
        {
            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));
            AddStep("local ranking", () => Config.SetValue(OsuSetting.BeatmapDetailTab, BeatmapDetailTab.Local));

            LoadSongSelect();
            ImportBeatmapForRuleset(0);
            AddUntilStep("beatmap selected", () => !Beatmap.IsDefault);

            AddStep("import two scores", () =>
            {
                var beatmapInfo = Beatmap.Value.BeatmapInfo;

                foreach (var (total, combo, rank) in new[] { (1_000_000L, 500, ScoreRank.S), (400_000L, 120, ScoreRank.B) })
                {
                    ScoreManager.Import(new ScoreInfo
                    {
                        Hash = System.Guid.NewGuid().ToString(),
                        BeatmapHash = beatmapInfo.Hash,
                        BeatmapInfo = beatmapInfo,
                        Ruleset = new OsuRuleset().RulesetInfo,
                        User = new APIUser { Username = "player", Id = 1001 },
                        TotalScore = total,
                        MaxCombo = combo,
                        Accuracy = 0.95,
                        Statistics = { [HitResult.Great] = 300, [HitResult.Ok] = 2, [HitResult.Meh] = 1, [HitResult.Miss] = 0 },
                        Mods = total > 500_000 ? new Mod[] { new OsuModHidden() } : System.Array.Empty<Mod>(),
                        Rank = rank,
                        Date = System.DateTimeOffset.Now.AddHours(-3),
                    });
                }
            });

            AddUntilStep("stable leaderboard shown", () => SongSelect.ChildrenOfType<LegacySongSelectLeaderboard>().Any(l => l.IsLoaded));
            AddAssert("song select's leaderboard hidden", () => !SongSelect.ChildrenOfType<BeatmapDetailsArea>().Single().Parent!.IsPresent);
            AddStep("refetch", () => SongSelect.ChildrenOfType<LegacySongSelectLeaderboard>().Single().Refetch());

            AddUntilStep("two rows", () => SongSelect.ChildrenOfType<LegacySongSelectLeaderboard.LegacyLeaderboardRow>().Count() == 2);
            AddAssert("best first, with score and combo", () => rowTexts(0).Contains("Score: 1,000,000 (500x)"));
            AddAssert("lead over the next", () => rowTexts(0).Contains("+600,000"));
            AddAssert("last has no lead", () => rowTexts(1).Contains("-"));
            AddAssert("recent: hours ago", () => rowTexts(0).Contains("3h"));
            AddAssert("stable's tooltip", () =>
            {
                var rows = SongSelect.ChildrenOfType<LegacySongSelectLeaderboard.LegacyLeaderboardRow>().OrderBy(r => r.Y).ToArray();
                string best = rows[0].TooltipText.ToString(), other = rows[1].TooltipText.ToString();
                return best.StartsWith("Achieved on ", System.StringComparison.Ordinal)
                       && best.EndsWith("\n300:300 100:2 50:1 Miss:0\nAccuracy:95.00%\nMods:Hidden", System.StringComparison.Ordinal)
                       && other.EndsWith("\nMods:None", System.StringComparison.Ordinal);
            });

            // The choice is song select's own (lazer's leaderboard shows the same when the setting is off).
            AddStep("choose global ranking", () => SongSelect.ChildrenOfType<LegacySongSelectLeaderboard>().Single().RankingType.Value = LegacyRankingType.Global);
            AddAssert("song select's choice is global", () => Config.Get<BeatmapDetailTab>(OsuSetting.BeatmapDetailTab) == BeatmapDetailTab.Global);
            AddUntilStep("local rows gone", () => !SongSelect.ChildrenOfType<LegacySongSelectLeaderboard.LegacyLeaderboardRow>().Any());

            AddStep("choose selected mods ranking", () => SongSelect.ChildrenOfType<LegacySongSelectLeaderboard>().Single().RankingType.Value = LegacyRankingType.SelectedMods);
            AddAssert("song select filters by mods", () => Config.Get<bool>(OsuSetting.BeatmapDetailModsFilter));

            AddStep("stop using stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, false));
            AddUntilStep("stable leaderboard gone", () => !SongSelect.ChildrenOfType<LegacySongSelectLeaderboard>().Any());
            AddAssert("song select's leaderboard back", () => SongSelect.ChildrenOfType<BeatmapDetailsArea>().Single().Parent!.IsPresent);

            AddStep("reset", () =>
            {
                Config.SetValue(OsuSetting.BeatmapDetailModsFilter, false);
                Config.SetValue(OsuSetting.BeatmapDetailTab, BeatmapDetailTab.Details);
                skins.CurrentSkinInfo.SetDefault();
            });
        }

        private string[] rowTexts(int index) => SongSelect.ChildrenOfType<LegacySongSelectLeaderboard.LegacyLeaderboardRow>()
                                                          .OrderBy(r => r.Y).ElementAt(index)
                                                          .ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Select(t => t.Text.ToString()).ToArray();

        [Test]
        public void TestLayoutVersions()
        {
            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));
            AddStep("2015", () => Config.SetValue(OsuSetting.StableLayoutVersion, StableLayoutVersion.Stable2015));

            LoadSongSelect();
            ImportBeatmapForRuleset(0);

            // 2015's: its own top and bottom (whatever the skin), its boxes.
            AddUntilStep("2015's bottom", () => stableBottom() != null);

            AddStep("later stable", () => Config.SetValue(OsuSetting.StableLayoutVersion, StableLayoutVersion.Later));
            AddUntilStep("not 2015's bottom", () => SongSelect.ChildrenOfType<LegacyFooter>().SingleOrDefault() is { IsLoaded: true } && stableBottom() == null);

            // Automatic: 2015's for a skin without its own song select top or bottom, else the later one (the default legacy skin has them
            // in Reprise's resources, not in osu!'s).
            AddStep("automatic", () => Config.SetValue(OsuSetting.StableLayoutVersion, StableLayoutVersion.Automatic));
            AddUntilStep("2015's bottom by the skin", () =>
            {
                var skin = skins.CurrentSkin.Value;
                bool skinHasOwn = skin.GetTexture(@"songselect-top") != null || skin.GetTexture(@"songselect-bottom") != null;
                return SongSelect.ChildrenOfType<LegacyFooter>().SingleOrDefault() is { IsLoaded: true } && (stableBottom() != null) != skinHasOwn;
            });

            AddStep("reset", () =>
            {
                Config.SetValue(OsuSetting.StableLayouts, false);
                Config.SetValue(OsuSetting.StableLayoutVersion, StableLayoutVersion.Automatic);
                skins.CurrentSkinInfo.SetDefault();
            });
        }

        // Stable 2015's own bottom (101 high, stretched across).
        private Sprite? stableBottom() => SongSelect.ChildrenOfType<LegacyFooter>().SingleOrDefault(f => f.IsLoaded)?.ChildrenOfType<Sprite>()
                                                    .FirstOrDefault(s => s.Name == LegacyFooter.STABLE_2015_BOTTOM && s.Texture != null);

        [Test]
        public void TestRebuiltForAnotherLegacySkin()
        {
            LegacyFooter? footer = null;
            LegacySongSelectTop? top = null;

            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));

            LoadSongSelect();
            ImportBeatmapForRuleset(0);

            Drawable? bar = null;
            LegacyCarouselPanel? panel = null;

            AddUntilStep("stable layout shown", () =>
            {
                panel = SongSelect.ChildrenOfType<LegacyCarouselPanel>().FirstOrDefault(p => p.IsLoaded && p.Item != null);
                bar = panel?.Bar;
                footer = SongSelect.ChildrenOfType<LegacyFooter>().SingleOrDefault(f => f.IsLoaded);
                top = SongSelect.ChildrenOfType<LegacySongSelectTop>().SingleOrDefault(t => t.IsLoaded);
                return footer != null && top != null && panel != null;
            });

            // The images are taken when it's made: another legacy skin needs a new one.
            AddStep("set another legacy skin", () =>
            {
                const string filename = @"classic-layout-version-0.osk";
                var imported = skins.Import(new ImportTask(TestResources.OpenResource($@"Archives/{filename}"), filename)).GetResultSafely();
                skins.CurrentSkinInfo.Value = imported!;
            });

            AddUntilStep("footer rebuilt", () => SongSelect.ChildrenOfType<LegacyFooter>().SingleOrDefault(f => f.IsLoaded) is LegacyFooter f && f != footer);
            AddUntilStep("top rebuilt", () => SongSelect.ChildrenOfType<LegacySongSelectTop>().SingleOrDefault(t => t.IsLoaded) is LegacySongSelectTop t && t != top);
            AddAssert("screen footer still off", () => SongSelect.FooterContent.Value.Mode != ScreenFooterContent.FooterMode.Argon);
            // The beatmap list's panels (reused, not remade) take the new skin's images too.
            AddUntilStep("list panel's bar remade", () => panel!.Bar != bar);

            AddStep("reset", () =>
            {
                Config.SetValue(OsuSetting.StableLayouts, false);
                skins.CurrentSkinInfo.SetDefault();
            });
        }

        [Test]
        public void TestStableTopFollowsMods()
        {
            string? withoutMods = null;

            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));

            LoadSongSelect();
            ImportBeatmapForRuleset(0);

            AddUntilStep("difficulty line shown", () => (withoutMods = difficultyLine()) != null);

            // Like stable's: HR and DT change CS/AR/OD (and the stars).
            AddStep("select HR and DT", () => SelectedMods.Value = new osu.Game.Rulesets.Mods.Mod[]
            {
                new osu.Game.Rulesets.Osu.Mods.OsuModHardRock(), new osu.Game.Rulesets.Osu.Mods.OsuModDoubleTime(),
            });
            AddUntilStep("difficulty line changed", () => difficultyLine() != withoutMods);

            AddStep("no mods", () => SelectedMods.Value = System.Array.Empty<osu.Game.Rulesets.Mods.Mod>());
            AddUntilStep("back as it was", () => difficultyLine() == withoutMods);

            // DT on its own: AR and OD go up (shorter windows), the length down.
            AddStep("select DT only", () => SelectedMods.Value = new osu.Game.Rulesets.Mods.Mod[] { new osu.Game.Rulesets.Osu.Mods.OsuModDoubleTime() });
            AddUntilStep("AR and OD changed with DT", () => difficultyLine() is string line && part(line, "AR:") != part(withoutMods!, "AR:")
                                                            && part(line, "OD:") != part(withoutMods!, "OD:"));
            AddStep("no mods", () => SelectedMods.Value = System.Array.Empty<osu.Game.Rulesets.Mods.Mod>());

            AddStep("reset", () => skins.CurrentSkinInfo.SetDefault());
        }

        private static string part(string line, string name) => line.Split(' ').First(p => p.StartsWith(name, System.StringComparison.Ordinal));

        private string? difficultyLine() => SongSelect.ChildrenOfType<LegacySongSelectTop>().SingleOrDefault()?
                                                      .ChildrenOfType<Graphics.Sprites.OsuSpriteText>()
                                                      .FirstOrDefault(t => t.Text.ToString().StartsWith("CS:", System.StringComparison.Ordinal))?.Text.ToString();

        [Test]
        public void TestStableTopFollowsSpeedModsOnRealBeatmap()
        {
            string? withoutMods = null;

            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));

            LoadSongSelect();

            // A real beatmap: its star rating gets calculated (and recalculated for the mods).
            AddStep("import real beatmap", () => Beatmaps.Import(new ImportTask(TestResources.GetQuickTestBeatmapForImport())).WaitSafely());
            AddUntilStep("stars shown", () => difficultyLine() is string line && part(line, "Stars:") != "Stars:0.00" && (withoutMods = line) != null);

            AddStep("select DT", () => SelectedMods.Value = new osu.Game.Rulesets.Mods.Mod[] { new osu.Game.Rulesets.Osu.Mods.OsuModDoubleTime() });

            // Once the stars with DT are in too, AR and OD still are DT's.
            AddUntilStep("stars with DT", () => difficultyLine() is string line && part(line, "Stars:") != part(withoutMods!, "Stars:"));
            AddWaitStep("let ratings settle", 10);
            AddAssert("AR and OD with DT", () => difficultyLine() is string line && part(line, "AR:") != part(withoutMods!, "AR:")
                                                 && part(line, "OD:") != part(withoutMods!, "OD:"));

            // Dragging DT's speed changes a setting, not which mods: the line follows it too.
            string? atDefaultSpeed = null;
            AddStep("remember line", () => atDefaultSpeed = difficultyLine());
            AddStep("DT at 2x", () => ((osu.Game.Rulesets.Osu.Mods.OsuModDoubleTime)SelectedMods.Value.Single()).SpeedChange.Value = 2);
            AddUntilStep("AR and OD follow the speed", () => difficultyLine() is string line && part(line, "AR:") != part(atDefaultSpeed!, "AR:")
                                                                 && part(line, "OD:") != part(atDefaultSpeed!, "OD:"));

            // While the speed changes, the stars don't drop back to the rating without mods in between.
            string? noModStars = null;
            AddStep("remember rating without mods", () => noModStars = part(withoutMods!, "Stars:"));
            AddStep("DT at 1.8x", () => ((osu.Game.Rulesets.Osu.Mods.OsuModDoubleTime)SelectedMods.Value.Single()).SpeedChange.Value = 1.8);
            AddAssert("not back to the rating without mods", () => part(difficultyLine()!, "Stars:") != noModStars);
            AddUntilStep("rating for 1.8x", () => part(difficultyLine()!, "Stars:") != noModStars);
            AddWaitStep("let it settle", 5);
            AddAssert("still not without mods", () => part(difficultyLine()!, "Stars:") != noModStars);

            AddStep("no mods", () => SelectedMods.Value = System.Array.Empty<osu.Game.Rulesets.Mods.Mod>());
            AddStep("reset", () => skins.CurrentSkinInfo.SetDefault());
        }

        [Test]
        public void TestStableBeatmapList()
        {
            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));
            AddStep("no grouping", () => Config.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.None));

            LoadSongSelect();
            ImportBeatmapForRuleset(0);
            ImportBeatmapForRuleset(0);

            AddUntilStep("stable panels shown", () => visibleStablePanels().Count() >= 4);
            AddAssert("no lazer panels", () => !SongSelect.ChildrenOfType<Panel>().Any(p => p.IsPresent));

            // Stable shows an open set as its difficulties: the set's own panel isn't there.
            AddUntilStep("open set's panel hidden", () => visibleStablePanels().Any(p => p.Item?.Model is GroupedBeatmap && p.Selected.Value)
                                                            && !visibleStablePanels().Any(p => p.Item?.Model is GroupedBeatmapSet && p.Expanded.Value));

            // The open set's difficulties sit further left than the other sets (50 in stable's 640x480).
            AddUntilStep("difficulties further left", () =>
            {
                var difficulty = visibleStablePanels().FirstOrDefault(p => p.Item?.Model is GroupedBeatmap && !p.Selected.Value);
                var otherSet = visibleStablePanels().FirstOrDefault(p => p.Item?.Model is GroupedBeatmapSet && !p.Expanded.Value);

                return difficulty != null && otherSet != null && difficulty.X < otherSet.X - 40;
            });

            // Like stable's: a closed set of several difficulties shows just its name, not a difficulty.
            AddAssert("closed set without a difficulty", () => visibleStablePanels().Where(p => p.Item?.Model is GroupedBeatmapSet && !p.Expanded.Value)
                                                                                   .All(p => p.DifficultyText.Length == 0));
            AddAssert("difficulties named", () => visibleStablePanels().Where(p => p.Item?.Model is GroupedBeatmap).All(p => p.DifficultyText.Length > 0));

            AddStep("stop using stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, false));
            AddUntilStep("lazer panels back", () => SongSelect.ChildrenOfType<Panel>().Any(p => p.IsPresent));
            AddUntilStep("stable panels gone", () => !visibleStablePanels().Any());

            AddStep("reset", () => skins.CurrentSkinInfo.SetDefault());
        }

        private System.Collections.Generic.IEnumerable<LegacyCarouselPanel> visibleStablePanels() =>
            SongSelect.ChildrenOfType<LegacyCarouselPanel>().Where(p => p.IsPresent && p.Item != null && p.Shown);

        [Test]
        public void TestStableGroupHeaders()
        {
            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));
            AddStep("group by artist", () => Config.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.Artist));

            LoadSongSelect();
            ImportBeatmapForRuleset(0);

            // Stable's: "Name (3 maps)".
            AddUntilStep("group header with count", () => SongSelect.ChildrenOfType<LegacyCarouselPanel>()
                                                                    .Where(p => p.Item?.Model is GroupDefinition)
                                                                    .SelectMany(p => p.ChildrenOfType<Graphics.Sprites.OsuSpriteText>())
                                                                    .Any(t => System.Text.RegularExpressions.Regex.IsMatch(t.Text.ToString(), @"\(\d+ maps?\)$")));

            AddStep("reset", () =>
            {
                Config.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.None);
                skins.CurrentSkinInfo.SetDefault();
            });
        }

        [Test]
        public void TestStableRandomKey()
        {
            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));

            LoadSongSelect();
            for (int i = 0; i < 5; i++)
                ImportBeatmapForRuleset(0);
            AddUntilStep("beatmap selected", () => !Beatmap.IsDefault);
            AddUntilStep("stable footer shown", () => SongSelect.ChildrenOfType<LegacyFooter>().Any(f => f.IsLoaded));

            BeatmapSetInfo? before = null;
            AddStep("press F2", () =>
            {
                before = Beatmap.Value.BeatmapSetInfo;
                InputManager.Key(Key.F2);
            });
            AddUntilStep("another set", () => !Beatmap.Value.BeatmapSetInfo.Equals(before));

            AddStep("press Shift+F2", () =>
            {
                InputManager.PressKey(Key.ShiftLeft);
                InputManager.Key(Key.F2);
                InputManager.ReleaseKey(Key.ShiftLeft);
            });
            AddUntilStep("back to the first", () => Beatmap.Value.BeatmapSetInfo.Equals(before));
        }

        [Test]
        public void TestStableBeatmapOptions()
        {
            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));

            LoadSongSelect();
            ImportBeatmapForRuleset(0);
            AddUntilStep("beatmap selected", () => !Beatmap.IsDefault);
            AddUntilStep("stable footer shown", () => SongSelect.ChildrenOfType<LegacyFooter>().Any(f => f.IsLoaded));

            // The options button (stable's F3): stable's dialog, numbered.
            AddStep("click options", () => SongSelect.ChildrenOfType<LegacyFooterButton>().Last(b => b is not LegacyRulesetFooterButton).TriggerClick());
            AddUntilStep("stable options shown", () => SongSelect.ChildrenOfType<LegacyButtonDialog>().Any(d => d.IsLoaded));
            AddAssert("its choices", () =>
            {
                var texts = SongSelect.ChildrenOfType<LegacyButtonDialog>().Single().ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Select(t => t.Text.ToString()).ToArray();
                return texts.Contains("1. Manage collections") && texts.Contains("2. Delete...") && texts.Any(t => t.EndsWith(". Cancel", System.StringComparison.Ordinal));
            });

            AddStep("cancel", () => SongSelect.ChildrenOfType<LegacyButtonDialog>().Single().ChildrenOfType<ClickableContainer>().Last().TriggerClick());
            AddUntilStep("closed", () => !SongSelect.ChildrenOfType<LegacyButtonDialog>().Any());

            // F3 opens them, and pressed again closes them (as F1 does the mods).
            AddStep("press F3", () => InputManager.Key(Key.F3));
            AddUntilStep("options shown again", () => SongSelect.ChildrenOfType<LegacyButtonDialog>().Any(d => d.IsLoaded));
            AddStep("press F3 again", () => InputManager.Key(Key.F3));
            AddUntilStep("closed by F3", () => !SongSelect.ChildrenOfType<LegacyButtonDialog>().Any());

            // Stable's: right-clicking a beatmap's panel opens its options too.
            AddStep("right-click a difficulty", () =>
            {
                var panel = SongSelect.ChildrenOfType<LegacyCarouselPanel>().First(p => p.IsPresent && p.Shown && p.Item?.Model is GroupedBeatmap);
                InputManager.MoveMouseTo(panel.Bar);
                InputManager.Click(osuTK.Input.MouseButton.Right);
            });
            AddUntilStep("options shown", () => SongSelect.ChildrenOfType<LegacyButtonDialog>().Any(d => d.IsLoaded));

            // Its numbers are its own (not typed into the search, which holds the keyboard in song select).
            AddStep("search focused as in the game", () => InputManager.ChangeFocus(SongSelect.ChildrenOfType<SearchTextBox>().First()));
            AddStep("open options again", () =>
            {
                SongSelect.ChildrenOfType<LegacyButtonDialog>().SingleOrDefault()?.Close();
            });
            AddUntilStep("closed", () => !SongSelect.ChildrenOfType<LegacyButtonDialog>().Any());
            AddStep("right-click a difficulty again", () =>
            {
                var panel = SongSelect.ChildrenOfType<LegacyCarouselPanel>().First(p => p.IsPresent && p.Shown && p.Item?.Model is GroupedBeatmap);
                InputManager.MoveMouseTo(panel.Bar);
                InputManager.Click(osuTK.Input.MouseButton.Right);
            });
            AddUntilStep("options shown again", () => SongSelect.ChildrenOfType<LegacyButtonDialog>().Any(d => d.IsLoaded));
            AddStep("press 9", () => InputManager.Key(osuTK.Input.Key.Number9));
            AddAssert("search still empty", () => SongSelect.ChildrenOfType<SearchTextBox>().All(t => string.IsNullOrEmpty(t.Text)));
            AddStep("press the cancel's number", () =>
            {
                var texts = SongSelect.ChildrenOfType<LegacyButtonDialog>().Single().ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Select(t => t.Text.ToString()).ToArray();
                int number = int.Parse(texts.First(t => t.EndsWith(". Cancel", System.StringComparison.Ordinal)).Split('.')[0], System.Globalization.CultureInfo.InvariantCulture);
                InputManager.Key(osuTK.Input.Key.Number1 + (number - 1));
            });
            AddUntilStep("closed by its number", () => !SongSelect.ChildrenOfType<LegacyButtonDialog>().Any());
            AddAssert("search still empty after", () => SongSelect.ChildrenOfType<SearchTextBox>().All(t => string.IsNullOrEmpty(t.Text)));

            AddStep("reset", () =>
            {
                Config.SetValue(OsuSetting.StableLayouts, false);
                skins.CurrentSkinInfo.SetDefault();
            });
        }

        [Test]
        public void TestStableModSelect()
        {
            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));
            AddStep("no mods", () => SelectedMods.Value = System.Array.Empty<osu.Game.Rulesets.Mods.Mod>());

            LoadSongSelect();
            ImportBeatmapForRuleset(0);
            AddUntilStep("stable footer shown", () => SongSelect.ChildrenOfType<LegacyFooter>().Any(f => f.IsLoaded));

            AddStep("click mods", () => SongSelect.ChildrenOfType<LegacyFooterButton>().Single(b => b is not LegacyRulesetFooterButton && b.X > 0
                                                                                                  && SongSelect.ChildrenOfType<LegacyFooterButton>()
                                                                                                               .Where(o => o is not LegacyRulesetFooterButton)
                                                                                                               .Min(o => o.X) == b.X).TriggerClick());
            AddUntilStep("stable mod selection shown", () => SongSelect.ChildrenOfType<LegacyModSelect>().Any(m => m.IsLoaded));

            // Like stable's: the DT slot goes DT, NC, off.
            AddStep("click DT slot", () => slot("DT").TriggerClick());
            AddUntilStep("DT on", () => SelectedMods.Value.Single().Acronym == "DT");
            // Stable's: the mods written out on the left.
            AddUntilStep("DoubleTime written", () => modsText() == "DoubleTime");
            // DT raises it, in the dialog and under the mods (the game's own calculation: mods' ScoreMultiplier is always 1 now).
            AddUntilStep("its score multiplier under it", () => multiplierText()?.StartsWith("Score Multiplier: ", System.StringComparison.Ordinal) == true
                                                                 && multiplierText() != "Score Multiplier: 1.00x");
            AddUntilStep("the dialog's raised too", () => SongSelect.ChildrenOfType<LegacyModSelect>().Single().ChildrenOfType<Graphics.Sprites.OsuSpriteText>()
                                                                     .Any(t => t.Text.ToString() == multiplierText()));
            AddStep("click DT slot again", () => slot("DT").TriggerClick());
            AddUntilStep("NC on", () => SelectedMods.Value.Single().Acronym == "NC");
            AddUntilStep("Nightcore written", () => modsText() == "Nightcore");
            AddStep("click DT slot again", () => slot("DT").TriggerClick());
            AddUntilStep("off", () => !SelectedMods.Value.Any());
            AddUntilStep("nothing written", () => modsText() == string.Empty);
            AddUntilStep("no score multiplier", () => multiplierText() == string.Empty);

            // Mods that can't go together: the newest stays.
            AddStep("HR then EZ", () =>
            {
                slot("HR").TriggerClick();
                slot("EZ").TriggerClick();
            });
            AddUntilStep("EZ only", () => SelectedMods.Value.Single().Acronym == "EZ");

            AddStep("reset", () => SongSelect.ChildrenOfType<LegacyButtonDialog>().Single().ChildrenOfType<ClickableContainer>()
                                                .First(c => c.ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Any(t => t.Text.ToString().EndsWith("Reset all mods", System.StringComparison.Ordinal)))
                                                .TriggerClick());
            AddUntilStep("no mods", () => !SelectedMods.Value.Any());
            AddAssert("still open", () => SongSelect.ChildrenOfType<LegacyModSelect>().Any());

            AddStep("reset", () =>
            {
                Config.SetValue(OsuSetting.StableLayouts, false);
                skins.CurrentSkinInfo.SetDefault();
            });
        }

        private string? multiplierText() => SongSelect.ChildrenOfType<LegacySongSelectLeaderboard>().SingleOrDefault()?
                                                      .ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Skip(1).FirstOrDefault()?.Text.ToString();

        private string? modsText() => SongSelect.ChildrenOfType<LegacySongSelectLeaderboard>().SingleOrDefault()?
                                                .ChildrenOfType<Graphics.Sprites.OsuSpriteText>().FirstOrDefault()?.Text.ToString();

        private LegacyModSelect.Slot slot(string acronym) => SongSelect.ChildrenOfType<LegacyModSelect.Slot>().First(s => s.Mods.Any(m => m.Acronym == acronym));

        [Test]
        public void TestModeIconDrawnAdditively()
        {
            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));

            LoadSongSelect();

            AddUntilStep("stable footer shown", () => SongSelect.ChildrenOfType<LegacyFooter>().Any(f => f.IsLoaded));

            // Like stable: skins (XooMoon) draw screen-sized art on black around the icon, which mustn't cover the screen.
            AddAssert("mode icon additive", () => SongSelect.ChildrenOfType<LegacyRulesetIcon>().Single()
                                                          .ChildrenOfType<Sprite>().Single().Blending == BlendingParameters.Additive);

            // Like stable's: above the other buttons (skins' options images would cover it otherwise).
            AddAssert("mode icon above the buttons", () =>
            {
                var footer = SongSelect.ChildrenOfType<LegacyFooter>().Single();
                var all = footer.ChildrenOfType<Drawable>().ToList();
                return all.IndexOf(SongSelect.ChildrenOfType<LegacyRulesetIcon>().Single()) > all.FindLastIndex(d => d is LegacyFooterButton);
            });

            AddStep("reset", () =>
            {
                Config.SetValue(OsuSetting.StableLayouts, false);
                skins.CurrentSkinInfo.SetDefault();
            });
        }

        [Test]
        public void TestModeButtonChangesRuleset()
        {
            AddStep("set legacy skin", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            AddStep("use stable layouts", () => Config.SetValue(OsuSetting.StableLayouts, true));

            LoadSongSelect();
            ImportBeatmapForRuleset(0);

            AddUntilStep("stable footer shown", () => SongSelect.ChildrenOfType<LegacyFooter>().Any(f => f.IsLoaded));
            AddAssert("osu! selected", () => Ruleset.Value.ShortName == "osu");

            // Stable's: the button opens the list of modes, a mode in it changes to it.
            AddStep("click mode", () => SongSelect.ChildrenOfType<LegacyRulesetFooterButton>().Single().TriggerClick());
            AddUntilStep("mode list open", () => SongSelect.ChildrenOfType<LegacyModeMenu>().Single().State.Value == Visibility.Visible);
            // The row under the name (names are drawn over all the icons, apart from the rows).
            AddStep("choose osu!taiko", () =>
            {
                var menu = SongSelect.ChildrenOfType<LegacyModeMenu>().Single();
                var name = menu.ChildrenOfType<Graphics.Sprites.OsuSpriteText>().Single(t => t.Text == "osu!taiko");
                menu.ChildrenOfType<ClickableContainer>().First(c => c.ScreenSpaceDrawQuad.Contains(name.ScreenSpaceDrawQuad.Centre)).TriggerClick();
            });
            AddUntilStep("taiko selected", () => Ruleset.Value.ShortName == "taiko");
            AddUntilStep("mode list closed", () => SongSelect.ChildrenOfType<LegacyModeMenu>().Single().State.Value == Visibility.Hidden);

            AddStep("reset", () =>
            {
                Config.SetValue(OsuSetting.StableLayouts, false);
                skins.CurrentSkinInfo.SetDefault();
            });
        }
    }
}

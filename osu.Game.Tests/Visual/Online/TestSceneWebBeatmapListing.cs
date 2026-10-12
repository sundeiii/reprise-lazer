// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Beatmaps.Drawables.Cards.Web;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>
    /// The beatmap listing after the website's (filters in columns, its views).
    /// </summary>
    public partial class TestSceneWebBeatmapListing : OsuManualInputManagerTestScene
    {
        /// <summary>
        /// Called with a name where a screenshot of the scene is wanted (by a runner showing it in a window; nothing in tests).
        /// </summary>
        public static Action<string>? Screenshot;

        private readonly List<APIBeatmapSet> sets = new List<APIBeatmapSet>();

        private BeatmapListingOverlay overlay = null!;
        private OsuConfigManager localConfig = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.Cache(localConfig = new OsuConfigManager(LocalStorage));
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("set view to normal", () => localConfig.SetValue(OsuSetting.BeatmapListingView, BeatmapListingView.Normal));

            AddStep("answer searches", () =>
            {
                sets.Clear();
                sets.AddRange(createSets());

                var api = (DummyAPIAccess)API;

                api.HandleRequest = req =>
                {
                    if (req is not SearchBeatmapSetsRequest search)
                        return false;

                    search.TriggerSuccess(new SearchBeatmapSetsResponse { BeatmapSets = sets });
                    return true;
                };
            });

            AddStep("show listing", () => Child = overlay = new BeatmapListingOverlay { State = { Value = Visibility.Visible } });
            AddUntilStep("wait for cards", () => overlay.ChildrenOfType<BeatmapCard>().Count(c => c.IsLoaded) == sets.Count);
        }

        [Test]
        public void TestViews()
        {
            foreach (var view in Enum.GetValues<BeatmapListingView>())
            {
                AddStep($"set view to {view}", () => localConfig.SetValue(OsuSetting.BeatmapListingView, view));

                if (view == BeatmapListingView.Extra)
                    AddUntilStep("game's extra cards", () => overlay.ChildrenOfType<BeatmapCardExtra>().Count(c => c.IsLoaded) == sets.Count);
                else
                    AddUntilStep($"website's {view} cards", () => overlay.ChildrenOfType<WebBeatmapCard>().Count(c => c.IsLoaded && c.View == view) == sets.Count);

                AddWaitStep("wait for covers", 10);
                AddStep($"screenshot {view}", () => Screenshot?.Invoke(view.ToString()));
            }
        }

        [Test]
        public void TestColumnsShareWidth()
        {
            AddStep("set view to normal", () => localConfig.SetValue(OsuSetting.BeatmapListingView, BeatmapListingView.Normal));
            AddUntilStep("two cards a row", () =>
            {
                var cards = overlay.ChildrenOfType<WebBeatmapCard>().Where(c => c.IsLoaded).ToArray();
                return cards.Length == sets.Count && cards.Select(c => c.Y).Distinct().Count() == sets.Count / 2 && cards.Select(c => c.X).Distinct().Count() == 2;
            });

            AddStep("set view to list", () => localConfig.SetValue(OsuSetting.BeatmapListingView, BeatmapListingView.List));
            AddUntilStep("one card a row", () =>
            {
                var cards = overlay.ChildrenOfType<WebBeatmapCard>().Where(c => c.IsLoaded && c.View == BeatmapListingView.List).ToArray();
                return cards.Length == sets.Count && cards.Select(c => c.Y).Distinct().Count() == cards.Length;
            });
        }

        [Test]
        public void TestRightClickMenus()
        {
            foreach (var view in new[] { BeatmapListingView.Normal, BeatmapListingView.Nano, BeatmapListingView.List })
            {
                AddStep($"set view to {view}", () => localConfig.SetValue(OsuSetting.BeatmapListingView, view));
                AddUntilStep($"{view} cards' menus have the buttons' actions", () =>
                {
                    var cards = overlay.ChildrenOfType<WebBeatmapCard>().Where(c => c.IsLoaded && c.View == view).ToArray();
                    return cards.Length == sets.Count && cards.All(c => c.ContextMenuItems.Length >= 2 && c.ContextMenuItems.Any(i => i.Text.Value.ToString() == "Discussion"));
                });
            }
        }


        [Test]
        public void TestCategoryPills()
        {
            AddAssert("a pill per category", () => overlay.ChildrenOfType<FilterTabItem<SearchCategory>>().Count() == Enum.GetValues<SearchCategory>().Length);
            AddWaitStep("wait", 3);
            AddStep("screenshot filters", () => Screenshot?.Invoke("Filters"));
        }

        private IEnumerable<APIBeatmapSet> createSets()
        {
            var statuses = new[]
            {
                BeatmapOnlineStatus.Ranked, BeatmapOnlineStatus.Loved, BeatmapOnlineStatus.Qualified, BeatmapOnlineStatus.Pending,
                BeatmapOnlineStatus.WIP, BeatmapOnlineStatus.Graveyard, BeatmapOnlineStatus.Approved, BeatmapOnlineStatus.Ranked,
            };

            for (int i = 0; i < statuses.Length; i++)
            {
                var set = CreateAPIBeatmapSet(new OsuRuleset().RulesetInfo);
                set.OnlineID = 1000 + i;
                set.Status = statuses[i];
                set.Title = i == 3 ? "An extremely long title that goes on and on until it cannot fit in the card" : $"Test song {i}";
                set.Artist = "Test artist";
                set.Source = i % 2 == 0 ? "Some source" : string.Empty;
                set.HasVideo = i % 3 == 0;
                set.HasStoryboard = i % 2 == 0;
                set.HasExplicitContent = i == 2;
                set.PlayCount = 1234 * (i + 1);
                set.FavouriteCount = 56 * (i + 1);
                set.Ranked = DateTimeOffset.Now.AddDays(-i * 30);
                set.LastUpdated = DateTimeOffset.Now.AddDays(-i * 40);

                var beatmaps = new List<APIBeatmap>();
                int count = i == 1 ? 24 : 2 + i;

                for (int d = 0; d < count; d++)
                {
                    int ruleset = i == 4 ? 3 : i == 5 ? d % 4 : 0;

                    beatmaps.Add(new APIBeatmap
                    {
                        OnlineID = set.OnlineID * 100 + d,
                        OnlineBeatmapSetID = set.OnlineID,
                        RulesetID = ruleset,
                        DifficultyName = $"Difficulty {d}",
                        StarRating = 1 + d * 0.8,
                        CircleSize = ruleset == 3 ? (d % 2 == 0 ? 4 : 7) : 4,
                        Status = set.Status,
                    });
                }

                set.Beatmaps = beatmaps.ToArray();

                yield return set;
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapSet;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>
    /// The beatmap page after the website's.
    /// </summary>
    public partial class TestSceneWebBeatmapSet : OsuTestScene
    {
        /// <summary>
        /// Called with a name where a screenshot of the scene is wanted (by a runner showing it in a window; nothing in tests).
        /// </summary>
        public static Action<string>? Screenshot;

        private BeatmapSetOverlay overlay = null!;

        private void showOverlay() => AddStep("show overlay", () => Child = overlay = new BeatmapSetOverlay { State = { Value = Visibility.Visible } });

        [Test]
        public void TestPage()
        {
            showOverlay();
            AddStep("show set", () => overlay.ShowBeatmapSet(createSet()));
            AddUntilStep("difficulties shown", () => overlay.ChildrenOfType<BeatmapPicker.DifficultySelectorButton>().Count() == 6);
            AddAssert("first chosen", () => overlay.ChildrenOfType<BeatmapPicker.DifficultySelectorButton>().First().State == BeatmapPicker.DifficultySelectorState.Selected);
            AddAssert("stat rows for osu!", () => overlay.ChildrenOfType<WebStatRow>().Count() == 5);
            AddWaitStep("wait for covers", 10);
            AddStep("screenshot page", () => Screenshot?.Invoke("Page"));

            AddStep("choose the hardest", () => overlay.ChildrenOfType<BeatmapPicker.DifficultySelectorButton>().Last().TriggerClick());
            AddAssert("hardest chosen", () => overlay.ChildrenOfType<BeatmapPicker.DifficultySelectorButton>().Last().State == BeatmapPicker.DifficultySelectorState.Selected);
            AddWaitStep("wait", 5);
            AddStep("screenshot hardest", () => Screenshot?.Invoke("Hardest"));
        }

        [Test]
        public void TestImportedSetShowsOsuMappers()
        {
            showOverlay();
            AddStep("show imported set", () =>
            {
                var set = createSet();
                set.Author = new APIUser { Id = 0, Username = "osuCreator" };
                set.RelatedUsers = Array.Empty<APIUser>();
                set.CurrentNominations = Array.Empty<BeatmapSetOnlineNomination>();
                set.OsuMappers = new TomYumMappers
                {
                    Creator = new OsuProfile { Id = 100, Username = "osuCreator" },
                    Beatmaps = { [set.Beatmaps[0].OnlineID] = new[] { new OsuProfile { Id = 200, Username = "osuGuest" } } },
                };
                set.OsuNominators = new[] { new OsuProfile { Id = 300, Username = "osuNominator" } };
                overlay.ShowBeatmapSet(set);
            });

            AddUntilStep("guest mapper on the difficulty line", () => texts().Contains("osuGuest"));
            AddAssert("creator credited", () => texts().Contains("osuCreator"));
            AddAssert("nominator credited", () => texts().Contains("osuNominator"));
            AddWaitStep("wait for avatars", 10);
            AddStep("screenshot imported", () => Screenshot?.Invoke("Imported"));

            string[] texts() => overlay.ChildrenOfType<OsuSpriteText>().Select(t => t.Text.ToString()).ToArray();
        }

        [Test]
        public void TestReadsWebsiteMappers()
        {
            AddStep("clear", () => Clear());
            AddAssert("website json read", () =>
            {
                var set = Newtonsoft.Json.JsonConvert.DeserializeObject<APIBeatmapSet>(
                    @"{""id"":1,""tomyum_mappers"":{""beatmaps"":{""55"":[{""id"":7,""username"":""guest""}]},""creator"":{""id"":8,""username"":""host""}},""tomyum_nominators"":[{""id"":""9"",""username"":""nom""}]}")!;
                return set.OsuMappers!.Creator!.Id == 8 && set.OsuMappers.Beatmaps[55][0].Username == "guest" && set.OsuNominators![0].Id == 9;
            });
        }

        private APIBeatmapSet createSet()
        {
            var set = CreateAPIBeatmapSet(new OsuRuleset().RulesetInfo);
            set.OnlineID = 163112;
            set.Status = BeatmapOnlineStatus.Ranked;
            set.HasVideo = true;
            set.HasStoryboard = true;
            set.TrackId = 1;
            set.Source = "Some anime";
            set.Tags = "kubaoms fast stream tech";

            var mapper = new APIUser { Id = 2, Username = "mapper" };
            var nominators = new[] { new APIUser { Id = 3, Username = "BanchoBot" }, new APIUser { Id = 4, Username = "nominator" } };

            set.Author = mapper;
            set.RelatedUsers = nominators.Append(mapper).ToArray();
            set.CurrentNominations = nominators.Select(n => new BeatmapSetOnlineNomination { UserId = n.Id, Rulesets = new[] { "osu" } }).ToArray();
            set.Ratings = Enumerable.Range(0, 11).Select(i => i * i).ToArray();

            var template = set.Beatmaps.First();

            set.Beatmaps = Enumerable.Range(0, 6).Select(i => new APIBeatmap
            {
                OnlineID = 1000 + i,
                OnlineBeatmapSetID = set.OnlineID,
                RulesetID = 0,
                DifficultyName = i == 5 ? "Extra Hard Insane" : $"Difficulty {i}",
                StarRating = 1.5 + i * 1.2,
                CircleSize = 4,
                DrainRate = 5 + i * 0.5f,
                OverallDifficulty = 6 + i * 0.5f,
                ApproachRate = 7 + i * 0.5f,
                BPM = 180,
                Length = 213000,
                HitLength = 200000,
                CircleCount = 300 + i * 100,
                SliderCount = 150 + i * 20,
                SpinnerCount = 2,
                PlayCount = 10000 / (i + 1),
                PassCount = 3000 / (i + 1),
                Status = set.Status,
                FailTimes = template.FailTimes,
                BeatmapSet = set,
            }).ToArray();

            return set;
        }
    }
}

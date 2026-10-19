// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Screens.SelectV1;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.SongSelect
{
    /// <summary>Song Select V1 (lazer's song select from before its redesign, behind <see cref="OsuSetting.SongSelectV1"/>): it opens, shows the beatmaps and selects one.</summary>
    public partial class TestSceneSongSelectV1 : ScreenTestScene
    {
        /// <summary>Set by a screenshot runner: called with a name once the screen is shown.</summary>
        public static Action<string>? Screenshot;

        private BeatmapManager beatmaps = null!;
        private RealmRulesetStore rulesets = null!;
        private RealmDetachedBeatmapStore beatmapStore = null!;
        private TestPlaySongSelect songSelect = null!;

        [Cached]
        private readonly LeaderboardManager leaderboardManager = new LeaderboardManager();

        [Cached(typeof(INotificationOverlay))]
        private readonly NotificationOverlay notifications = new NotificationOverlay();

        public TestSceneSongSelectV1()
        {
            Children = new Drawable[]
            {
                new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        leaderboardManager,
                        new Toolbar { State = { Value = Visibility.Visible } },
                    },
                },
            };

            Stack.Padding = new MarginPadding { Top = Toolbar.HEIGHT };
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            // As SongSelectTestScene: its own stores, so an interactive run doesn't touch the local install's beatmaps.
            dependencies.Cache(rulesets = new RealmRulesetStore(Realm));
            dependencies.Cache(Realm);
            dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, null, Dependencies.Get<AudioManager>(), Resources, Dependencies.Get<GameHost>(), Beatmap.Default));
            var config = new OsuConfigManager(LocalStorage);
            dependencies.Cache(config);
            dependencies.Cache(new ScoreManager(rulesets, () => beatmaps, LocalStorage, Realm, API, config));
            dependencies.CacheAs<BeatmapStore>(beatmapStore = new RealmDetachedBeatmapStore());

            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(beatmapStore);
        }

        [Test]
        public void TestShowsAndSelects()
        {
            AddStep("reset", () =>
            {
                Ruleset.Value = rulesets.AvailableRulesets.First();
                Beatmap.SetDefault();
                SelectedMods.SetDefault();
                beatmaps.Delete();
            });
            AddStep("import beatmaps", () =>
            {
                for (int i = 0; i < 8; i++)
                    beatmaps.Import(TestResources.CreateTestBeatmapSetInfo(4, rulesets.AvailableRulesets.Where(r => r.OnlineID == 0).ToArray()));
            });

            AddStep("load Song Select V1", () => Stack.Push(songSelect = new TestPlaySongSelect()));
            AddUntilStep("wait for load", () => Stack.CurrentScreen == songSelect && songSelect.IsLoaded);
            AddUntilStep("a beatmap is selected", () => !Beatmap.IsDefault);
            AddUntilStep("carousel shows the sets", () => songSelect.ChildrenOfType<Screens.SelectV1.Carousel.DrawableCarouselBeatmapSet>().Count(), () => Is.GreaterThan(0));
            AddWaitStep("let it settle", 15);
            AddStep("screenshot", () => Screenshot?.Invoke("song-select-v1"));
            AddWaitStep("saved", 5);

            // The mod select's multiplier (and ranked state) needs the ruleset: it showed 1.00x whatever the mods.
            AddAssert("mod select has the ruleset", () => songSelect.ModSelectOverlay.Ruleset.Value, () => Is.EqualTo(Ruleset.Value));
            AddAssert("mod select has the beatmap", () => songSelect.ModSelectOverlay.Beatmap.Value, () => Is.EqualTo(Beatmap.Value));
        }

        private partial class TestPlaySongSelect : PlaySongSelect
        {
            public ModSelectOverlay ModSelectOverlay => ModSelect;
        }
    }
}

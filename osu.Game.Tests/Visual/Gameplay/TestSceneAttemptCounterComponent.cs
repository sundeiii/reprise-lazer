// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking.Statistics.Session;
using osu.Game.Skinning.Components;
using osu.Game.Tests.Gameplay;
using osu.Game.Utils;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneAttemptCounterComponent : OsuTestScene
    {
        private SessionStatsStore store = null!;
        private GameplayState gameplayState = null!;
        private AttemptCounter counter = null!;

        private string beatmapKey => gameplayState.Beatmap.BeatmapInfo.ToString();

        private string shownText => counter.ChildrenOfType<OsuSpriteText>().Single().Text.ToString();

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            store = new SessionStatsStore(LocalStorage.GetStorageForDirectory(Guid.NewGuid().ToString("N")));
            gameplayState = TestGameplayState.Create(new OsuRuleset());

            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(SessionStatsStore), store),
                    (typeof(GameplayState), gameplayState),
                },
                Child = counter = new AttemptCounter
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
            };
        });

        private void recordPlayWithAccuracy(double accuracy) => store.Record(new ScoreInfo
        {
            ID = Guid.NewGuid(),
            Ruleset = new OsuRuleset().RulesetInfo,
            BeatmapInfo = gameplayState.Beatmap.BeatmapInfo,
            Accuracy = accuracy,
        }, DateTimeOffset.Now);

        [Test]
        public void TestShowsAFirstAttemptBeforeAnythingIsCounted()
        {
            // e.g. while editing the skin, when nothing is being played.
            AddUntilStep("component loaded", () => counter.IsLoaded);
            AddAssert("shows a first attempt", () => shownText == "Attempt #1");
        }

        [Test]
        public void TestFollowsAttemptsBeingCounted()
        {
            AddUntilStep("component loaded", () => counter.IsLoaded);

            AddStep("count three attempts", () =>
            {
                for (int i = 0; i < 3; i++)
                    store.RegisterAttempt(beatmapKey);
            });

            AddUntilStep("shows the third attempt", () => shownText == "Attempt #3");
        }

        [Test]
        public void TestOnlyFollowsItsOwnMap()
        {
            AddUntilStep("component loaded", () => counter.IsLoaded);

            AddStep("count several attempts at another map", () =>
            {
                for (int i = 0; i < 5; i++)
                    store.RegisterAttempt("another map");
            });

            AddWaitStep("wait", 3);
            AddAssert("unaffected", () => shownText == "Attempt #1");

            // the first counted attempt is attempt #1, so it takes two to reach #2.
            AddStep("count two attempts at this map", () =>
            {
                store.RegisterAttempt(beatmapKey);
                store.RegisterAttempt(beatmapKey);
            });

            AddUntilStep("shows the second attempt, not one inflated by the other map", () => shownText == "Attempt #2");
        }

        [Test]
        public void TestShowsTheBestAccuracySoFar()
        {
            AddUntilStep("component loaded", () => counter.IsLoaded);

            AddStep("finish a play, then start another attempt", () =>
            {
                recordPlayWithAccuracy(0.9);
                recordPlayWithAccuracy(0.9873);
                recordPlayWithAccuracy(0.95);
                store.RegisterAttempt(beatmapKey);
            });

            AddUntilStep("shows the best accuracy", () => shownText == $"Attempt #1 · best {0.9873.FormatAccuracy()}");
        }

        [Test]
        public void TestBestAccuracyCanBeHidden()
        {
            AddUntilStep("component loaded", () => counter.IsLoaded);

            AddStep("finish a play, then start another attempt", () =>
            {
                recordPlayWithAccuracy(0.95);
                store.RegisterAttempt(beatmapKey);
            });

            AddUntilStep("best accuracy shown", () => shownText.Contains("best", StringComparison.Ordinal));

            AddStep("hide best accuracy", () => counter.ShowBestAccuracy.Value = false);
            AddAssert("only the attempt is shown", () => shownText == "Attempt #1");

            AddStep("show best accuracy again", () => counter.ShowBestAccuracy.Value = true);
            AddAssert("best accuracy is back", () => shownText.Contains("best", StringComparison.Ordinal));
        }

        [Test]
        public void TestRemovedComponentStopsListening()
        {
            AddUntilStep("component loaded", () => counter.IsLoaded);
            AddAssert("listening to attempts", () => attemptListenerCount() == 1);

            AddStep("remove the component", () => Clear());

            // a handler left behind on the store would keep a removed component alive and updating forever.
            AddUntilStep("no longer listening", () => attemptListenerCount() == 0);
            AddStep("count an attempt", () => Assert.DoesNotThrow(() => store.RegisterAttempt(beatmapKey)));
        }

        // how many handlers are currently subscribed to the store's attempt event, read from the event's backing field.
        private int attemptListenerCount()
            => ((Delegate?)typeof(SessionStatsStore).GetField(nameof(SessionStatsStore.AttemptRegistered), BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(store))?.GetInvocationList().Length ?? 0;
    }
}

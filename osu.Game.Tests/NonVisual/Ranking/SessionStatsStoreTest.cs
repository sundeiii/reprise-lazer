// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics.Session;

namespace osu.Game.Tests.NonVisual.Ranking
{
    [TestFixture]
    public class SessionStatsStoreTest
    {
        private static ScoreInfo createScore(params double[] timeOffsets) => new ScoreInfo
        {
            ID = Guid.NewGuid(),
            Ruleset = new OsuRuleset().RulesetInfo,
            TotalScore = 1_000_000,
            Accuracy = 0.97,
            MaxCombo = 100,
            HitEvents = timeOffsets.Select(t => new HitEvent(t, 1.0, HitResult.Great, new HitObject(), null, null)).ToList(),
        };

        private static ScoreInfo createMapScore(string map, double accuracy, params double[] timeOffsets)
        {
            var score = createScore(timeOffsets);
            score.BeatmapInfo = new BeatmapInfo { DifficultyName = map };
            score.Accuracy = accuracy;
            return score;
        }

        [Test]
        public void TestRecordCapturesHitErrors()
        {
            using var storage = new TemporaryNativeStorage("session-stats-test");
            var store = new SessionStatsStore(storage);

            var record = store.Record(createScore(-10, -10, 0, 3, 3), DateTimeOffset.Now);

            Assert.That(record, Is.Not.Null);
            Assert.That(record!.AverageHitError, Is.EqualTo(-2.8).Within(0.0001));
            Assert.That(record.UnstableRate, Is.Not.Null);
            Assert.That(record.Accuracy, Is.EqualTo(0.97));

            // -10ms is two bins left of zero, 0ms is the centre bin, and 3ms rounds to one bin right of zero.
            int centre = SessionPlayRecord.HISTOGRAM_HALF_BINS;
            Assert.That(record.HitErrorHistogram[centre - 2], Is.EqualTo(2));
            Assert.That(record.HitErrorHistogram[centre], Is.EqualTo(1));
            Assert.That(record.HitErrorHistogram[centre + 1], Is.EqualTo(2));
        }

        [Test]
        public void TestOutOfRangeHitsAreClampedIntoOutermostBins()
        {
            using var storage = new TemporaryNativeStorage("session-stats-test");
            var store = new SessionStatsStore(storage);

            var record = store.Record(createScore(-500, 500), DateTimeOffset.Now)!;

            Assert.That(record.HitErrorHistogram.First(), Is.EqualTo(1));
            Assert.That(record.HitErrorHistogram.Last(), Is.EqualTo(1));
        }

        [Test]
        public void TestSameScoreIsNotRecordedTwice()
        {
            using var storage = new TemporaryNativeStorage("session-stats-test");
            var store = new SessionStatsStore(storage);
            var score = createScore(0, 1, 2);

            Assert.That(store.Record(score, DateTimeOffset.Now), Is.Not.Null);
            Assert.That(store.Record(score, DateTimeOffset.Now), Is.Null);
            Assert.That(store.CurrentSessionPlays, Has.Count.EqualTo(1));
        }

        [Test]
        public void TestPersistsAcrossSessions()
        {
            using var storage = new TemporaryNativeStorage("session-stats-test");

            var first = new SessionStatsStore(storage);
            first.Record(createScore(1, 2, 3), DateTimeOffset.Now);

            var second = new SessionStatsStore(storage);

            Assert.That(second.SessionId, Is.Not.EqualTo(first.SessionId));
            Assert.That(second.AllPlays, Has.Count.EqualTo(1));

            // a new session starts empty even though history was loaded.
            Assert.That(second.CurrentSessionPlays, Is.Empty);

            second.Record(createScore(1, 2, 3), DateTimeOffset.Now);

            Assert.That(second.AllPlays, Has.Count.EqualTo(2));
            Assert.That(second.CurrentSessionPlays, Has.Count.EqualTo(1));
        }

        [Test]
        public void TestCorruptFileStartsEmptyInsteadOfThrowing()
        {
            using var storage = new TemporaryNativeStorage("session-stats-test");

            using (var stream = storage.GetStream(SessionStatsStore.FILENAME, System.IO.FileAccess.Write, System.IO.FileMode.Create))
            using (var writer = new System.IO.StreamWriter(stream))
                writer.Write("{ this is not json");

            SessionStatsStore? store = null;
            Assert.DoesNotThrow(() => store = new SessionStatsStore(storage));
            Assert.That(store!.AllPlays, Is.Empty);
        }

        [Test]
        public void TestSessionsAreGroupedNewestFirst()
        {
            using var storage = new TemporaryNativeStorage("session-stats-test");

            var older = new SessionStatsStore(storage);
            older.Record(createScore(1, 2, 3), DateTimeOffset.Now.AddDays(-2));
            older.Record(createScore(1, 2, 3), DateTimeOffset.Now.AddDays(-2).AddMinutes(5));

            var newer = new SessionStatsStore(storage);
            newer.Record(createScore(1, 2, 3), DateTimeOffset.Now.AddDays(-1));

            var sessions = newer.GetSessions();

            Assert.That(sessions, Has.Count.EqualTo(2));
            Assert.That(sessions[0].SessionId, Is.EqualTo(newer.SessionId));
            Assert.That(sessions[0].Plays, Has.Count.EqualTo(1));
            Assert.That(sessions[1].SessionId, Is.EqualTo(older.SessionId));
            Assert.That(sessions[1].Plays, Has.Count.EqualTo(2));
            Assert.That(sessions[1].Plays[0].PlayedAt, Is.LessThan(sessions[1].Plays[1].PlayedAt));
        }

        [Test]
        public void TestHistogramsAreCombined()
        {
            using var storage = new TemporaryNativeStorage("session-stats-test");
            var store = new SessionStatsStore(storage);

            store.Record(createScore(0, 0, 5), DateTimeOffset.Now);
            store.Record(createScore(0, -5), DateTimeOffset.Now);

            int[] combined = SessionSummary.CombineHistograms(store.CurrentSessionPlays);
            int centre = SessionPlayRecord.HISTOGRAM_HALF_BINS;

            Assert.That(combined, Has.Length.EqualTo(centre * 2 + 1));
            Assert.That(combined[centre], Is.EqualTo(3));
            Assert.That(combined[centre + 1], Is.EqualTo(1));
            Assert.That(combined[centre - 1], Is.EqualTo(1));
            Assert.That(combined.Sum(), Is.EqualTo(5));
        }

        [Test]
        public void TestHistogramsWithUnexpectedLayoutAreIgnored()
        {
            var plays = new[]
            {
                new SessionPlayRecord { HitErrorHistogram = new[] { 1, 2, 3 } },
                new SessionPlayRecord { HitErrorHistogram = new int[SessionPlayRecord.HISTOGRAM_HALF_BINS * 2 + 1] },
            };

            Assert.That(SessionSummary.CombineHistograms(plays).Sum(), Is.EqualTo(0));
        }

        [Test]
        public void TestPersonalBestsAreComparedWithEarlierPlaysOfTheSameMap()
        {
            using var storage = new TemporaryNativeStorage("session-stats-test");
            var store = new SessionStatsStore(storage);

            var first = store.Record(createMapScore("A", 0.90, -30, 30, -30, 30), DateTimeOffset.Now)!;

            // nothing to compare the first play with.
            Assert.That(store.GetPersonalBests(first), Is.EqualTo(new PersonalBests(0, false, false)));
            Assert.That(store.GetPersonalBests(first).HasAnyNewBest, Is.False);

            var better = store.Record(createMapScore("A", 0.95, -2, 2, -2, 2), DateTimeOffset.Now)!;
            var bests = store.GetPersonalBests(better);

            Assert.That(bests.PreviousAttempts, Is.EqualTo(1));
            Assert.That(bests.NewBestUnstableRate, Is.True);
            Assert.That(bests.NewBestAccuracy, Is.True);
            Assert.That(bests.HasAnyNewBest, Is.True);

            var worse = store.Record(createMapScore("A", 0.92, -20, 20, -20, 20), DateTimeOffset.Now)!;
            Assert.That(store.GetPersonalBests(worse).HasAnyNewBest, Is.False);

            // a different map is a separate record to beat.
            var otherMap = store.Record(createMapScore("B", 0.50, -40, 40), DateTimeOffset.Now)!;
            Assert.That(store.GetPersonalBests(otherMap).PreviousAttempts, Is.EqualTo(0));
        }

        [Test]
        public void TestPersonalBestsIgnoreLaterPlays()
        {
            using var storage = new TemporaryNativeStorage("session-stats-test");
            var store = new SessionStatsStore(storage);

            var early = store.Record(createMapScore("A", 0.90, -5, 5), DateTimeOffset.Now)!;
            store.Record(createMapScore("A", 0.99, -1, 1), DateTimeOffset.Now);

            // the earlier play was a best at the time, whatever came after it.
            Assert.That(store.GetPersonalBests(early).PreviousAttempts, Is.EqualTo(0));
        }

        [Test]
        public void TestAttemptsAreCountedPerMap()
        {
            using var storage = new TemporaryNativeStorage("session-stats-test");
            var store = new SessionStatsStore(storage);

            Assert.That(store.GetAttempts("A"), Is.EqualTo(0));
            Assert.That(store.RegisterAttempt("A"), Is.EqualTo(1));
            Assert.That(store.RegisterAttempt("A"), Is.EqualTo(2));
            Assert.That(store.RegisterAttempt("B"), Is.EqualTo(1));
            Assert.That(store.GetAttempts("A"), Is.EqualTo(2));

            // a new session starts counting again.
            Assert.That(new SessionStatsStore(storage).GetAttempts("A"), Is.EqualTo(0));
        }

        [Test]
        public void TestPlaysAreGroupedByMapMostRecentFirst()
        {
            using var storage = new TemporaryNativeStorage("session-stats-test");
            var store = new SessionStatsStore(storage);
            var start = DateTimeOffset.Now.AddHours(-3);

            store.Record(createMapScore("A", 0.90, 1, 2), start);
            store.Record(createMapScore("B", 0.90, 1, 2), start.AddMinutes(10));
            store.Record(createMapScore("A", 0.95, 1, 2), start.AddMinutes(20));

            var maps = MapGroup.Create(store.AllPlays);

            Assert.That(maps, Has.Count.EqualTo(2));
            Assert.That(maps[0].Plays, Has.Count.EqualTo(2));
            Assert.That(maps[0].Plays[0].Accuracy, Is.EqualTo(0.90));
            Assert.That(maps[0].Plays[1].Accuracy, Is.EqualTo(0.95));
            Assert.That(maps[1].Plays, Has.Count.EqualTo(1));
        }
    }
}

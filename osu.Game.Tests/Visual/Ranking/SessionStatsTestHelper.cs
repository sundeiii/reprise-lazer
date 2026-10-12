// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics.Session;

namespace osu.Game.Tests.Visual.Ranking
{
    /// <summary>
    /// Creates believable fake session history for tests.
    /// </summary>
    internal static class SessionStatsTestHelper
    {
        public static ScoreInfo CreateScore(Random random, double spread, string? map = null) => new ScoreInfo
        {
            ID = Guid.NewGuid(),
            BeatmapInfo = map == null ? null : new BeatmapInfo { DifficultyName = map },
            Ruleset = new OsuRuleset().RulesetInfo,
            Accuracy = 0.9 + random.NextDouble() * 0.09,
            MaxCombo = random.Next(50, 400),
            HitEvents = Enumerable.Range(0, 200)
                                  .Select(_ => new HitEvent((random.NextDouble() - 0.5) * spread, 1.0, HitResult.Great, new HitObject(), null, null))
                                  .ToList(),
        };

        /// <summary>
        /// Records <paramref name="plays"/> plays into <paramref name="store"/>, getting steadily more consistent,
        /// and switching from manual play to Relax + aim assist half way.
        /// </summary>
        public static void FillSession(SessionStatsStore store, Random random, int plays, DateTimeOffset start)
        {
            for (int i = 0; i < plays; i++)
            {
                double spread = 40 - i * 2.5;

                store.Record(CreateScore(random, spread, i % 2 == 0 ? "Map A" : "Map B"), start.AddMinutes(i * 4));
            }
        }

        /// <summary>
        /// Creates a store which already holds <paramref name="pastSessions"/> finished sessions,
        /// and has recorded <paramref name="currentPlays"/> plays in its own (current) session.
        /// </summary>
        public static SessionStatsStore CreateStore(Storage storage, int pastSessions, int currentPlays)
        {
            var random = new Random(1234);

            for (int s = 0; s < pastSessions; s++)
            {
                // a new store instance is a new session.
                var past = new SessionStatsStore(storage);
                FillSession(past, random, 5 + s * 2, DateTimeOffset.Now.AddDays(-(pastSessions - s)));
            }

            var store = new SessionStatsStore(storage);
            FillSession(store, random, currentPlays, DateTimeOffset.Now.AddMinutes(-currentPlays * 4));
            return store;
        }
    }
}

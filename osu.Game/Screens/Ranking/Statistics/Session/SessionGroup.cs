// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Screens.Ranking.Statistics.Session
{
    /// <summary>
    /// All plays of a single beatmap, across sessions.
    /// </summary>
    public class MapGroup
    {
        public string Beatmap { get; }

        /// <summary>
        /// The plays of this beatmap, oldest first.
        /// </summary>
        public IReadOnlyList<SessionPlayRecord> Plays { get; }

        public DateTimeOffset LastPlayed { get; }

        public MapGroup(string beatmap, IEnumerable<SessionPlayRecord> plays)
        {
            Beatmap = beatmap;
            Plays = plays.OrderBy(p => p.PlayedAt).ToList();
            LastPlayed = Plays.Count == 0 ? DateTimeOffset.MinValue : Plays[^1].PlayedAt;
        }

        /// <summary>
        /// Groups <paramref name="plays"/> by beatmap, most recently played first.
        /// </summary>
        public static IReadOnlyList<MapGroup> Create(IEnumerable<SessionPlayRecord> plays)
            => plays.GroupBy(p => p.Beatmap)
                    .Select(g => new MapGroup(g.Key, g))
                    .OrderByDescending(m => m.LastPlayed)
                    .ToList();
    }

    /// <summary>
    /// All plays recorded during a single session.
    /// </summary>
    public class SessionGroup
    {
        public string SessionId { get; }

        /// <summary>
        /// The plays of this session, oldest first.
        /// </summary>
        public IReadOnlyList<SessionPlayRecord> Plays { get; }

        public DateTimeOffset Start { get; }

        public DateTimeOffset End { get; }

        public SessionGroup(string sessionId, IEnumerable<SessionPlayRecord> plays)
        {
            SessionId = sessionId;
            Plays = plays.OrderBy(p => p.PlayedAt).ToList();

            Start = Plays.Count == 0 ? DateTimeOffset.MinValue : Plays[0].PlayedAt;
            End = Plays.Count == 0 ? DateTimeOffset.MinValue : Plays[^1].PlayedAt;
        }
    }
}

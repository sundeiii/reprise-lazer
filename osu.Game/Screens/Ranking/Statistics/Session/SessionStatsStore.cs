// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Scoring;

namespace osu.Game.Screens.Ranking.Statistics.Session
{
    /// <summary>
    /// Keeps a local, persisted history of finished plays, and knows which of them belong to the current session.
    /// A session lasts from game launch until the game is closed.
    /// </summary>
    public class SessionStatsStore
    {
        public const string FILENAME = "session-stats.json";

        /// <summary>
        /// The most records kept on disk. The oldest are dropped first.
        /// </summary>
        private const int max_stored_records = 2000;

        /// <summary>
        /// Identifies the current session.
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// All plays recorded in the current session, oldest first.
        /// </summary>
        public IReadOnlyList<SessionPlayRecord> CurrentSessionPlays
        {
            get
            {
                lock (syncLock)
                    return records.Where(r => r.SessionId == SessionId).ToList();
            }
        }

        /// <summary>
        /// All stored plays, across sessions, oldest first.
        /// </summary>
        public IReadOnlyList<SessionPlayRecord> AllPlays
        {
            get
            {
                lock (syncLock)
                    return records.ToList();
            }
        }

        /// <summary>
        /// All stored sessions, newest first.
        /// </summary>
        public IReadOnlyList<SessionGroup> GetSessions()
        {
            lock (syncLock)
            {
                return records.GroupBy(r => r.SessionId)
                              .Select(g => new SessionGroup(g.Key, g))
                              .OrderByDescending(s => s.Start)
                              .ToList();
            }
        }

        /// <summary>
        /// Counts an attempt at the beatmap identified by <paramref name="beatmapKey"/> in the current session.
        /// Unlike recorded plays, attempts include ones which were retried or quit before finishing.
        /// </summary>
        /// <returns>The number of this attempt, starting from 1.</returns>
        public int RegisterAttempt(string beatmapKey)
        {
            int attempt;

            lock (syncLock)
            {
                attempt = attempts.GetValueOrDefault(beatmapKey) + 1;
                attempts[beatmapKey] = attempt;
            }

            AttemptRegistered?.Invoke(beatmapKey);
            return attempt;
        }

        /// <summary>
        /// Invoked, with the beatmap it was for, whenever <see cref="RegisterAttempt"/> counts an attempt.
        /// May be invoked from any thread.
        /// </summary>
        public event Action<string>? AttemptRegistered;

        /// <summary>
        /// How many attempts at the beatmap identified by <paramref name="beatmapKey"/> have been made in the current session.
        /// </summary>
        public int GetAttempts(string beatmapKey)
        {
            lock (syncLock)
                return attempts.GetValueOrDefault(beatmapKey);
        }

        /// <summary>
        /// Checks whether <paramref name="play"/> beat every earlier play of the same beatmap.
        /// </summary>
        public PersonalBests GetPersonalBests(SessionPlayRecord play)
        {
            lock (syncLock)
            {
                int index = records.IndexOf(play);

                var previous = records.Where((r, i) => (index < 0 || i < index)
                                                       && r.ScoreID != play.ScoreID
                                                       && r.Beatmap == play.Beatmap)
                                     .ToList();

                var previousUnstableRates = previous.Where(p => p.UnstableRate != null).Select(p => p.UnstableRate!.Value).ToList();

                return new PersonalBests(
                    previous.Count,
                    play.UnstableRate != null && previousUnstableRates.Count > 0 && play.UnstableRate < previousUnstableRates.Min(),
                    previous.Count > 0 && play.Accuracy > previous.Max(p => p.Accuracy));
            }
        }

        private readonly Dictionary<string, int> attempts = new Dictionary<string, int>();
        private readonly Storage storage;
        private readonly List<SessionPlayRecord> records;
        private readonly object syncLock = new object();

        public SessionStatsStore(Storage storage)
        {
            this.storage = storage;

            SessionId = Guid.NewGuid().ToString("N");
            records = load();
        }

        /// <summary>
        /// Records a just-finished <paramref name="score"/> into the current session.
        /// </summary>
        /// <returns>The created record, or <see langword="null"/> if the score was already recorded.</returns>
        public SessionPlayRecord? Record(ScoreInfo score) => Record(score, DateTimeOffset.Now);

        /// <summary>
        /// Records a just-finished <paramref name="score"/> into the current session.
        /// </summary>
        /// <returns>The created record, or <see langword="null"/> if the score was already recorded.</returns>
        public SessionPlayRecord? Record(ScoreInfo score, DateTimeOffset playedAt)
        {
            lock (syncLock)
            {
                if (records.Any(r => r.ScoreID == score.ID))
                    return null;

                var record = SessionPlayRecord.FromScore(score, SessionId, playedAt);
                records.Add(record);

                if (records.Count > max_stored_records)
                    records.RemoveRange(0, records.Count - max_stored_records);

                save();
                return record;
            }
        }

        private List<SessionPlayRecord> load()
        {
            try
            {
                if (!storage.Exists(FILENAME))
                    return new List<SessionPlayRecord>();

                using var stream = storage.GetStream(FILENAME);
                using var reader = new StreamReader(stream);

                return JsonConvert.DeserializeObject<List<SessionPlayRecord>>(reader.ReadToEnd()) ?? new List<SessionPlayRecord>();
            }
            catch (Exception e)
            {
                // a broken stats file should never get in the way of playing.
                Logger.Error(e, $"Failed to load {FILENAME}, starting with empty session stats.");
                return new List<SessionPlayRecord>();
            }
        }

        private void save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(records, Formatting.None);

                using var stream = storage.GetStream(FILENAME, FileAccess.Write, FileMode.Create);
                using var writer = new StreamWriter(stream);

                writer.Write(json);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to save {FILENAME}.");
            }
        }
    }
}

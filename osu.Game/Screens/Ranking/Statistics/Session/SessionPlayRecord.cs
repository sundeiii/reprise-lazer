// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Screens.Ranking.Statistics.Session
{
    /// <summary>
    /// A summary of one finished play, persisted locally so that stats can be shown across a session.
    /// </summary>
    public class SessionPlayRecord
    {
        /// <summary>
        /// Width (in milliseconds) of each bin of <see cref="HitErrorHistogram"/>.
        /// </summary>
        public const int HISTOGRAM_BIN_WIDTH = 5;

        /// <summary>
        /// Number of bins on each side of zero in <see cref="HitErrorHistogram"/>.
        /// </summary>
        public const int HISTOGRAM_HALF_BINS = 20;

        [JsonProperty("score_id")]
        public Guid ScoreID { get; set; }

        [JsonProperty("session_id")]
        public string SessionId { get; set; } = string.Empty;

        [JsonProperty("played_at")]
        public DateTimeOffset PlayedAt { get; set; }

        [JsonProperty("beatmap")]
        public string Beatmap { get; set; } = string.Empty;

        [JsonProperty("ruleset")]
        public string Ruleset { get; set; } = string.Empty;

        [JsonProperty("mods")]
        public string[] Mods { get; set; } = [];

        [JsonProperty("total_score")]
        public long TotalScore { get; set; }

        /// <summary>
        /// Accuracy in the range 0 to 1.
        /// </summary>
        [JsonProperty("accuracy")]
        public double Accuracy { get; set; }

        [JsonProperty("max_combo")]
        public int MaxCombo { get; set; }

        [JsonProperty("miss_count")]
        public int MissCount { get; set; }

        [JsonProperty("unstable_rate")]
        public double? UnstableRate { get; set; }

        /// <summary>
        /// Average hit error in milliseconds. Negative means early.
        /// </summary>
        [JsonProperty("average_hit_error")]
        public double? AverageHitError { get; set; }

        /// <summary>
        /// Hit error counts, from -100ms to +100ms in <see cref="HISTOGRAM_BIN_WIDTH"/>ms bins.
        /// Out of range hits are clamped into the outermost bins.
        /// </summary>
        [JsonProperty("hit_error_histogram")]
        public int[] HitErrorHistogram { get; set; } = [];

        /// <summary>
        /// Creates a record from a just-finished <paramref name="score"/>.
        /// </summary>
        public static SessionPlayRecord FromScore(ScoreInfo score, string sessionId, DateTimeOffset playedAt)
        {
            var hitEvents = score.HitEvents;

            int[] histogram = new int[HISTOGRAM_HALF_BINS * 2 + 1];

            foreach (var e in hitEvents.Where(HitEventExtensions.AffectsUnstableRate))
            {
                int bin = (int)Math.Clamp(Math.Round(e.TimeOffset / HISTOGRAM_BIN_WIDTH), -HISTOGRAM_HALF_BINS, HISTOGRAM_HALF_BINS);
                histogram[bin + HISTOGRAM_HALF_BINS]++;
            }

            // unstable rate requires the gameplay rate to be known for every event.
            double? unstableRate = hitEvents.All(e => e.GameplayRate != null) ? hitEvents.CalculateUnstableRate()?.Result : null;

            return new SessionPlayRecord
            {
                ScoreID = score.ID,
                SessionId = sessionId,
                PlayedAt = playedAt,
                Beatmap = score.BeatmapInfo?.ToString() ?? string.Empty,
                Ruleset = score.Ruleset.ShortName,
                Mods = score.Mods.Select(m => m.Acronym).ToArray(),
                TotalScore = score.TotalScore,
                Accuracy = score.Accuracy,
                MaxCombo = score.MaxCombo,
                MissCount = score.Statistics.GetValueOrDefault(HitResult.Miss),
                UnstableRate = unstableRate,
                AverageHitError = hitEvents.CalculateAverageHitError(),
                HitErrorHistogram = histogram,
            };
        }
    }
}

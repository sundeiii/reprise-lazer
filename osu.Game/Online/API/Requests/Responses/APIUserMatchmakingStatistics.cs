// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Responses
{
    public class APIUserMatchmakingStatistics
    {
        [JsonProperty("user_id")]
        public int UserId;

        [JsonProperty("pool_id")]
        public int PoolId { get; set; }

        [JsonProperty("rating")]
        public int Rating { get; set; }

        [JsonProperty("rank")]
        public int? Rank { get; set; }

        [JsonProperty("plays")]
        public int Plays { get; set; }

        [JsonProperty("total_points")]
        public int TotalPoints { get; set; }

        [JsonProperty("first_placements")]
        public int FirstPlacements { get; set; }

        [JsonProperty("is_rating_provisional")]
        public bool IsRatingProvisional { get; set; }

        [JsonProperty("pool")]
        public APIMatchmakingPool Pool { get; set; } = new APIMatchmakingPool();

        /// <summary>Reprise: the latest matches (newest first, up to ten), as the profile shows them.</summary>
        [JsonProperty("recent_history")]
        public APIMatchmakingEloHistory[] RecentHistory { get; set; } = [];
    }

    /// <summary>Reprise: one ranked play match's result for a player, and their rating after it.</summary>
    public class APIMatchmakingEloHistory
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("room_id")]
        public long RoomId { get; set; }

        /// <summary>"win", "loss" or "draw".</summary>
        [JsonProperty("result")]
        public string Result { get; set; } = string.Empty;

        [JsonProperty("elo_after")]
        public int EloAfter { get; set; }

        [JsonProperty("created_at")]
        public System.DateTimeOffset CreatedAt { get; set; }
    }
}

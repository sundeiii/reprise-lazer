// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// The beatmap set's rating and the player's own vote (osu-web's <c>beatmapsets/{id}/ratings</c>).
    /// </summary>
    public class GetBeatmapsetRatingRequest : APIRequest<GetBeatmapsetRatingRequest.Response>
    {
        private readonly int beatmapSetId;

        public GetBeatmapsetRatingRequest(int beatmapSetId)
        {
            this.beatmapSetId = beatmapSetId;
        }

        protected override string Target => $@"beatmapsets/{beatmapSetId}/ratings";

        public class Response
        {
            /// <summary>
            /// Why the player can't rate it (not played it, for one), or null when they can.
            /// </summary>
            [JsonProperty("disallow_rating_reason")]
            public string? DisallowRatingReason { get; set; }

            [JsonProperty("total_rating")]
            public double TotalRating { get; set; }

            /// <summary>
            /// The player's vote (1-10), or null when they haven't voted.
            /// </summary>
            [JsonProperty("user_rating")]
            public int? UserRating { get; set; }
        }
    }
}

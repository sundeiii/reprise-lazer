// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.IO.Network;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// Votes 1-10 for a beatmap set (osu-web's <c>beatmapsets/{id}/ratings</c>), replying with its new rating.
    /// </summary>
    public class PostBeatmapsetRatingRequest : APIRequest<PostBeatmapsetRatingRequest.Response>
    {
        private readonly int beatmapSetId;
        private readonly int rating;

        public PostBeatmapsetRatingRequest(int beatmapSetId, int rating)
        {
            this.beatmapSetId = beatmapSetId;
            this.rating = rating;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Method = HttpMethod.Post;
            req.AddParameter(@"rating", rating.ToString(CultureInfo.InvariantCulture));
            return req;
        }

        protected override string Target => $@"beatmapsets/{beatmapSetId}/ratings";

        public class Response
        {
            [JsonProperty("updated_rating")]
            public double UpdatedRating { get; set; }
        }
    }
}

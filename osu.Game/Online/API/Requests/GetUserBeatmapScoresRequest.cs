// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// All of a player's scores on a beatmap (<c>beatmaps/{beatmap}/scores/users/{user}/all</c>), for the stable results screen's
    /// "Beatmap Ranking" row (this play against their best before it).
    /// </summary>
    public class GetUserBeatmapScoresRequest : APIRequest<GetUserBeatmapScoresRequest.Response>
    {
        private readonly int beatmapId;
        private readonly long userId;
        private readonly IRulesetInfo ruleset;

        public GetUserBeatmapScoresRequest(int beatmapId, long userId, IRulesetInfo ruleset)
        {
            this.beatmapId = beatmapId;
            this.userId = userId;
            this.ruleset = ruleset;
        }

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.AddParameter(@"ruleset", ruleset.ShortName);
            return request;
        }

        protected override string Target => $@"beatmaps/{beatmapId}/scores/users/{userId}/all";

        public class Response
        {
            [JsonProperty(@"scores")]
            public List<SoloScoreInfo> Scores { get; set; } = new List<SoloScoreInfo>();
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using osu.Framework.IO.Network;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// Reprise: where a play would place on osu!'s own leaderboard for the map (for a player with osu!'s scores on, else
    /// <see cref="BanchoPlace.Enabled"/> is false).
    /// </summary>
    public class GetBanchoPlaceRequest : APIRequest<BanchoPlace>
    {
        private readonly int beatmapId;
        private readonly string ruleset;
        private readonly long score;
        private readonly long? legacyScore;
        private readonly IEnumerable<string> mods;

        public GetBanchoPlaceRequest(int beatmapId, string ruleset, long score, long? legacyScore, IEnumerable<string> mods)
        {
            this.beatmapId = beatmapId;
            this.ruleset = ruleset;
            this.score = score;
            this.legacyScore = legacyScore;
            this.mods = mods;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();

            req.AddParameter(@"mode", ruleset);
            req.AddParameter(@"score", score.ToString(CultureInfo.InvariantCulture));
            if (legacyScore > 0)
                req.AddParameter(@"legacy_score", legacyScore.Value.ToString(CultureInfo.InvariantCulture));

            foreach (string mod in mods)
                req.AddParameter(@"mods[]", mod);

            return req;
        }

        protected override string Target => $@"beatmaps/{beatmapId}/tomyum-bancho-place";
    }

    public class BanchoPlace
    {
        /// <summary>Whether the player has osu!'s scores on.</summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        /// <summary>The place the play would take (null when osu! has no board for the map, or it couldn't be told).</summary>
        [JsonProperty("place")]
        public int? Place { get; set; }

        /// <summary>How many plays osu!'s board has.</summary>
        [JsonProperty("count")]
        public int Count { get; set; }
    }
}

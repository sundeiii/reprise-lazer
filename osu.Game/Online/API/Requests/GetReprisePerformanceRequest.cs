// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// Reprise: a play's pp as the server counts it (its own rules: Relax, Difficulty Adjust, a DT's speed), or what it
    /// would be worth on a map that gives none (<see cref="ReprisePerformance.Ranked"/> false).
    /// </summary>
    public class GetReprisePerformanceRequest : APIRequest<ReprisePerformance>
    {
        private readonly long scoreId;

        public GetReprisePerformanceRequest(long scoreId)
        {
            this.scoreId = scoreId;
        }

        protected override string Target => $@"scores/{scoreId}/tomyum-pp";
    }

    public class ReprisePerformance
    {
        /// <summary>The pp; null while the server is still working it out, or for a fail.</summary>
        [JsonProperty("pp")]
        public double? PP { get; set; }

        /// <summary>Whether the map gives pp (else <see cref="PP"/> is what it would if it were ranked).</summary>
        [JsonProperty("ranked")]
        public bool Ranked { get; set; }
    }
}

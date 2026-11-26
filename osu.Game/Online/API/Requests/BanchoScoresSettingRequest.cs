// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.IO.Network;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// Reprise: whether osu!'s own (bancho's) top scores show on the player's leaderboards (the account's setting, also on the website
    /// and BanchoBot's !bancho). Reads it, or sets it when <see cref="Enabled"/> is given.
    /// </summary>
    public class BanchoScoresSettingRequest : APIRequest<BanchoScoresSettingRequest.Response>
    {
        public readonly bool? Enabled;

        public BanchoScoresSettingRequest(bool? enabled = null)
        {
            Enabled = enabled;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();

            if (Enabled is bool enabled)
            {
                req.Method = HttpMethod.Put;
                req.AddParameter(@"enabled", enabled ? @"1" : @"0");
            }

            return req;
        }

        protected override string Target => @"me/tomyum-bancho-scores";

        public class Response
        {
            [JsonProperty("enabled")]
            public bool Enabled { get; set; }
        }
    }
}

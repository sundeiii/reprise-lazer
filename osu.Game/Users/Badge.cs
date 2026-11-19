// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using Newtonsoft.Json;

namespace osu.Game.Users
{
    public class Badge
    {
        [JsonProperty("awarded_at")]
        public DateTimeOffset AwardedAt;

        [JsonProperty("description")]
        public string Description;

        /// <summary>Reprise: the image's filename ("fad-2023.png"), which is what a pinned badge (profile_pinned_badge) is named by.</summary>
        [JsonProperty("image")]
        public string Image;

        /// <summary>Reprise: its title, when it has one (shown instead of the description, as the website does).</summary>
        [JsonProperty("name")]
        public string Name;

        [JsonProperty("image@2x_url")]
        public string ImageUrl;

        [JsonProperty("image_url")]
        public string ImageUrlLowRes;

        [JsonProperty("url")]
        public string Url;
    }
}

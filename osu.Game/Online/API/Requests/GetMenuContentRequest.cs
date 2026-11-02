// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.API.Requests
{
    public class GetMenuContentRequest : OsuJsonWebRequest<APIMenuContent>
    {
        public GetMenuContentRequest(string? websiteUrl = null)
            : base(UrlFor(websiteUrl))
        {
        }

        /// <summary>
        /// Reprise: the banners come from the server's own asset host (assets.reprise.pw for osu.reprise.pw, where staff set them
        /// in the admin panel or on Discord), as osu!'s come from assets.ppy.sh for osu.ppy.sh. A site whose host isn't osu.* gets osu!'s.
        /// </summary>
        public static string UrlFor(string? websiteUrl)
        {
            if (Uri.TryCreate(websiteUrl, UriKind.Absolute, out var uri) && uri.Host.StartsWith(@"osu.", StringComparison.OrdinalIgnoreCase))
                return $@"{uri.Scheme}://assets.{uri.Host[4..]}/menu-content.json";

            return @"https://assets.ppy.sh/menu-content.json";
        }
    }
}

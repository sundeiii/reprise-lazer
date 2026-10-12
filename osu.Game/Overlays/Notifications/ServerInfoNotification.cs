// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
// This file is originally created by GooGuTeam.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Notifications
{
    /// <summary>
    /// A notification showing the current server information on startup.
    /// </summary>
    public partial class ServerInfoNotification : SimpleNotification
    {
        private readonly string serverUrl;

        public ServerInfoNotification(string serverUrl)
        {
            this.serverUrl = serverUrl;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Icon = FontAwesome.Solid.Server;
            IconContent.Colour = colours.BlueLight;
            Text = GetServerDisplayText(serverUrl);
        }

        private static LocalisableString GetServerDisplayText(string serverUrl)
        {
            if (string.IsNullOrEmpty(serverUrl))
                return OnlineSettingsStrings.ConnectedToDefaultServer;

            string displayName = ExtractDisplayName(serverUrl);
            return OnlineSettingsStrings.CurrentServer(displayName);
        }

        private static string ExtractDisplayName(string url)
        {
            if (string.IsNullOrEmpty(url))
                return OnlineSettingsStrings.DefaultServer.ToString();

            try
            {
                string cleanUrl = url.Replace("https://", "").Replace("http://", "");

                int pathIndex = cleanUrl.IndexOf('/');
                if (pathIndex > 0)
                    cleanUrl = cleanUrl[..pathIndex];

                return cleanUrl.ToLowerInvariant() switch
                {
                    "osu.ppy.sh" => OnlineSettingsStrings.OfficialServer.ToString(),
                    "dev.ppy.sh" => OnlineSettingsStrings.DevelopmentServer.ToString(),
                    _ => cleanUrl
                };
            }
            catch
            {
                return url;
            }
        }
    }
}

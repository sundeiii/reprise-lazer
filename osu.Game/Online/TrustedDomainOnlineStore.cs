// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Game.Configuration;

namespace osu.Game.Online
{
    public sealed class TrustedDomainOnlineStore : OnlineStore
    {
        private readonly OsuConfigManager? configManager;

        public TrustedDomainOnlineStore(OsuConfigManager? configManager = null)
        {
            this.configManager = configManager;
        }

        protected override string GetLookupUrl(string url)
        {
            string? customApiUrl = configManager?.Get<string>(OsuSetting.CustomApiUrl);
            if (!string.IsNullOrWhiteSpace(customApiUrl))
                return url;

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                uri.Host.EndsWith(".ppy.sh", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            Logger.Log(
                $"[TrustedDomainOnlineStore] Blocked external resource lookup: {url}",
                LoggingTarget.Network,
                LogLevel.Important
            );

            return string.Empty;
        }
    }
}

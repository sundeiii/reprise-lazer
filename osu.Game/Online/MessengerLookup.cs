// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Configuration;

namespace osu.Game.Online
{
    /// <summary>
    /// Reprise: a player's MSN Messenger address (msn.reprise.pw), when they chose to show it there, and whether they're signed in to
    /// Messenger. Only for reprise.pw (the MSN site's accounts are linked to its players' ids). Kept a minute, a miss too, as the website
    /// keeps it; an MSN site that's down just leaves it off.
    /// </summary>
    public static class MessengerLookup
    {
        public const string MSN_URL = @"https://msn.reprise.pw";

        private static readonly TimeSpan cache_duration = TimeSpan.FromMinutes(1);

        private static readonly ConcurrentDictionary<int, (DateTimeOffset Fetched, MessengerAccount? Account)> cache = new ConcurrentDictionary<int, (DateTimeOffset, MessengerAccount?)>();

        /// <summary>Whether the given website is the one the MSN site's accounts belong to.</summary>
        public static bool AppliesTo(string websiteUrl) =>
            Uri.TryCreate(websiteUrl, UriKind.Absolute, out var uri) && uri.Host.Equals(OsuConfigManager.REPRISE_HOST, StringComparison.OrdinalIgnoreCase);

        /// <summary>The player's Messenger address and whether they're on, or null when they don't show one (or it can't be told).</summary>
        public static async Task<MessengerAccount?> GetAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 1)
                return null;

            if (cache.TryGetValue(userId, out var cached) && DateTimeOffset.Now - cached.Fetched < cache_duration)
                return cached.Account;

            MessengerAccount? account = null;

            try
            {
                using (var request = new JsonWebRequest<MessengerAccount>($@"{MSN_URL}/reprise-api/users/{userId}"))
                {
                    request.Timeout = 5000;
                    await request.PerformAsync(cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(request.ResponseObject?.Address))
                        account = request.ResponseObject;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Not shown (404), or the MSN site isn't answering: nothing to show.
            }

            cache[userId] = (DateTimeOffset.Now, account);
            return account;
        }
    }

    /// <summary>Reprise: a player's MSN Messenger account as the MSN site shows it.</summary>
    public class MessengerAccount
    {
        [JsonProperty(@"address")]
        public string Address { get; set; } = string.Empty;

        [JsonProperty(@"online")]
        public bool Online { get; set; }
    }
}

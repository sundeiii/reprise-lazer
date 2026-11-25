// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Users;
using Realms;

namespace osu.Game.Scoring
{
    /// <summary>
    /// Reprise: a local score keeps the player's name (and country) from when it was set, so after a name change
    /// the player's older plays and replays still showed the old one. Once logged in, the player's own local
    /// scores (by their user id) take the name and country they have now.
    /// </summary>
    public partial class LocalScoreUsernameUpdater : Component
    {
        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            localUser.BindTo(api.LocalUser);
            localUser.BindValueChanged(u =>
            {
                var user = u.NewValue;
                if (user == null || user.Id <= 1 || string.IsNullOrEmpty(user.Username))
                    return;

                int id = user.Id;
                string username = user.Username;
                var country = user.CountryCode;

                realm.WriteAsync(r => Update(r, id, username, country)).ContinueWith(t =>
                {
                    if (t.Exception != null)
                        Logger.Error(t.Exception, "Couldn't update the player's name on their local scores.");
                    else if (t.GetResultSafely() is int changed and > 0)
                        Logger.Log($"{changed} local score(s) now show {username}.");
                });
            }, true);
        }

        /// <summary>Gives <paramref name="userId"/>'s local scores this name (and country, when known). Call inside a write. How many changed.</summary>
        public static int Update(Realm r, int userId, string username, CountryCode country)
        {
            if (userId <= 1 || string.IsNullOrEmpty(username))
                return 0;

            string user = "User";
            var stale = (country == CountryCode.Unknown
                    ? r.All<ScoreInfo>().Filter($"{user}.{nameof(RealmUser.OnlineID)} == $0 && {user}.{nameof(RealmUser.Username)} != $1", userId, username)
                    : r.All<ScoreInfo>().Filter($"{user}.{nameof(RealmUser.OnlineID)} == $0 && ({user}.{nameof(RealmUser.Username)} != $1 || {user}.CountryCode != $2)",
                        userId, username, country.ToString()))
                .ToList();

            foreach (var score in stale)
            {
                score.RealmUser.Username = username;
                if (country != CountryCode.Unknown)
                    score.RealmUser.CountryCode = country;
            }

            return stale.Count;
        }
    }
}

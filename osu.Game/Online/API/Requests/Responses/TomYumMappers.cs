// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Responses
{
    /// <summary>
    /// TomYum: who made a set imported from osu! and each of its difficulties, as osu! users (they have no account on the server).
    /// </summary>
    public class TomYumMappers
    {
        [JsonProperty(@"creator")]
        public OsuProfile? Creator { get; set; }

        /// <summary>
        /// By beatmap id.
        /// </summary>
        [JsonProperty(@"beatmaps")]
        public Dictionary<int, OsuProfile[]> Beatmaps { get; set; } = new Dictionary<int, OsuProfile[]>();
    }

    /// <summary>
    /// An osu! user (on osu.ppy.sh, not this server): their avatar and profile are osu!'s.
    /// </summary>
    public class OsuProfile
    {
        [JsonProperty(@"id")]
        public int Id { get; set; }

        [JsonProperty(@"username")]
        public string Username { get; set; } = string.Empty;

        public string ProfileUrl => $@"https://osu.ppy.sh/users/{Id}";

        public string AvatarUrl => $@"https://a.ppy.sh/{Id}";
    }

    /// <summary>
    /// Someone credited on a set, as the website shows them: a user of this server, an osu! user (imported sets), or only a name.
    /// </summary>
    public class SetPerson
    {
        public readonly string Username;

        /// <summary>
        /// A user of this server.
        /// </summary>
        public readonly APIUser? User;

        /// <summary>
        /// An osu! user (no account on this server).
        /// </summary>
        public readonly OsuProfile? Osu;

        private SetPerson(string username, APIUser? user, OsuProfile? osu)
        {
            Username = username;
            User = user;
            Osu = osu;
        }

        public static SetPerson Local(APIUser user) => new SetPerson(user.Username, user, null);

        public static SetPerson FromOsu(OsuProfile profile) => new SetPerson(profile.Username, null, profile);

        public static SetPerson NameOnly(string name) => new SetPerson(name, null, null);

        /// <summary>
        /// The set's creator: its user here, else its osu! creator (imported sets), else its creator's name.
        /// </summary>
        public static SetPerson CreatorOf(APIBeatmapSet set)
        {
            if (set.Author.OnlineID > 1)
                return Local(set.Author);

            if (set.OsuMappers?.Creator is OsuProfile creator)
                return FromOsu(creator);

            return set.Author.OnlineID > 0 ? Local(set.Author) : NameOnly(set.Author.Username);
        }

        /// <summary>
        /// A difficulty's mappers: its owners here, else its osu! mappers, else the set's creator.
        /// </summary>
        public static SetPerson[] MappersOf(APIBeatmapSet set, APIBeatmap beatmap)
        {
            var owners = beatmap.BeatmapOwners?
                                .Select(o => set.RelatedUsers?.FirstOrDefault(u => u.OnlineID == o.Id && u.OnlineID > 1))
                                .OfType<APIUser>()
                                .Select(Local)
                                .ToArray();

            if (owners?.Length > 0)
                return owners;

            if (set.OsuMappers?.Beatmaps.TryGetValue(beatmap.OnlineID, out var osu) == true && osu.Length > 0)
                return osu.Select(FromOsu).ToArray();

            return new[] { CreatorOf(set) };
        }

        /// <summary>
        /// The set's nominators: users here, else its osu! nominators (imported sets).
        /// </summary>
        public static SetPerson[] NominatorsOf(APIBeatmapSet set)
        {
            var local = set.CurrentNominations?
                           .Select(n => set.RelatedUsers?.FirstOrDefault(u => u.OnlineID == n.UserId))
                           .OfType<APIUser>()
                           .Select(Local)
                           .ToArray() ?? Array.Empty<SetPerson>();

            if (local.Length > 0)
                return local;

            return set.OsuNominators?.Select(FromOsu).ToArray() ?? Array.Empty<SetPerson>();
        }
    }
}

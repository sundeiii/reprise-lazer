// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Users.Drawables;
using osuTK;

namespace osu.Game.Overlays.BeatmapSet
{
    /// <summary>
    /// People credited on a set, as the website shows them: users here open their profile in the game, osu! users (imported sets) open
    /// theirs on osu! with osu!'s avatar, names alone are plain text.
    /// </summary>
    public static class SetPersonDisplay
    {
        /// <summary>
        /// The person's avatar (none for a name alone).
        /// </summary>
        public static Drawable Avatar(SetPerson person, float size, float cornerRadius = 4)
        {
            if (person.User != null)
            {
                return new UpdateableAvatar(person.User, showUserPanelOnHover: true)
                {
                    Size = new Vector2(size),
                    Masking = true,
                    CornerRadius = cornerRadius,
                };
            }

            if (person.Osu != null)
            {
                return new OsuAvatar(person.Osu)
                {
                    Size = new Vector2(size),
                    Masking = true,
                    CornerRadius = cornerRadius,
                };
            }

            return Drawable.Empty();
        }

        /// <summary>
        /// Adds the person's name to <paramref name="flow"/>: a link to their profile here or on osu!.
        /// </summary>
        public static void AddName(LinkFlowContainer flow, SetPerson person, Action<SpriteText>? creationParameters = null)
        {
            if (person.User != null)
                flow.AddUserLink(person.User, creationParameters);
            else if (person.Osu != null)
                flow.AddLink(person.Username, LinkAction.External, person.Osu.ProfileUrl, person.Osu.ProfileUrl, creationParameters);
            else
                flow.AddText(person.Username, creationParameters);
        }

        // An osu! user's avatar from osu!, opening their osu! profile.
        private partial class OsuAvatar : OsuClickableContainer
        {
            private readonly OsuProfile profile;

            [Resolved]
            private OsuGame? game { get; set; }

            public OsuAvatar(OsuProfile profile)
            {
                this.profile = profile;

                TooltipText = LocalisableString.Interpolate($"{profile.Username} (osu!)");
                Child = new UpdateableAvatar(new APIUser { Username = profile.Username, AvatarUrl = profile.AvatarUrl }, isInteractive: false)
                {
                    RelativeSizeAxes = Axes.Both,
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Action = () => game?.OpenUrlExternally(profile.ProfileUrl);
            }
        }
    }
}

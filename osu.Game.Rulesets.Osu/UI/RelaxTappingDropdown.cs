// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Reprise: "Perfect" (Relax's own tapping) and the players there are tapping profiles of. Each row shows the player as osu! does
    /// (their avatar and flag) with how they tap and their unstable rate; the chosen one reads the same when closed.
    /// </summary>
    public partial class RelaxTappingDropdown : FormDropdown<string>
    {
        private static readonly Dictionary<string, RelaxTappingProfile?> profiles = new Dictionary<string, RelaxTappingProfile?>();

        private static RelaxTappingProfile? profileOf(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            lock (profiles)
            {
                if (!profiles.TryGetValue(name, out var profile))
                    profiles[name] = profile = RelaxTappingProfile.Load(name);
                return profile;
            }
        }

        /// <summary>"single-taps jumps · UR 71", or Relax's own.</summary>
        private static string detailOf(string name) => profileOf(name) is RelaxTappingProfile p
            ? $"{p.Style} · UR {Math.Round(p.Summary?.UnstableRate ?? 0).ToString(CultureInfo.InvariantCulture)}"
            : "Relax's own · UR 0";

        public RelaxTappingDropdown()
        {
            Items = new[] { string.Empty }.Concat(RelaxTappingProfile.Available).ToArray();
        }

        protected override LocalisableString GenerateItemText(string item) => string.IsNullOrEmpty(item)
            ? "Perfect (Relax's own)"
            : $"{item}  ·  {detailOf(item)}";

        protected override DropdownMenu CreateMenu() => new PlayerMenu { MaxHeight = MaxHeight };

        private partial class PlayerMenu : FormDropdownMenu
        {
            protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(MenuItem item) => new PlayerItem(item)
            {
                BackgroundColourHover = HoverColour,
                BackgroundColourSelected = SelectionColour,
            };
        }

        private partial class PlayerItem : OsuDropdownMenu.DrawableOsuDropdownMenuItem
        {
            public PlayerItem(MenuItem item)
                : base(item)
            {
            }

            protected override Drawable CreateContent() => new PlayerRow(Item is DropdownMenuItem<string> { Value: string name } ? name : string.Empty);
        }

        /// <summary>A row: avatar, flag, name, and a pill with how they tap.</summary>
        private partial class PlayerRow : CompositeDrawable, IHasText
        {
            private readonly string name;

            // The menu sets each row's text (the closed dropdown's); a row draws its own.
            public LocalisableString Text { get; set; }

            public PlayerRow(string name)
            {
                this.name = name;

                RelativeSizeAxes = Axes.X;
                Height = 30;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                var profile = profileOf(name);

                Drawable picture = profile?.UserId is int userId
                    ? new DelayedLoadWrapper(() => new DrawableAvatar(new APIUser { Id = userId, Username = name, AvatarUrl = $@"https://a.ppy.sh/{userId}" })
                    {
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Fill,
                    }, 0)
                    {
                        RelativeSizeAxes = Axes.Both,
                    }
                    : new SpriteIcon
                    {
                        Icon = FontAwesome.Solid.Bolt,
                        Size = new Vector2(12),
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Colour = colourProvider.Content2,
                    };

                var nameRow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 34,
                };

                if (profile?.Country is string country && Enum.TryParse(country, true, out CountryCode code))
                {
                    nameRow.Add(new DrawableFlag(code)
                    {
                        Size = new Vector2(20, 14),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    });
                }

                nameRow.Add(new OsuSpriteText
                {
                    Text = profile == null ? "Perfect" : name,
                    Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                });

                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        Size = new Vector2(24),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 4,
                        Masking = true,
                        CornerRadius = 6,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Background3,
                            },
                            picture,
                        },
                    },
                    nameRow,
                    new CircularContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -4,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Background4,
                            },
                            new OsuSpriteText
                            {
                                Text = detailOf(name),
                                Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                                Colour = colourProvider.Content2,
                                Margin = new MarginPadding { Horizontal = 8, Vertical = 3 },
                            },
                        },
                    },
                };
            }
        }
    }
}

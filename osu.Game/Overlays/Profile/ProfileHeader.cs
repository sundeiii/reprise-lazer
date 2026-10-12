// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Overlays.Profile.Header;
using osu.Game.Overlays.Profile.Header.Components;
using osu.Game.Resources.Localisation.Web;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osuTK.Graphics;
using osuTK;

namespace osu.Game.Overlays.Profile
{
    public partial class ProfileHeader : TabControlOverlayHeader<LocalisableString>
    {
        public Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

        private CentreHeaderContainer centreHeaderContainer;
        private DetailHeaderContainer detailHeaderContainer;

        private TopHeaderContainer topHeaderContainer = null!;

        public ProfileHeader()
        {
            ContentSidePadding = WaveOverlayContainer.HORIZONTAL_PADDING;

            TabControl.AddItem(LayoutStrings.HeaderUsersShow);

            // todo: pending implementation.
            // TabControl.AddItem(LayoutStrings.HeaderUsersModding);

            // Haphazardly guaranteed by OverlayHeader constructor (see CreateBackground / CreateContent).
            Debug.Assert(centreHeaderContainer != null);
            Debug.Assert(detailHeaderContainer != null);
        }

        protected override Drawable CreateBackground() => Empty();

        protected override Drawable CreateContent() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Children = new Drawable[]
            {
                topHeaderContainer = new TopHeaderContainer
                {
                    RelativeSizeAxes = Axes.X,
                    User = { BindTarget = User },
                },
                new BannerHeaderContainer
                {
                    User = { BindTarget = User },
                },
                new BadgeHeaderContainer
                {
                    RelativeSizeAxes = Axes.X,
                    User = { BindTarget = User },
                },
                new RestrictedHeaderContainer
                {
                    RelativeSizeAxes = Axes.X,
                    User = { BindTarget = User },
                },
                detailHeaderContainer = new DetailHeaderContainer
                {
                    RelativeSizeAxes = Axes.X,
                    User = { BindTarget = User },
                },
                new ProfileProcessingNotice(),
                centreHeaderContainer = new CentreHeaderContainer
                {
                    RelativeSizeAxes = Axes.X,
                    User = { BindTarget = User },
                },
                new BottomHeaderContainer
                {
                    RelativeSizeAxes = Axes.X,
                    User = { BindTarget = User },
                },
            }
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // This is basically a tooltip display on hover, so we should display above everything.
            // If this ever breaks let's just trash the design and make it a standard tooltip.
            AddInternal(topHeaderContainer.PreviousUsernamesDisplay.CreateProxy());
        }

        protected override OverlayTitle CreateTitle() => new ProfileHeaderTitle();

        protected override Drawable CreateTabControlContent() => new ProfileRulesetSelector
        {
            User = { BindTarget = User }
        };

        private partial class RestrictedHeaderContainer : CompositeDrawable
        {
            public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

            private readonly OsuSpriteText title;
            private readonly OsuSpriteText description;

            public RestrictedHeaderContainer()
            {
                RelativeSizeAxes = Axes.X;
                Height = 0;
                Alpha = 0;
                AlwaysPresent = true;
                Padding = new MarginPadding
                {
                    Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING,
                    Top = 10,
                    Bottom = 8,
                };

                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 6,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(42, 22, 30, 235),
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 5,
                            Colour = new Color4(235, 72, 112, 255),
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 4),
                            Padding = new MarginPadding
                            {
                                Left = 18,
                                Right = 18,
                                Top = 10,
                                Bottom = 10,
                            },
                            Children = new Drawable[]
                            {
                                title = new OsuSpriteText
                                {
                                    Text = "This account is restricted",
                                    Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                                    Colour = Color4.White,
                                },
                                description = new OsuSpriteText
                                {
                                    Text = "",
                                    Font = OsuFont.GetFont(size: 14),
                                    Colour = new Color4(255, 215, 225, 255),
                                },
                            },
                        },
                    },
                };

                User.BindValueChanged(user => updateDisplay(user.NewValue?.User), true);
            }

            private void updateDisplay(APIUser? user)
            {
                if (user?.IsRestricted != true)
                {
                    this.ResizeHeightTo(0, 180, Easing.OutQuint);
                    this.FadeOut(120, Easing.OutQuint);
                    return;
                }

                string reason = string.IsNullOrWhiteSpace(user.RestrictionReason)
                    ? "No reason provided"
                    : user.RestrictionReason;

                string until = user.RestrictionPermanent || user.RestrictionUntil == null
                    ? "Permanent"
                    : user.RestrictionUntil.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

                description.Text = $"Scores from this account do not affect rankings, pp, leaderboards, or medals. Reason: {reason}. Restriction ends: {until}.";

                this.ResizeHeightTo(76, 220, Easing.OutQuint);
                this.FadeIn(220, Easing.OutQuint);
            }
        }

        private partial class ProfileHeaderTitle : OverlayTitle
        {
            public ProfileHeaderTitle()
            {
                Title = PageTitleStrings.MainUsersControllerDefault;
                Icon = OsuIcon.Player;
            }
        }
    }
}

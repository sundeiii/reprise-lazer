// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Extensions;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Sections;
using osu.Game.Rulesets;
using osu.Game.Users;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public partial class UserProfileOverlay : FullscreenOverlay<ProfileHeader>
    {
        protected override Container<Drawable> Content => onlineViewContainer;

        private readonly OnlineViewContainer onlineViewContainer;
        private readonly LoadingLayer loadingLayer;

        private ProfileSection? lastSection;
        private ProfileSection[]? sections;
        private GetUserRequest? userReq;
        private ProfileSectionsContainer? sectionsContainer;
        private ProfileSectionTabControl? tabs;

        private IUser? user;
        private IRulesetInfo? ruleset;

        private int currentProfileHue = OverlayColourScheme.Pink.GetHue();

        private readonly IBindable<APIState> apiState = new Bindable<APIState>();

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        public UserProfileOverlay()
            : base(OverlayColourScheme.Pink)
        {
            base.Content.Add(new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    onlineViewContainer = new OnlineViewContainer($"Sign in to view the {Header.Title.Title}")
                    {
                        RelativeSizeAxes = Axes.Both
                    },
                    loadingLayer = new LoadingLayer(true)
                }
            });
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            apiState.BindTo(API.State);
            apiState.BindValueChanged(state => Schedule(() =>
            {
                if (state.NewValue == APIState.Online && user != null)
                    Scheduler.AddOnce(fetchAndSetContent);
            }));

            config.GetBindable<bool>(OsuSetting.CustomUIHueEnabled).BindValueChanged(_ => applyCurrentHue());
            config.GetBindable<float>(OsuSetting.CustomUIHue).BindValueChanged(_ => applyCurrentHue());
            config.GetBindable<bool>(OsuSetting.CustomUIHueApplyToOverlays).BindValueChanged(_ => applyCurrentHue());
        }

        private void applyCurrentHue()
        {
            Schedule(() => changeOverlayColours(currentProfileHue));
        }

        protected override ProfileHeader CreateHeader() => new ProfileHeader();

        protected override Color4 BackgroundColour => ColourProvider.Background5;

        public void ShowUser(IUser userToShow, IRulesetInfo? userRuleset = null)
        {
            if (userToShow.OnlineID == APIUser.SYSTEM_USER_ID)
                return;

            user = userToShow;
            ruleset = userRuleset;

            Show();
            Scheduler.AddOnce(fetchAndSetContent);
        }

        private void fetchAndSetContent()
        {
            Debug.Assert(user != null);

            bool sameUser = user.OnlineID == Header.User.Value?.User.Id;
            if (sameUser && ruleset?.MatchesOnlineID(Header.User.Value?.Ruleset) == true)
                return;

            if (sectionsContainer != null)
                sectionsContainer.ExpandableHeader = null;

            userReq?.Cancel();
            lastSection = null;

            sections = !user.IsBot
                ? new ProfileSection[]
                {
                    //new AboutSection(),
                    new RecentSection(),
                    new RanksSection(),
                    //new MedalsSection(),
                    new HistoricalSection(),
                    new BeatmapsSection(),
                    new KudosuSection()
                }
                : Array.Empty<ProfileSection>();

            if (!sameUser)
            {
                currentProfileHue = OverlayColourScheme.Pink.GetHue();
                changeOverlayColours(currentProfileHue);
            }

            recreateBaseContent();

            if (API.State.Value != APIState.Offline)
            {
                var requestedUser = user;
                var requestedRuleset = ruleset;

                userReq = user.OnlineID > 1
                    ? new GetUserRequest(user.OnlineID, ruleset)
                    : new GetUserRequest(user.Username, ruleset);

                userReq.Success += loadedUser =>
                {
                    Schedule(() =>
                    {
                        if (user != requestedUser || ruleset != requestedRuleset)
                            return;

                        if (loadedUser == null || loadedUser.Id <= 0)
                        {
                            userLoadFailed(requestedUser, requestedRuleset);
                            return;
                        }

                        userLoadComplete(loadedUser, requestedRuleset);
                    });
                };

                userReq.Failure += _ =>
                {
                    Schedule(() =>
                    {
                        if (user != requestedUser || ruleset != requestedRuleset)
                            return;

                        userLoadFailed(requestedUser, requestedRuleset);
                    });
                };

                API.Queue(userReq);
                loadingLayer.Show();
            }
            else
            {
                showUserLookupError(
                    "You are offline",
                    "This profile cannot be loaded while the client is offline.",
                    user,
                    ruleset
                );
            }
        }

        private void userLoadComplete(APIUser loadedUser, IRulesetInfo? userRuleset)
        {
            Debug.Assert(sections != null && sectionsContainer != null && tabs != null);

            currentProfileHue = loadedUser.ProfileHue ?? OverlayColourScheme.Pink.GetHue();

            if (changeOverlayColours(currentProfileHue))
                recreateBaseContent();

            RulesetInfo? actualRuleset = rulesets.GetRuleset(userRuleset?.ShortName ?? loadedUser.PlayMode);

            switch (actualRuleset)
            {
                case null when userRuleset != null && userRuleset.IsSpecialRuleset():
                    actualRuleset = rulesets.GetRuleset(userRuleset.ShortName[..^2]).AsNonNull().CreateSpecialRuleset(userRuleset.ShortName, userRuleset.OnlineID);
                    break;

                case null:
                    actualRuleset = rulesets.GetRuleset(loadedUser.PlayMode).AsNonNull();
                    break;
            }

            var userProfile = new UserProfileData(loadedUser, actualRuleset);
            Header.User.Value = userProfile;

            if (loadedUser.ProfileOrder != null)
            {
                foreach (string id in loadedUser.ProfileOrder)
                {
                    var sec = sections.FirstOrDefault(s => s.Identifier == id);

                    if (sec != null)
                    {
                        sec.User.Value = userProfile;

                        sectionsContainer.Add(sec);
                        tabs.AddItem(sec);
                    }
                }
            }

            loadingLayer.Hide();
        }

        private void userLoadFailed(IUser requestedUser, IRulesetInfo? requestedRuleset)
        {
            showUserLookupError(
                "This user could not be found",
                "The profile may have been deleted, restricted, renamed, or it may not exist on this server yet.",
                requestedUser,
                requestedRuleset
            );
        }

        private void showUserLookupError(string title, string description, IUser requestedUser, IRulesetInfo? requestedRuleset)
        {
            loadingLayer.Hide();

            if (sectionsContainer != null)
                sectionsContainer.ExpandableHeader = null;

            string lookupText = requestedUser.OnlineID > 1
                ? $"user id: {requestedUser.OnlineID}"
                : $"username: {requestedUser.Username}";

            string rulesetText = requestedRuleset != null
                ? $"ruleset: {requestedRuleset.Name}"
                : "ruleset: default";

            Child = new OsuContextMenuContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new UserLookupErrorContainer
                {
                    Title = { Value = title },
                    Description = { Value = description },
                    LookupText = { Value = lookupText },
                    RulesetText = { Value = rulesetText }
                }
            };
        }

        private void recreateBaseContent()
        {
            Child = new OsuContextMenuContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = sectionsContainer = new ProfileSectionsContainer
                {
                    ExpandableHeader = Header,
                    FixedHeader = tabs = new ProfileSectionTabControl
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                    },
                    HeaderBackground = new Box
                    {
                        // this is only visible as the ProfileTabControl background
                        Colour = ColourProvider.Background5,
                        RelativeSizeAxes = Axes.Both
                    },
                }
            };

            sectionsContainer.SelectedSection.ValueChanged += section =>
            {
                if (lastSection != section.NewValue)
                {
                    lastSection = section.NewValue;
                    tabs.Current.Value = lastSection!;
                }
            };

            tabs.Current.ValueChanged += section =>
            {
                if (lastSection == null)
                {
                    lastSection = sectionsContainer.Children.FirstOrDefault();
                    if (lastSection != null)
                        tabs.Current.Value = lastSection;
                    return;
                }

                if (lastSection != section.NewValue)
                {
                    lastSection = section.NewValue;
                    sectionsContainer.ScrollTo(lastSection);
                }
            };
        }

        private bool changeOverlayColours(int hue)
        {
            int resolvedHue = CustomUiHueHelper.ResolveHue(
                config,
                hue,
                CustomUiHueScope.Overlays);

            if (resolvedHue == ColourProvider.Hue)
                return false;

            ColourProvider.ChangeColourScheme(resolvedHue);

            RecreateHeader();
            UpdateColours();
            return true;
        }

        private partial class UserLookupErrorContainer : CompositeDrawable
        {
            public readonly Bindable<string> Title = new Bindable<string>();
            public readonly Bindable<string> Description = new Bindable<string>();
            public readonly Bindable<string> LookupText = new Bindable<string>();
            public readonly Bindable<string> RulesetText = new Bindable<string>();

            private OsuSpriteText titleText = null!;
            private OsuSpriteText descriptionText = null!;
            private OsuSpriteText lookupText = null!;
            private OsuSpriteText rulesetText = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public UserLookupErrorContainer()
            {
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding
                    {
                        Horizontal = 70,
                        Vertical = 55
                    },
                    Children = new Drawable[]
                    {
                        // Exact profile overlay background colour.
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background5
                        },

                        new Container
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.X,
                            Height = 285,
                            Masking = true,
                            CornerRadius = 20,
                            EdgeEffect = new EdgeEffectParameters
                            {
                                Type = EdgeEffectType.Shadow,
                                Colour = new Color4(0, 0, 0, 110),
                                Radius = 18,
                                Offset = new Vector2(0, 4),
                            },
                            Children = new Drawable[]
                            {
                                // Card background, still theme-native.
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = colourProvider.Background3
                                },

                                // Soft pink tint so it doesn't look too plain.
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = new Color4(255, 130, 195, 18)
                                },

                                new Box
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    RelativeSizeAxes = Axes.X,
                                    Height = 5,
                                    Colour = colourProvider.Highlight1,
                                    Alpha = 0.85f
                                },

                                new FillFlowContainer
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 13),
                                    Children = new Drawable[]
                                    {
                                        new Container
                                        {
                                            Anchor = Anchor.TopCentre,
                                            Origin = Anchor.TopCentre,
                                            Size = new Vector2(72),
                                            Masking = true,
                                            CornerRadius = 36,
                                            Children = new Drawable[]
                                            {
                                                new Box
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Colour = colourProvider.Background2
                                                },
                                                new SpriteIcon
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Icon = FontAwesome.Solid.UserSlash,
                                                    Size = new Vector2(30),
                                                    Colour = colourProvider.Highlight1
                                                }
                                            }
                                        },

                                        titleText = new OsuSpriteText
                                        {
                                            Anchor = Anchor.TopCentre,
                                            Origin = Anchor.TopCentre,
                                            Font = OsuFont.Default.With(size: 31, weight: FontWeight.SemiBold),
                                            Colour = Color4.White,
                                            Text = string.Empty
                                        },

                                        descriptionText = new OsuSpriteText
                                        {
                                            Anchor = Anchor.TopCentre,
                                            Origin = Anchor.TopCentre,
                                            Font = OsuFont.Default.With(size: 18),
                                            Colour = colourProvider.Light2,
                                            Text = string.Empty
                                        },

                                        new FillFlowContainer
                                        {
                                            Anchor = Anchor.TopCentre,
                                            Origin = Anchor.TopCentre,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(10, 0),
                                            Children = new Drawable[]
                                            {
                                                lookupText = new OsuSpriteText
                                                {
                                                    Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold),
                                                    Colour = colourProvider.Highlight1,
                                                    Text = string.Empty
                                                },
                                                rulesetText = new OsuSpriteText
                                                {
                                                    Font = OsuFont.Default.With(size: 15),
                                                    Colour = colourProvider.Light4,
                                                    Text = string.Empty
                                                }
                                            }
                                        },

                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.TopCentre,
                                            Origin = Anchor.TopCentre,
                                            Font = OsuFont.Default.With(size: 14),
                                            Colour = colourProvider.Light4,
                                            Alpha = 0.65f,
                                            Text = "Try opening another profile, checking the username, or syncing users from bancho."
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                Title.BindValueChanged(v => titleText.Text = v.NewValue ?? string.Empty, true);
                Description.BindValueChanged(v => descriptionText.Text = v.NewValue ?? string.Empty, true);
                LookupText.BindValueChanged(v => lookupText.Text = v.NewValue ?? string.Empty, true);
                RulesetText.BindValueChanged(v => rulesetText.Text = v.NewValue ?? string.Empty, true);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                this.FadeInFromZero(250, Easing.OutQuint);
                this.MoveToY(0, 250, Easing.OutQuint);
            }
        }

        private partial class ProfileSectionTabControl : OsuTabControl<ProfileSection>
        {
            public ProfileSectionTabControl()
            {
                Height = 40;
                Padding = new MarginPadding { Horizontal = HORIZONTAL_PADDING };
                TabContainer.Spacing = new Vector2(20);
            }

            protected override TabItem<ProfileSection> CreateTabItem(ProfileSection value) => new ProfileSectionTabItem(value);

            protected override bool OnClick(ClickEvent e) => true;

            protected override bool OnHover(HoverEvent e) => true;

            private partial class ProfileSectionTabItem : TabItem<ProfileSection>
            {
                private OsuSpriteText text = null!;

                [Resolved]
                private OverlayColourProvider colourProvider { get; set; } = null!;

                public ProfileSectionTabItem(ProfileSection value)
                    : base(value)
                {
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    AutoSizeAxes = Axes.Both;
                    Anchor = Anchor.CentreLeft;
                    Origin = Anchor.CentreLeft;

                    InternalChild = text = new OsuSpriteText
                    {
                        Text = Value.Title
                    };

                    updateState();
                }

                protected override void OnActivated() => updateState();

                protected override void OnDeactivated() => updateState();

                protected override bool OnHover(HoverEvent e)
                {
                    updateState();
                    return true;
                }

                protected override void OnHoverLost(HoverLostEvent e) => updateState();

                private void updateState()
                {
                    text.Font = OsuFont.Default.With(size: 14, weight: Active.Value ? FontWeight.SemiBold : FontWeight.Regular);

                    Colour4 textColour;

                    if (IsHovered)
                        textColour = colourProvider.Light1;
                    else
                        textColour = Active.Value ? colourProvider.Content1 : colourProvider.Light2;

                    text.FadeColour(textColour, 300, Easing.OutQuint);
                }
            }
        }

        private partial class ProfileSectionsContainer : SectionsContainer<ProfileSection>
        {
            private OverlayScrollContainer scroll = null!;

            public ProfileSectionsContainer()
            {
                RelativeSizeAxes = Axes.Both;
            }

            protected override UserTrackingScrollContainer CreateScrollContainer() => scroll = new OverlayScrollContainer();

            // Reverse child ID is required so expanding beatmap panels can appear above sections below them.
            // This can also be done by setting Depth when adding new sections above if using ReverseChildID turns out to have any issues.
            protected override FlowContainer<ProfileSection> CreateScrollContentContainer() => new ReverseChildIDFillFlowContainer<ProfileSection>
            {
                Direction = FillDirection.Vertical,
                AutoSizeAxes = Axes.Y,
                RelativeSizeAxes = Axes.X,
                Spacing = new Vector2(0, 10),
                Padding = new MarginPadding { Horizontal = 10 },
                Margin = new MarginPadding { Bottom = 10 },
            };

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // Ensure the scroll-to-top button is displayed above the fixed header.
                AddInternal(scroll.Button.CreateProxy());
            }
        }
    }
}
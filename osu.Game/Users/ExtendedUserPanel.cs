// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Users.Drawables;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Metadata;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Users
{
    public abstract partial class ExtendedUserPanel : UserPanel
    {
        protected TextFlowContainer LastVisitMessage { get; private set; } = null!;

        private FillFlowContainer clientBadges = null!;
        private string lastClientBadgeKey = string.Empty;

        private StatusIcon statusIcon = null!;
        private StatusText statusMessage = null!;


        [Resolved]
        private MetadataClient? metadata { get; set; }

        private UserStatus? lastStatus;
        private UserActivity? lastActivity;
        private DateTimeOffset? lastVisit;

        protected ExtendedUserPanel(APIUser user)
            : base(user)
        {
            lastVisit = user.LastVisit;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            BorderColour = ColourProvider?.Light1 ?? Colours.GreyVioletLighter;

            AddInternal(clientBadges = new FillFlowContainer
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5, 0),
                Margin = new MarginPadding
                {
                    Top = 10,
                    Right = 10
                },
                Depth = -10,
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updatePresence();

            // Colour should be applied immediately on first load.
            statusIcon.FinishTransforms();
        }

        protected override void Update()
        {
            base.Update();
            updatePresence();
        }

        protected Container CreateStatusIcon() => statusIcon = new StatusIcon();

        protected FillFlowContainer CreateStatusMessage(bool rightAlignedChildren)
        {
            var statusContainer = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical
            };

            var alignment = rightAlignedChildren ? Anchor.CentreRight : Anchor.CentreLeft;

            statusContainer.Add(LastVisitMessage = new TextFlowContainer(t => t.Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold)).With(text =>
            {
                text.Anchor = alignment;
                text.Origin = alignment;
                text.AutoSizeAxes = Axes.Both;
                text.Alpha = 0;
            }));

            statusContainer.Add(statusMessage = new StatusText
            {
                Anchor = alignment,
                Origin = alignment,
                Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold)
            });

            return statusContainer;
        }

        private void updatePresence()
        {
            // TODO: we probably don't want to do this every frame.
            UserPresence? presence = metadata?.GetPresence(User.OnlineID);

            UserStatus status = presence?.Status
                                ?? (User.WasRecentlyOnline ? UserStatus.Online : UserStatus.Offline);

            UserActivity? activity = presence?.Activity;

            updateClientBadges(presence);

            if (status == lastStatus && activity == lastActivity)
                return;

            if (status == UserStatus.Offline && lastVisit != null)
            {
                LastVisitMessage.FadeTo(1);
                LastVisitMessage.Clear();
                LastVisitMessage.AddText(@"Last seen ");
                LastVisitMessage.AddText(new DrawableDate(lastVisit.Value, italic: false)
                {
                    Shadow = false
                });
            }
            else
                LastVisitMessage.FadeTo(0);

            if (activity == null || status == UserStatus.Offline)
            {
                statusMessage.Text = status.GetLocalisableDescription();
                statusMessage.TooltipText = string.Empty;
            }
            else
            {
                statusMessage.Text = activity.GetStatus();
                statusMessage.TooltipText = activity.GetDetails() ?? string.Empty;
            }

            if (activity == null || status != UserStatus.Online)
                statusIcon.FadeColour(status.GetAppropriateColour(Colours), 500, Easing.OutQuint);
            else
                statusIcon.FadeColour(activity.GetAppropriateColour(Colours), 500, Easing.OutQuint);

            lastStatus = status;
            lastActivity = activity;
            lastVisit = status != UserStatus.Offline ? DateTimeOffset.Now : lastVisit;
        }

        protected override bool OnHover(HoverEvent e)
        {
            BorderThickness = 2;
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            BorderThickness = 0;
            base.OnHoverLost(e);
        }

        private static string getClientDisplayName(string client) => client switch
        {
            "stable" => "Rinari Stable",
            "lazer" => "Rinari Lazer",
            "web" => "Rinari Web",
            _ => client
        };

        private static string getClientShortName(string client) => client switch
        {
            "stable" => "stable",
            "lazer" => "lazer",
            "web" => "web",
            _ => client
        };

        private static string getClientTooltip(string client) => client switch
        {
            "stable" => "Playing on Rinari Stable",
            "lazer" => "Playing on Rinari Lazer",
            "web" => "Browsing Rinari Web",
            _ => $"Online via {client}"
        };

        private void updateClientBadges(UserPresence? presence)
        {
            string[] clients = presence != null
                ? new[] { "lazer" }
                : User.CurrentClients ?? Array.Empty<string>();

            clients = clients.Distinct().ToArray();

            string key = string.Join(",", clients);

            if (key == lastClientBadgeKey)
                return;

            lastClientBadgeKey = key;

            clientBadges.Clear();

            foreach (string client in clients)
                clientBadges.Add(new ClientBadge(client));
        }

        private partial class StatusText : OsuSpriteText, IHasTooltip
        {
            public LocalisableString TooltipText { get; set; }
        }

        private partial class ClientBadge : CircularContainer, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public ClientBadge(string client)
            {
                TooltipText = getClientTooltip(client);

                Size = new Vector2(getClientBadgeWidth(client), 24);
                Masking = true;
                CornerRadius = 7;

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = client switch
                        {
                            "stable" => Colour4.FromHex("#c52f55"),
                            "lazer" => Colour4.FromHex("#d9a21b"),
                            "web" => Colour4.FromHex("#4f8cff"),
                            _ => Colour4.FromHex("#555555")
                        }
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = getClientShortName(client),
                        Font = OsuFont.GetFont(size: 13, weight: FontWeight.Bold),
                        Colour = Colour4.White,
                    }
                };
            }

            private static float getClientBadgeWidth(string client) => client switch
            {
                "stable" => 72,
                "lazer" => 62,
                "web" => 50,
                _ => 70
            };

            protected override bool OnHover(osu.Framework.Input.Events.HoverEvent e) => true;
        }
    }
}

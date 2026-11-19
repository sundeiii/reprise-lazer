// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Users;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Profile.Header.Components
{
    /// <summary>
    /// Reprise: the profile's third column, as the website's (profile-detail-stats-card--extra, no card of its own): the pinned badge and
    /// the daily challenge as "big" blocks, then the statistics' box taking the rest of the height.
    /// </summary>
    public partial class BadgeStatsCard : ProfileStatsCard
    {
        private const float spacing = 5;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private FillFlowContainer blocks = null!;
        private Container statsBox = null!;
        private ExtendedDetails stats = null!;

        public override float ContentHeight => blocks.DrawHeight + (blocks.Any(b => b.IsPresent) ? spacing : 0) + stats.DrawHeight;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                blocks = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(spacing),
                },
                // profile-stats: b4, rounded, flex: 1 (the rest of the column).
                statsBox = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Masking = true,
                    CornerRadius = CORNER_RADIUS,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background4,
                        },
                        stats = new ExtendedDetails
                        {
                            ShowBackground = false,
                            User = { BindTarget = User },
                        },
                    }
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            bool anyBlock = blocks.Any(b => b.IsPresent);
            statsBox.Y = anyBlock ? blocks.DrawHeight + spacing : 0;
            statsBox.Height = Math.Max(stats.DrawHeight, DrawHeight - statsBox.Y);
        }

        /// <summary>The badge pinned to the profile, if any: the website names it by its image's filename (badges.find(b => b.image === image)).</summary>
        public static Badge? PinnedBadge(UserProfileData? data)
        {
            var user = data?.User;
            string? pinned = user?.ProfilePinnedBadge;

            return string.IsNullOrEmpty(pinned)
                ? null
                : user!.Badges?.FirstOrDefault(b => b.Image == pinned);
        }

        protected override void UpdateDisplay(UserProfileData? data)
        {
            blocks.Clear();

            if (PinnedBadge(data) is Badge badge)
            {
                // The badge's image loads in the background (a DrawableBadge can't be loaded with its parent).
                var holder = new Container { RelativeSizeAxes = Axes.Both };

                blocks.Add(new BigBlock(FontAwesome.Solid.Trophy, badge.Name ?? badge.Description, badge.AwardedAt.ToLocalisedMediumDate(), holder));
                LoadComponentAsync(new DrawableBadge(badge), b =>
                {
                    if (holder.Parent != null)
                        holder.Add(b);
                });
            }

            var daily = data?.User.DailyChallengeStatistics;

            if (daily != null && daily.PlayCount > 0)
            {
                bool playedToday = daily.LastUpdate?.Date == DateTimeOffset.UtcNow.Date && daily.UserID == api.LocalUser.Value.Id;

                blocks.Add(new BigBlock(OsuIcon.DailyChallenge, UsersStrings.ShowDailyChallengeTitle.ToString().Replace(@"\n", " "), UsersStrings.ShowDailyChallengePlaycount,
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = DailyChallengeStatsDisplayStrings.UnitDay(daily.PlayCount.ToLocalisableString("N0")),
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                        Colour = OsuColour.ForRankingTier(DailyChallengeStatsTooltip.TierForPlayCount(daily.PlayCount)),
                    }, playedToday ? colours.Lime1 : null)
                {
                    TooltipContent = new DailyChallengeTooltipData(colourProvider, daily),
                });
            }
        }

        /// <summary>
        /// The website's profile-detail-stats-big: an icon, a bold label over a second line, and an 86x40 box on the right (a badge, or the
        /// daily challenge's days; with a lime ring and tick when played today).
        /// </summary>
        private partial class BigBlock : CompositeDrawable, IHasCustomTooltip<DailyChallengeTooltipData>
        {
            private readonly IconUsage icon;
            private readonly LocalisableString label;
            private readonly LocalisableString subtitle;
            private readonly Drawable boxContent;
            private readonly Color4? ring;

            public DailyChallengeTooltipData? TooltipContent { get; init; }

            public ITooltip<DailyChallengeTooltipData> GetCustomTooltip() => new DailyChallengeStatsTooltip();

            public BigBlock(IconUsage icon, LocalisableString label, LocalisableString subtitle, Drawable boxContent, Color4? ring = null)
            {
                this.icon = icon;
                this.label = label;
                this.subtitle = subtitle;
                this.boxContent = boxContent;
                this.ring = ring;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = CORNER_RADIUS,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Colour = Color4.Black.Opacity(0.25f),
                        Offset = new Vector2(0, 2),
                        Radius = 4,
                    },
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background3,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0), Color4.Black.Opacity(0.4f)),
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding { Vertical = 10, Horizontal = 8 },
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(GridSizeMode.Absolute, 6),
                                new Dimension(),
                                new Dimension(GridSizeMode.Absolute, 4),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new SpriteIcon
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Size = new Vector2(20),
                                        Icon = icon,
                                    },
                                    Empty(),
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Children = new Drawable[]
                                        {
                                            new TruncatingSpriteText
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Text = label,
                                                Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                                                Colour = colourProvider.Content1,
                                                ShowTooltip = true,
                                            },
                                            new OsuSpriteText
                                            {
                                                Text = subtitle,
                                                Font = OsuFont.Default.With(size: 12),
                                                Colour = colourProvider.Content2,
                                            },
                                        }
                                    },
                                    Empty(),
                                    badgeBox(),
                                }
                            }
                        },
                    }
                };
            }

            // profile-detail-stats-big__badge: 86x40, rounded, a dark backdrop.
            private Drawable badgeBox()
            {
                var box = new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Size = DrawableBadge.DRAWABLE_BADGE_SIZE,
                    Masking = true,
                    CornerRadius = 4,
                    Children = new[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.25f),
                        },
                        boxContent,
                    }
                };

                if (ring == null)
                    return box;

                box.BorderThickness = 2;
                box.BorderColour = ring.Value;

                return new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        box,
                        new CircularContainer
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.Centre,
                            Size = new Vector2(16),
                            Masking = true,
                            Children = new Drawable[]
                            {
                                new Box { RelativeSizeAxes = Axes.Both, Colour = ring.Value },
                                new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = new Vector2(8),
                                    Icon = FontAwesome.Solid.Check,
                                    Colour = Color4.Black,
                                },
                            }
                        },
                    }
                };
            }
        }
    }
}

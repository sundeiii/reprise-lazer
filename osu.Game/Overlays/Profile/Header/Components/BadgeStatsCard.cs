// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Users;
using osuTK;

namespace osu.Game.Overlays.Profile.Header.Components
{
    /// <summary>
    /// Reprise: the profile's third card, as the website's: the badge the player pinned (what it's for and when, the badge itself), then
    /// their statistics.
    /// </summary>
    public partial class BadgeStatsCard : ProfileStatsCard
    {
        private Container badgeRow = null!;
        private OsuSpriteText badgeTitle = null!;
        private OsuSpriteText badgeDate = null!;
        private Container badgeHolder = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            Body.Children = new Drawable[]
            {
                badgeRow = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = DrawableBadge.DRAWABLE_BADGE_SIZE.Y,
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding { Right = DrawableBadge.DRAWABLE_BADGE_SIZE.X + 10 },
                            Direction = FillDirection.Vertical,
                            Children = new Drawable[]
                            {
                                badgeTitle = new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                                    ShowTooltip = true,
                                },
                                badgeDate = new OsuSpriteText
                                {
                                    Font = OsuFont.Default.With(size: 11),
                                    Colour = colourProvider.Content2,
                                },
                            }
                        },
                        badgeHolder = new Container
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Size = DrawableBadge.DRAWABLE_BADGE_SIZE,
                        },
                    }
                },
                new ExtendedDetails
                {
                    ShowBackground = false,
                    User = { BindTarget = User },
                },
            };
        }

        /// <summary>The badge pinned to the profile (the website matches it by its image), if any.</summary>
        public static Badge? PinnedBadge(UserProfileData? data)
        {
            var user = data?.User;
            string? pinned = user?.ProfilePinnedBadge;

            return string.IsNullOrEmpty(pinned)
                ? null
                : user!.Badges?.FirstOrDefault(b => b.ImageUrl == pinned || b.ImageUrlLowRes == pinned);
        }

        protected override void UpdateDisplay(UserProfileData? data)
        {
            var badge = PinnedBadge(data);

            badgeHolder.Clear();
            badgeRow.Alpha = badge == null ? 0 : 1;

            if (badge == null)
                return;

            badgeTitle.Text = badge.Description;
            badgeDate.Text = badge.AwardedAt.ToLocalTime().ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
            LoadComponentAsync(new DrawableBadge(badge), badgeHolder.Add);
        }
    }
}

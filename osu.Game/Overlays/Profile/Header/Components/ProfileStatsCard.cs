// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Overlays.Profile.Header.Components
{
    /// <summary>
    /// Reprise: one of the profile's statistics cards, as the website has them side by side (Solo Play, Ranked Play, and the pinned badge
    /// with the statistics): rounded, a title with its icon, then the card's own content. The cards are given the tallest one's height.
    /// </summary>
    public abstract partial class ProfileStatsCard : CompositeDrawable
    {
        private const float padding = 15;

        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

        private readonly IconUsage? icon;
        private readonly LocalisableString? title;

        private FillFlowContainer flow = null!;

        /// <summary>The card's content (under its title).</summary>
        protected FillFlowContainer Body { get; private set; } = null!;

        /// <summary>How tall the card's content wants it.</summary>
        public float ContentHeight => flow.DrawHeight;

        protected ProfileStatsCard(IconUsage? icon = null, LocalisableString? title = null)
        {
            this.icon = icon;
            this.title = title;

            RelativeSizeAxes = Axes.X;
            Masking = true;
            CornerRadius = 10;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4,
                },
                flow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12),
                    Padding = new MarginPadding(padding),
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8, 0),
                            Alpha = title == null ? 0 : 1,
                            Children = new Drawable[]
                            {
                                new SpriteIcon
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(16),
                                    Icon = icon ?? FontAwesome.Solid.User,
                                    Colour = colourProvider.Content2,
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = title ?? default,
                                    Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold),
                                },
                            }
                        },
                        Body = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 12),
                        },
                    }
                },
            };

            if (title == null)
                flow.Remove(flow.Children[0], true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            User.BindValueChanged(u => UpdateDisplay(u.NewValue), true);
        }

        /// <summary>Shows the given player's (or nothing, for none).</summary>
        protected abstract void UpdateDisplay(UserProfileData? data);
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Profile.Header.Components;

namespace osu.Game.Overlays.Profile.Header
{
    /// <summary>
    /// Reprise: the profile's statistics as the website has them now: three cards side by side, Solo Play, Ranked Play, and the pinned
    /// badge with the statistics, each as tall as the tallest.
    /// </summary>
    public partial class DetailHeaderContainer : CompositeDrawable
    {
        private const float spacing = 10;

        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

        private OverlayColourProvider colourProvider = null!;

        private Box background = null!;
        private ProfileStatsCard[] cards = null!;
        private GridContainer cardsContainer = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            this.colourProvider = colourProvider;
            colourProvider.ColoursChanged += updateColours;

            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;

            cards = new ProfileStatsCard[]
            {
                new SoloPlayCard { User = { BindTarget = User } },
                new RankedPlayCard { User = { BindTarget = User } },
                new BadgeStatsCard { User = { BindTarget = User } },
            };

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 10 },
                    Child = cardsContainer = new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, spacing),
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, spacing),
                            new Dimension(),
                        },
                        Content = new[] { new Drawable[] { cards[0], Empty(), cards[1], Empty(), cards[2] } },
                    },
                },
            };

            updateColours();
        }

        private void updateColours()
        {
            background.Colour = colourProvider.Background5;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            User.BindValueChanged(user => updateDisplay(user.NewValue?.User), true);
        }

        protected override void Update()
        {
            base.Update();

            // As tall as the tallest card's content.
            float height = cards.Where(c => c.IsPresent).Select(c => c.ContentHeight).DefaultIfEmpty(0).Max();

            foreach (var card in cards)
                card.Height = height;
        }

        private void updateDisplay(APIUser? user)
        {
            // A restricted player's ranked play and statistics aren't shown (as before: only the solo card's ranks).
            bool restricted = user?.IsRestricted == true;

            cards[1].FadeTo(restricted ? 0 : 1, 180, Easing.OutQuint);
            cards[2].FadeTo(restricted ? 0 : 1, 180, Easing.OutQuint);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (colourProvider != null)
                colourProvider.ColoursChanged -= updateColours;
        }
    }
}

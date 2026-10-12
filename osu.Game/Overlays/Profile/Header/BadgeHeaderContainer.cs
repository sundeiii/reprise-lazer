// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Profile.Header.Components;
using osuTK;

namespace osu.Game.Overlays.Profile.Header
{
    public partial class BadgeHeaderContainer : CompositeDrawable
    {
        private OverlayColourProvider colourProvider = null!;

        private Box background = null!;
        private FillFlowContainer badgeFlowContainer = null!;

        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

        private CancellationTokenSource? cancellationTokenSource;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            this.colourProvider = colourProvider;
            colourProvider.ColoursChanged += updateColours;

            Alpha = 0;
            AutoSizeAxes = Axes.Y;
            User.ValueChanged += e => updateDisplay(e.NewValue?.User);

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 3,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(Colour4.Black.Opacity(0.2f), Colour4.Black.Opacity(0))
                    }
                },
                badgeFlowContainer = new FillFlowContainer
                {
                    Direction = FillDirection.Full,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Spacing = new Vector2(10, 10),
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 10 },
                }
            };

            updateColours();
        }

        private void updateColours()
        {
            background.Colour = colourProvider.Background4;
        }

        private void updateDisplay(APIUser? user)
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();

            badgeFlowContainer.Clear();

            var badges = user?.Badges;

            if (badges?.Length > 0)
            {
                Show();

                for (int index = 0; index < badges.Length; index++)
                {
                    int displayIndex = index;
                    LoadComponentAsync(new DrawableBadge(badges[index]), asyncBadge =>
                    {
                        badgeFlowContainer.Insert(displayIndex, asyncBadge);
                    }, cancellationTokenSource.Token);
                }
            }
            else
            {
                Hide();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellationTokenSource?.Cancel();

            if (colourProvider != null)
                colourProvider.ColoursChanged -= updateColours;

            base.Dispose(isDisposing);
        }
    }
}
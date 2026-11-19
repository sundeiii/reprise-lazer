// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Profile.Header.Components;
using osuTK;

namespace osu.Game.Overlays.Profile.Header
{
    /// <summary>
    /// The player's badges. Reprise: in one row as the website has them now: how many in a box of their own, then the badges, scrolled
    /// sideways with the wheel or the arrows (a page at a time).
    /// </summary>
    public partial class BadgeHeaderContainer : CompositeDrawable
    {
        private const float row_padding = 10;

        private OverlayColourProvider colourProvider = null!;

        private Box background = null!;
        private Box countBackground = null!;
        private OsuSpriteText countText = null!;
        private OsuScrollContainer scroll = null!;
        private FillFlowContainer badgeFlowContainer = null!;
        private ArrowButton previous = null!;
        private ArrowButton next = null!;

        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

        private CancellationTokenSource? cancellationTokenSource;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            this.colourProvider = colourProvider;
            colourProvider.ColoursChanged += updateColours;

            Alpha = 0;
            RelativeSizeAxes = Axes.X;
            Height = DrawableBadge.DRAWABLE_BADGE_SIZE.Y + row_padding * 2;
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
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = row_padding },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.Absolute, 6),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            // "Badges 27", as the website's.
                            new Container
                            {
                                AutoSizeAxes = Axes.X,
                                RelativeSizeAxes = Axes.Y,
                                Masking = true,
                                CornerRadius = 6,
                                Children = new Drawable[]
                                {
                                    countBackground = new Box { RelativeSizeAxes = Axes.Both },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Vertical,
                                        Padding = new MarginPadding { Horizontal = 10 },
                                        Children = new Drawable[]
                                        {
                                            new OsuSpriteText
                                            {
                                                Text = "Badges",
                                                Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                                            },
                                            countText = new OsuSpriteText
                                            {
                                                Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                                                Colour = colourProvider.Highlight1,
                                            },
                                        }
                                    },
                                }
                            },
                            Empty(),
                            previous = new ArrowButton(FontAwesome.Solid.ChevronLeft) { Action = () => scrollBy(-1) },
                            scroll = new OsuScrollContainer(Direction.Horizontal)
                            {
                                RelativeSizeAxes = Axes.Both,
                                ScrollbarVisible = false,
                                Child = badgeFlowContainer = new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.X,
                                    RelativeSizeAxes = Axes.Y,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(6, 0),
                                },
                            },
                            next = new ArrowButton(FontAwesome.Solid.ChevronRight) { Action = () => scrollBy(1) },
                        }
                    }
                },
            };

            updateColours();
        }

        // A page (what's in view) at a time, as the website's arrows.
        private void scrollBy(int direction) => scroll.ScrollTo(scroll.Target + direction * scroll.DrawWidth);

        protected override void Update()
        {
            base.Update();

            // The arrows only where there's more that way.
            bool more = badgeFlowContainer.DrawWidth > scroll.DrawWidth + 1;
            previous.Alpha = more && scroll.Current > 1 ? 1 : 0;
            next.Alpha = more && scroll.Current < scroll.ScrollableExtent - 1 ? 1 : 0;
        }

        private void updateColours()
        {
            background.Colour = colourProvider.Background4;
            countBackground.Colour = colourProvider.Background5;
        }

        private void updateDisplay(APIUser? user)
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();

            badgeFlowContainer.Clear();
            scroll.ScrollToStart(false);

            var badges = user?.Badges;

            if (badges?.Length > 0)
            {
                Show();
                countText.Text = badges.Length.ToString();

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

        private partial class ArrowButton : OsuClickableContainer
        {
            private readonly IconUsage icon;

            public ArrowButton(IconUsage icon)
            {
                this.icon = icon;

                RelativeSizeAxes = Axes.Y;
                Width = 30;
                Alpha = 0;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(16),
                    Icon = icon,
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                this.FadeColour(Colour4.LightGray, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                this.FadeColour(Colour4.White, 100);
                base.OnHoverLost(e);
            }
        }
    }
}

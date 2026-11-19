// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Overlays.LazerImport
{
    /// <summary>
    /// Reprise: something from osu!lazer in a <see cref="LazerImportScreen"/>: what it is and its line below, ticked to import; one that's here
    /// already ticked off in green.
    /// </summary>
    public partial class LazerImportRow : OsuClickableContainer
    {
        public readonly string Title;
        public readonly bool AlreadyHere;

        /// <summary>Ticked (bound to its entry's, which outlives the row).</summary>
        public readonly BindableBool Selected = new BindableBool();

        private readonly string subtitle;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private Box background = null!;
        private Container tickBox = null!;
        private Box tickFill = null!;
        private SpriteIcon tick = null!;

        public LazerImportRow(string title, string subtitle, bool alreadyHere, BindableBool selected)
        {
            Title = title;
            this.subtitle = subtitle;
            AlreadyHere = alreadyHere;
            Selected.BindTo(selected);

            RelativeSizeAxes = Axes.X;
            Height = 50;
            Masking = true;
            CornerRadius = 8;
            Action = () => Selected.Toggle();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 15 },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.Absolute, 15),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable?[]
                        {
                            // A filled box (an outline alone isn't drawn), its border the outline.
                            tickBox = new Container
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Size = new Vector2(22),
                                Masking = true,
                                CornerRadius = 6,
                                BorderThickness = 2,
                                Children = new Drawable[]
                                {
                                    tickFill = new Box { RelativeSizeAxes = Axes.Both },
                                    tick = new SpriteIcon
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Size = new Vector2(12),
                                        Icon = FontAwesome.Solid.Check,
                                    },
                                }
                            },
                            null,
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
                                        Text = Title,
                                        Font = OsuFont.Default.With(size: 16, weight: FontWeight.SemiBold),
                                        ShowTooltip = true,
                                    },
                                    new TruncatingSpriteText
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Text = subtitle,
                                        Font = OsuFont.Default.With(size: 13),
                                        Colour = colourProvider.Content2,
                                    },
                                }
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Margin = new MarginPadding { Left = 10 },
                                Text = "here already",
                                Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                                Colour = colours.Green1,
                                Alpha = AlreadyHere ? 1 : 0,
                            },
                        }
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Selected.BindValueChanged(_ => updateState(), true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateState();
            base.OnHoverLost(e);
        }

        private void updateState()
        {
            bool selected = Selected.Value;

            background.FadeColour(selected ? colourProvider.Background3 : IsHovered ? colourProvider.Background4 : colourProvider.Background5, 150, Easing.OutQuint);

            // Ticked to import: filled in; here already (and not ticked): ticked off in green.
            tickBox.BorderColour = selected ? colourProvider.Highlight1 : AlreadyHere ? colours.Green1 : colourProvider.Light4;
            tickFill.FadeColour(selected ? colourProvider.Highlight1 : colourProvider.Background6, 150, Easing.OutQuint);
            tick.Colour = selected ? colourProvider.Background6 : colours.Green1;
            tick.FadeTo(selected || AlreadyHere ? 1 : 0, 150, Easing.OutQuint);
        }
    }
}

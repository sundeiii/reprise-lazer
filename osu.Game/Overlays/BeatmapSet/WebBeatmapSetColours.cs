// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.BeatmapSet
{
    /// <summary>
    /// The website's beatmap page colours (osu!'s "Beatmap Info" design proposal) beyond the overlay's own.
    /// </summary>
    public static class WebBeatmapSetColours
    {
        public static readonly Color4 LIME_1 = Color4Extensions.FromHex(@"b3ff66");
        public static readonly Color4 LIME_3 = Color4Extensions.FromHex(@"80cc33");
        public static readonly Color4 PINK_3 = Color4Extensions.FromHex(@"cc3378");
        public static readonly Color4 RED_3 = Color4Extensions.FromHex(@"cc3333");
        public static readonly Color4 YELLOW = Color4Extensions.FromHex(@"ffcc22");

        /// <summary>
        /// The rating spread's bars, from 1 to 10.
        /// </summary>
        public static readonly Color4[] RATING_SPREAD =
        {
            Color4Extensions.FromHex(@"cc3333"), Color4Extensions.FromHex(@"cc3d33"), Color4Extensions.FromHex(@"cc4f33"), Color4Extensions.FromHex(@"d96633"),
            Color4Extensions.FromHex(@"e6893d"), Color4Extensions.FromHex(@"e6a84d"), Color4Extensions.FromHex(@"d9cc55"), Color4Extensions.FromHex(@"b8e05c"),
            Color4Extensions.FromHex(@"b2ff66"), Color4Extensions.FromHex(@"b2ff66"),
        };
    }

    /// <summary>
    /// A 4 high rounded bar on a dark track, filled to a fraction.
    /// </summary>
    public partial class WebBar : CircularContainer
    {
        private readonly Box fill;
        private readonly Box track;

        public Color4 TrackColour
        {
            set => track.Colour = value;
        }

        public Color4 FillColour
        {
            set => fill.Colour = value;
        }

        public WebBar()
        {
            RelativeSizeAxes = Axes.X;
            Height = 4;
            Masking = true;

            Children = new Drawable[]
            {
                track = new Box { RelativeSizeAxes = Axes.Both },
                new CircularContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    Width = 0,
                    Child = fill = new Box { RelativeSizeAxes = Axes.Both },
                }.With(c => fillContainer = c),
            };
        }

        private CircularContainer fillContainer = null!;

        public void SetFraction(float fraction) => fillContainer.ResizeWidthTo(Math.Clamp(fraction, 0, 1), 300, Easing.OutQuint);
    }

    /// <summary>
    /// The website's stat row: "Circle Size | 4 | ———" (the label 100 wide, the value 40 at its end, then the bar out of 10).
    /// </summary>
    public partial class WebStatRow : GridContainer
    {
        public readonly LocalisableString Title;

        public float Value { get; private set; }

        private readonly OsuSpriteText value;
        private readonly WebBar bar;

        public WebStatRow(LocalisableString title, bool starRating = false)
        {
            Title = title;

            RelativeSizeAxes = Axes.X;
            Height = 16;
            ColumnDimensions = new[]
            {
                new Dimension(GridSizeMode.Absolute, 100),
                new Dimension(GridSizeMode.Absolute, 10),
                new Dimension(GridSizeMode.Absolute, 40),
                new Dimension(GridSizeMode.Absolute, 10),
                new Dimension(),
            };

            Content = new[]
            {
                new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = title,
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                    },
                    Empty(),
                    value = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                    },
                    Empty(),
                    bar = new WebBar
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        FillColour = starRating ? WebBeatmapSetColours.YELLOW : Color4.White,
                    },
                }
            };

            starRatingBar = starRating;
        }

        private readonly bool starRatingBar;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            bar.TrackColour = colours.Background3;

            if (!starRatingBar)
                bar.FillColour = colours.Highlight1;
        }

        public void SetValue(float newValue, string text)
        {
            Value = newValue;
            value.Text = text;
            bar.SetFraction(newValue / 10);
        }
    }

    /// <summary>
    /// The website's points of failure: a 10 high rounded strip of equal cells, coloured by how many failed or quit there (of the most).
    /// </summary>
    public partial class WebFailStrip : CompositeDrawable
    {
        private readonly FillFlowContainer cells;

        public WebFailStrip()
        {
            RelativeSizeAxes = Axes.X;
            Height = 10;
            Masking = true;
            CornerRadius = 5;

            InternalChild = cells = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
            };
        }

        public void SetFailTimes(APIFailTimes? failTimes)
        {
            cells.Clear();

            int[] fails = failTimes?.Fails ?? Array.Empty<int>();
            int[] exits = failTimes?.Retries ?? Array.Empty<int>();
            int count = Math.Max(fails.Length, exits.Length);

            int[] totals = Enumerable.Range(0, count).Select(i => (i < fails.Length ? fails[i] : 0) + (i < exits.Length ? exits[i] : 0)).ToArray();
            int max = totals.DefaultIfEmpty(0).Max();

            if (max == 0)
            {
                Hide();
                return;
            }

            Show();

            foreach (int total in totals)
            {
                float share = (float)total / max;

                cells.Add(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Width = 1f / count,
                    Colour = Color4Extensions.FromHex(share > 0.75f ? @"eb5757" : share > 0.5f ? @"eeaa00" : share > 0.25f ? @"ffcc22" : share > 0.1f ? @"ffdd55" : @"aadd00"),
                });
            }
        }
    }

    /// <summary>
    /// The website's rounded buttons (30 high, 12 semibold, a shadow on their text), a little brighter under the mouse.
    /// </summary>
    public partial class WebPillButton : OsuClickableContainer
    {
        private readonly Box background;
        private readonly FillFlowContainer content;
        private Color4 colour;

        public Color4 BackgroundColour
        {
            get => colour;
            set
            {
                colour = value;
                background.Colour = value;
            }
        }

        public WebPillButton()
        {
            AutoSizeAxes = Axes.X;
            Height = 30;
            Masking = true;
            CornerRadius = 15;

            Children = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                content = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4, 0),
                    Padding = new MarginPadding { Horizontal = 15 },
                },
            };
        }

        public WebPillButton AddIcon(IconUsage icon)
        {
            content.Add(new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Icon = icon,
                Size = new Vector2(12),
                Shadow = true,
            });
            return this;
        }

        public OsuSpriteText AddText(LocalisableString text)
        {
            var sprite = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = text,
                Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
            };

            content.Add(sprite);
            return sprite;
        }

        public void ClearContent() => content.Clear();

        /// <summary>
        /// Keeps the button at least this wide, its content in the middle.
        /// </summary>
        public float MinimumWidth { get; set; }

        protected override void Update()
        {
            base.Update();

            float padding = Math.Max(15, (MinimumWidth - (content.DrawWidth - content.Padding.TotalHorizontal)) / 2);

            if (content.Padding.Left != padding)
                content.Padding = new MarginPadding { Horizontal = padding };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(colour.Lighten(0.1f), 120);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(colour, 120);
            base.OnHoverLost(e);
        }
    }

    /// <summary>
    /// A label over its value, the website's credits and counts.
    /// </summary>
    public partial class WebLabelledValue : FillFlowContainer, IHasTooltip
    {
        public LocalisableString TooltipText { get; set; }

        public WebLabelledValue(LocalisableString label, Drawable value, Color4 labelColour)
        {
            AutoSizeAxes = Axes.Both;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 2);

            Children = new[]
            {
                new OsuSpriteText
                {
                    Text = label,
                    Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                    Colour = labelColour,
                },
                value,
            };
        }
    }
}

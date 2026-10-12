// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    public abstract partial class ToolbarButton : OsuClickableContainer, IKeyBindingHandler<GlobalAction>
    {
        public const float PADDING = 3;

        protected GlobalAction? Hotkey { get; set; }

        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);
        private IDisposable? customUiHueBinding;

        public void SetIcon(Drawable icon)
        {
            IconContainer.Icon = icon;
            IconContainer.Show();
        }

        public void SetIcon(IconUsage icon) => SetIcon(new SpriteIcon { Icon = icon });

        public LocalisableString TooltipMain
        {
            get => tooltip1.Text;
            set => tooltip1.Text = value;
        }

        public LocalisableString TooltipSub
        {
            get => tooltip2.Text;
            set => tooltip2.Text = value;
        }

        protected virtual Anchor TooltipAnchor => Anchor.TopLeft;

        protected readonly Container ButtonContent;
        protected ConstrainedIconContainer IconContainer;
        protected Box HoverBackground;
        private readonly Box flashBackground;
        private readonly FillFlowContainer tooltipContainer;
        private readonly SpriteText tooltip1;
        private readonly SpriteText tooltip2;
        protected FillFlowContainer Flow;

        protected readonly Container BackgroundContent;

        private readonly FillFlowContainer subTooltipFlow;

        protected ToolbarButton()
        {
            AutoSizeAxes = Axes.X;
            RelativeSizeAxes = Axes.Y;

            Children = new Drawable[]
            {
                ButtonContent = new Container
                {
                    Width = Toolbar.HEIGHT,
                    RelativeSizeAxes = Axes.Y,
                    Padding = new MarginPadding(PADDING),
                    Children = new Drawable[]
                    {
                        BackgroundContent = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 6,
                            CornerExponent = 3f,
                            Children = new Drawable[]
                            {
                                HoverBackground = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Blending = BlendingParameters.Additive,
                                    Alpha = 0,
                                },
                                flashBackground = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Alpha = 0,
                                    Blending = BlendingParameters.Additive,
                                },
                            }
                        },
                        Flow = new FillFlowContainer
                        {
                            Direction = FillDirection.Horizontal,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Padding = new MarginPadding { Left = Toolbar.HEIGHT / 2, Right = Toolbar.HEIGHT / 2 },
                            RelativeSizeAxes = Axes.Y,
                            AutoSizeAxes = Axes.X,
                            Children = new Drawable[]
                            {
                                IconContainer = new ConstrainedIconContainer
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(20),
                                    Alpha = 0,
                                },
                            },
                        },
                    },
                },
                tooltipContainer = new FillFlowContainer
                {
                    Direction = FillDirection.Vertical,
                    RelativeSizeAxes = Axes.Both,
                    Anchor = TooltipAnchor.HasFlag(Anchor.x0) ? Anchor.BottomLeft : Anchor.BottomRight,
                    Origin = TooltipAnchor,
                    Position = new Vector2(TooltipAnchor.HasFlag(Anchor.x0) ? 5 : -5, 5),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        tooltip1 = new OsuSpriteText
                        {
                            Anchor = TooltipAnchor,
                            Origin = TooltipAnchor,
                            Shadow = true,
                            Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold),
                        },
                        subTooltipFlow = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = TooltipAnchor,
                            Origin = TooltipAnchor,
                            Direction = FillDirection.Horizontal,
                            Children = new Drawable[]
                            {
                                tooltip2 = new OsuSpriteText { Shadow = true },
                            }
                        }
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            customUiHueBinding = CustomUiHueHelper.BindFullScheme(
                config,
                colourProvider,
                OverlayColourScheme.Pink.GetHue(),
                CustomUiHueScope.Menu);

            colourProvider.ColoursChanged += updateColours;

            updateColours();

            if (Hotkey != null)
            {
                subTooltipFlow.Add(new HotkeyDisplay
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Hotkey = new Hotkey(Hotkey.Value),
                    Margin = new MarginPadding { Left = 3 },
                });
            }
        }

        private void updateColours()
        {
            HoverBackground.Colour = colourProvider.Highlight1.Opacity(0.35f);
            flashBackground.Colour = colourProvider.Highlight1.Opacity(0.45f);

            tooltip1.Colour = Color4.White;
            tooltip2.Colour = colourProvider.Light1;
        }

        protected override bool OnMouseDown(MouseDownEvent e) => false;

        protected override bool OnClick(ClickEvent e)
        {
            flashBackground.FadeIn(50).Then().FadeOutFromOne(800, Easing.OutQuint);
            tooltipContainer.FadeOut(100);
            return base.OnClick(e);
        }

        protected override bool OnHover(HoverEvent e)
        {
            HoverBackground.FadeIn(300, Easing.OutQuint);
            tooltipContainer.FadeIn(200, Easing.OutQuint);

            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            HoverBackground.FadeOut(200, Easing.Out);
            tooltipContainer.FadeOut(100, Easing.Out);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == Hotkey && !e.Repeat)
            {
                TriggerClick();
                return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            colourProvider.ColoursChanged -= updateColours;
            customUiHueBinding?.Dispose();
        }
    }

    public partial class OpaqueBackground : Container
    {
        public OpaqueBackground()
        {
            RelativeSizeAxes = Axes.Both;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = OsuColour.Gray(30)
                },
                new Triangles
                {
                    RelativeSizeAxes = Axes.Both,
                    ColourLight = OsuColour.Gray(40),
                    ColourDark = OsuColour.Gray(20),
                },
            };
        }
    }
}
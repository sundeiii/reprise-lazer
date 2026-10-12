// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// One-time popup shown the first time a player presses the mid-map break skip button.
    /// Uses the lazer V2 form controls instead of Torii briefing visuals.
    /// </summary>
    public partial class SkipBreakBriefingOverlay : VisibilityContainer, IKeyBindingHandler<GlobalAction>
    {
        public Action OnDismiss;

        private readonly Bindable<bool> singleConfirmation;

        private Container panel;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; }

        public SkipBreakBriefingOverlay(Bindable<bool> singleConfirmation)
        {
            this.singleConfirmation = singleConfirmation;

            RelativeSizeAxes = Axes.Both;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        protected override bool OnClick(ClickEvent e) => true;

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        [BackgroundDependencyLoader]
        private void load()
        {
            FillFlowContainer content;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.55f,
                },
                panel = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 540,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 18,
                    CornerExponent = 2.5f,
                    BorderThickness = 2.5f,
                    BorderColour = colourProvider.Highlight1.Opacity(0.22f),
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
                            Colour = colourProvider.Background4,
                            Alpha = 0.35f,
                        },
                        content = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 14),
                            Padding = new MarginPadding(24),
                        },
                    },
                },
            };

            content.AddRange(new Drawable[]
            {
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.Forward,
                            Size = new Vector2(14),
                            Colour = colourProvider.Colour2,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = "REPRISE GAMEPLAY",
                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                            Colour = colourProvider.Colour2,
                        },
                    },
                },
                new OsuSpriteText
                {
                    Text = "Skip breaks",
                    Font = OsuFont.GetFont(size: 25, weight: FontWeight.SemiBold),
                    Colour = colourProvider.Content1,
                },
                new OsuTextFlowContainer(t =>
                {
                    t.Font = OsuFont.GetFont(size: 16);
                    t.Colour = colourProvider.Content2.Opacity(0.9f);
                })
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = "Long break sections can be skipped. Press the skip button once to confirm, then press it again to jump near the end of the break.",
                },
                new FormCheckBox
                {
                    Caption = "Single confirmation",
                    HintText = "Skip immediately with one press instead of requiring a second confirmation.",
                    Current = { BindTarget = singleConfirmation },
                },
                new FormButton
                {
                    Caption = "All set?",
                    ButtonText = "Got it",
                    Action = dismiss,
                },
            });
        }

        private void dismiss()
        {
            Hide();
            OnDismiss?.Invoke();
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            if (e.Action == GlobalAction.Back)
            {
                dismiss();
                return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected override void PopIn()
        {
            this.FadeIn(180, Easing.OutQuint);

            panel.ScaleTo(0.96f)
                 .ScaleTo(1f, 450, Easing.OutElasticHalf)
                 .MoveToY(18)
                 .MoveToY(0, 450, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            this.FadeOut(180, Easing.OutQuint);

            panel.ScaleTo(0.97f, 180, Easing.OutQuint)
                 .MoveToY(10, 180, Easing.OutQuint);
        }
    }
}
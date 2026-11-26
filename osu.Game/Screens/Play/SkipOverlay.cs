// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using JetBrains.Annotations;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    public partial class SkipOverlay : Container, IKeyBindingHandler<GlobalAction>
    {
        /// <summary>
        /// The total number of successful skips performed by this overlay.
        /// </summary>
        public int SkipCount { get; private set; }

        private readonly double startTime;

        public Action RequestSkip;

        protected FadeContainer FadingContent { get; private set; }

        private OsuClickableContainer button;

        private ButtonContainer buttonContainer;
        protected Circle RemainingTimeBox { get; private set; }

        private double displayTime;

        /// <summary>
        /// Whether the gameplay clock is currently at the skippable period.
        /// </summary>
        private readonly BindableBool inSkipPeriod = new BindableBool();

        private bool skipQueued;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; }

        /// <summary>
        /// Reprise: osu!stable's skip button (<see cref="Configuration.OsuSetting.StableSkipButton"/>): the skin's play-skip in the bottom right
        /// at 60% (all of it when hovered), shown the whole time it can be used, without the time bar. Changing it once loaded (the skin
        /// changed mid-map) builds the overlay again.
        /// </summary>
        public bool StableButton
        {
            get => stableButton;
            set
            {
                if (stableButton == value)
                    return;

                stableButton = value;

                if (IsLoaded)
                    Schedule(rebuild);
            }
        }

        private bool stableButton;

        /// <summary>
        /// Whether <see cref="StableButton"/> changing once loaded builds the overlay again (not for overlays with their own button).
        /// </summary>
        protected virtual bool RebuildsForStableButton => true;

        internal bool IsButtonVisible => FadingContent.State == Visibility.Visible && buttonContainer.State.Value == Visibility.Visible;
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        /// <summary>
        /// Displays a skip overlay, giving the user the ability to skip forward.
        /// </summary>
        /// <param name="startTime">The time at which gameplay begins to appear.</param>
        public SkipOverlay(double startTime)
        {
            this.startTime = startTime;

            RelativePositionAxes = Axes.Both;
            RelativeSizeAxes = Axes.X;

            Position = new Vector2(0.5f, 0.7f);
            Size = new Vector2(1, 100);

            Origin = Anchor.Centre;
        }

        [Resolved(canBeNull: true)]
        private OsuColour colours { get; set; }

        [BackgroundDependencyLoader(true)]
        private void load()
        {
            buildLayout();
        }

        private void buildLayout()
        {
            if (StableButton)
            {
                RelativePositionAxes = Axes.None;
                RelativeSizeAxes = Axes.Both;
                Position = Vector2.Zero;
                Size = Vector2.One;
                Anchor = Origin = Anchor.TopLeft;
            }
            else if (IsLoaded)
            {
                // Back from osu!stable's button: the constructor's placement.
                RelativePositionAxes = Axes.Both;
                RelativeSizeAxes = Axes.X;
                Position = new Vector2(0.5f, 0.7f);
                Size = new Vector2(1, 100);
                Anchor = Anchor.TopLeft;
                Origin = Anchor.Centre;
            }

            RemainingTimeBox = new Circle
            {
                Height = 5,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Colour = colours?.Orange3 ?? Colour4.Orange,
                RelativeSizeAxes = Axes.X
            };

            if (StableButton)
            {
                // Only the button itself takes the mouse (the rest of the screen is gameplay's), and there's no time bar.
                InternalChild = buttonContainer = new ButtonContainer
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    AutoSizeAxes = Axes.Both,
                    Child = FadingContent = new FadeContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        StayShown = true,
                        Child = button = CreateButton(inSkipPeriod),
                    }
                };

                return;
            }

            InternalChild = buttonContainer = new ButtonContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = FadingContent = new FadeContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        button = CreateButton(inSkipPeriod),
                        RemainingTimeBox,
                    }
                }
            };
        }

        /// <summary>
        /// Creates a skip button.
        /// </summary>
        /// <param name="inSkipPeriod">Whether the gameplay clock is currently at the skippable period.</param>
        protected virtual OsuClickableContainer CreateButton(IBindable<bool> inSkipPeriod) => StableButton
            ? new LegacyButton { Enabled = { BindTarget = inSkipPeriod } }
            : new Button
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Enabled = { BindTarget = inSkipPeriod },
        };

        private const double fade_time = 300;

        private double fadeOutBeginTime => startTime - MasterGameplayClockContainer.MINIMUM_SKIP_TIME;

        public override void Hide()
        {
            base.Hide();
            FadingContent.Hide();
        }

        public override void Show()
        {
            base.Show();
            FadingContent.TriggerShow();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            displayTime = gameplayClock.CurrentTime;

            // skip is not required if there is no extra "empty" time to skip.
            // we may need to remove this if rewinding before the initial player load position becomes a thing.
            if (fadeOutBeginTime <= displayTime)
            {
                Expire();
                return;
            }

            bindButton();
            FadingContent.TriggerShow();
        }

        private void bindButton() => button.Action = () =>
        {
            SkipCount++;
            RequestSkip?.Invoke();
        };

        /// <summary>
        /// Reprise: the overlay again with the other button (osu!stable's, or the game's), when the skin changed mid-map.
        /// </summary>
        private void rebuild()
        {
            if (LifetimeEnd <= Time.Current || !RebuildsForStableButton)
                return;

            buildLayout();
            bindButton();

            if (inSkipPeriod.Value)
                FadingContent.TriggerShow();
        }

        /// <summary>
        /// Triggers an "automated" skip to happen as soon as available.
        /// </summary>
        public void SkipWhenReady()
        {
            if (skipQueued) return;

            skipQueued = true;
            attemptNextSkip();

            void attemptNextSkip() => Scheduler.AddDelayed(() =>
            {
                if (!button.Enabled.Value)
                {
                    skipQueued = false;
                    return;
                }

                button.TriggerClick();
                attemptNextSkip();
            }, 200);
        }

        protected override void Update()
        {
            base.Update();

            // This case causes an immediate expire in `LoadComplete`, but `Update` may run once after that.
            // Avoid div-by-zero below.
            if (fadeOutBeginTime <= displayTime)
                return;

            double progress = Math.Max(0, 1 - (gameplayClock.CurrentTime - displayTime) / (fadeOutBeginTime - displayTime));

            // osu!stable's button: skin elements are made for 1024x768.
            if (StableButton)
                button.Scale = new Vector2(DrawHeight / 768f);

            RemainingTimeBox.Width = (float)Interpolation.DampContinuously(RemainingTimeBox.Width, progress, 40, Math.Abs(Time.Elapsed));

            inSkipPeriod.Value = progress > 0;
            buttonContainer.State.Value = inSkipPeriod.Value ? Visibility.Visible : Visibility.Hidden;
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (inSkipPeriod.Value && !e.HasAnyButtonPressed)
                FadingContent.TriggerShow();

            return base.OnMouseMove(e);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.SkipCutscene:
                    if (!button.Enabled.Value)
                        return false;

                    button.TriggerClick();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        public partial class FadeContainer : Container, IStateful<Visibility>
        {
            [CanBeNull]
            public event Action<Visibility> StateChanged;

            private Visibility state;
            private double? nextHideTime;

            public override bool IsPresent => true;

            /// <summary>Never hidden for want of the mouse moving (osu!stable's skip button).</summary>
            public bool StayShown { get; set; }

            public void TriggerShow()
            {
                Show();

                if (!StayShown && !IsHovered && !IsDragged)
                    nextHideTime = Time.Current + 1000;
                else
                    nextHideTime = null;
            }

            protected override void Update()
            {
                base.Update();

                if (nextHideTime != null && nextHideTime <= Time.Current)
                {
                    Hide();
                    nextHideTime = null;
                }
            }

            public Visibility State
            {
                get => state;
                set
                {
                    if (value == state)
                        return;

                    state = value;

                    switch (state)
                    {
                        case Visibility.Visible:
                            this.FadeIn(500, Easing.OutExpo);
                            break;

                        case Visibility.Hidden:
                            this.FadeOut(1000, Easing.OutExpo);
                            break;
                    }

                    StateChanged?.Invoke(State);
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Show();
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                Show();
                nextHideTime = null;
                return true;
            }

            protected override void OnMouseUp(MouseUpEvent e)
            {
                Show();
            }

            public override void Hide() => State = Visibility.Hidden;

            public override void Show() => State = Visibility.Visible;
        }

        /// <summary>
        /// osu!stable's skip button: the skin's play-skip (animated; osu!stable's own when it has none) in the bottom right of the screen at
        /// its 1024x768 size, 60% shown and all of it over 300ms when hovered, with menuhit when used.
        /// </summary>
        internal partial class LegacyButton : OsuClickableContainer
        {
            [Resolved]
            private ISkinSource skin { get; set; }

            [Resolved]
            private SkinManager skinManager { get; set; }

            private Container content;
            private ISample sampleHit;

            public LegacyButton()
            {
                Anchor = Anchor.BottomRight;
                Origin = Anchor.BottomRight;
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(AudioManager audio, TextureStore textures)
            {
                // Reprise: the skin's menuhit, as stable plays (there was no "UI/menuhit" to play); the classic skin's when it has
                // none; looked up again when the skin changes mid-map.
                this.textures = textures;

                Add(content = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Alpha = 0.6f,
                });

                updateSkin();
                skin.SourceChanged += onSourceChanged;

                Action += () => sampleHit?.Play();
            }

            private TextureStore textures;

            private void onSourceChanged() => Schedule(updateSkin);

            /// <summary>
            /// The skin's play-skip and menuhit, again when the skin changes mid-map.
            /// </summary>
            private void updateSkin()
            {
                sampleHit = skin.GetSample(new SampleInfo(@"menuhit")) ?? skinManager.DefaultClassicSkin.GetSample(new SampleInfo(@"menuhit"));

                // osu!stable's own, for skins without one (the classic skin has none).
                var stableTexture = textures.Get(@"Stable/play-skip@2x");

                content.Child = skin.GetAnimation(@"play-skip", true, true, applyConfigFrameRate: true)
                                ?? skinManager.DefaultClassicSkin.GetAnimation(@"play-skip", true, true, applyConfigFrameRate: true)
                                ?? (stableTexture == null
                                    ? Empty()
                                    : new Sprite { Texture = stableTexture, Size = new Vector2(stableTexture.Width / 2f, stableTexture.Height / 2f) });
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                if (skin != null)
                    skin.SourceChanged -= onSourceChanged;
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (Enabled.Value)
                    content.FadeTo(1, 300);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                content.FadeTo(0.6f, 300);
                base.OnHoverLost(e);
            }
        }

        internal partial class ButtonContainer : VisibilityContainer
        {
            protected override void PopIn() => this.FadeIn(fade_time);

            protected override void PopOut() => this.FadeOut(fade_time);
        }

        internal partial class Button : OsuClickableContainer
        {
            private Color4 colourNormal;
            private Color4 colourHover;
            private Box box;
            private FillFlowContainer flow;
            private Box background;
            private AspectContainer aspect;

            private Sample sampleConfirm;

            public Button()
            {
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours, AudioManager audio)
            {
                colourNormal = colours.Orange3;
                colourHover = colours.Orange3.Lighten(0.2f);

                sampleConfirm = audio.Samples.Get(@"UI/submit-select");

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        Alpha = 0.2f,
                        Colour = Color4.Black,
                        RelativeSizeAxes = Axes.Both,
                    },
                    aspect = new AspectContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Y,
                        Height = 0.6f,
                        Masking = true,
                        CornerRadius = 15,
                        Children = new Drawable[]
                        {
                            box = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourNormal,
                            },
                            new TrianglesV2
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = ColourInfo.GradientVertical(colourNormal.Lighten(0.2f), colourNormal)
                            },
                            flow = new FillFlowContainer
                            {
                                Anchor = Anchor.TopCentre,
                                RelativePositionAxes = Axes.Y,
                                Y = 0.4f,
                                AutoSizeAxes = Axes.Both,
                                Origin = Anchor.Centre,
                                Direction = FillDirection.Horizontal,
                                Children = new[]
                                {
                                    new SpriteIcon { Size = new Vector2(15), Shadow = true, Icon = FontAwesome.Solid.ChevronRight },
                                    new SpriteIcon { Size = new Vector2(15), Shadow = true, Icon = FontAwesome.Solid.ChevronRight },
                                    new SpriteIcon { Size = new Vector2(15), Shadow = true, Icon = FontAwesome.Solid.ChevronRight },
                                }
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                RelativePositionAxes = Axes.Y,
                                Y = 0.7f,
                                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 12),
                                Origin = Anchor.Centre,
                                Text = @"SKIP",
                            },
                        }
                    }
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                flow.TransformSpacingTo(new Vector2(5), 500, Easing.OutQuint);
                box.FadeColour(colourHover, 500, Easing.OutQuint);
                background.FadeTo(0.4f, 500, Easing.OutQuint);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                flow.TransformSpacingTo(new Vector2(0), 500, Easing.OutQuint);
                box.FadeColour(colourNormal, 500, Easing.OutQuint);
                background.FadeTo(0.2f, 500, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                aspect.ScaleTo(0.75f, 2000, Easing.OutQuint);
                return base.OnMouseDown(e);
            }

            protected override void OnMouseUp(MouseUpEvent e)
            {
                aspect.ScaleTo(1, 1000, Easing.OutElastic);
                base.OnMouseUp(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (!Enabled.Value)
                    return false;

                sampleConfirm.Play();

                box.FlashColour(Color4.White, 500, Easing.OutQuint);
                aspect.ScaleTo(1.2f, 2000, Easing.OutQuint);

                return base.OnClick(e);
            }
        }
    }
}

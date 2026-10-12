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
using osu.Game.Configuration;
using osu.Game.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarOverlayToggleButton : ToolbarButton
    {
        private Box stateBackground;

        private OverlayContainer stateContainer;

        private readonly Bindable<Visibility> overlayState = new Bindable<Visibility>();

        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);
        private IDisposable customUiHueBinding;

        public OverlayContainer StateContainer
        {
            get => stateContainer;
            set
            {
                stateContainer = value;

                overlayState.UnbindBindings();

                if (stateContainer != null)
                {
                    Action = stateContainer.ToggleVisibility;
                    overlayState.BindTo(stateContainer.State);
                }

                if (stateContainer is INamedOverlayComponent named)
                {
                    TooltipMain = named.Title;
                    TooltipSub = named.Description;
                    SetIcon(named.Icon);
                }
            }
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

            BackgroundContent.Add(stateBackground = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Blending = BlendingParameters.Additive,
                Depth = float.MaxValue,
                Alpha = 0,
            });

            updateColours();

            overlayState.ValueChanged += stateChanged;
        }

        private void updateColours()
        {
            stateBackground.Colour = colourProvider.Highlight1.Opacity(0.45f);
        }

        private void stateChanged(ValueChangedEvent<Visibility> state)
        {
            switch (state.NewValue)
            {
                case Visibility.Hidden:
                    stateBackground.FadeOut(200, Easing.OutQuint);
                    break;

                case Visibility.Visible:
                    stateBackground.FadeIn(200, Easing.OutQuint);
                    break;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            colourProvider.ColoursChanged -= updateColours;
            customUiHueBinding?.Dispose();
        }
    }
}
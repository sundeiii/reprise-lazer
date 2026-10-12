// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>
    /// The website's "View: Nano Mini Normal Extra Cover List" (12, the views light and semibold, the chosen one white and bold).
    /// </summary>
    public partial class BeatmapListingViewSelector : FillFlowContainer, IHasCurrentValue<BeatmapListingView>
    {
        private readonly BindableWithCurrent<BeatmapListingView> current = new BindableWithCurrent<BeatmapListingView>();

        public Bindable<BeatmapListingView> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        public BeatmapListingViewSelector()
        {
            AutoSizeAxes = Axes.Both;
            Direction = FillDirection.Horizontal;
            Spacing = new Vector2(16, 0);
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            Add(new OsuSpriteText
            {
                Text = @"View",
                Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                Colour = colours.Content1,
            });

            foreach (var view in Enum.GetValues<BeatmapListingView>())
                Add(new ViewButton(view) { Current = { BindTarget = Current } });
        }

        private partial class ViewButton : OsuClickableContainer
        {
            public readonly Bindable<BeatmapListingView> Current = new Bindable<BeatmapListingView>();

            private readonly BeatmapListingView view;
            private OsuSpriteText text = null!;

            [Resolved]
            private OverlayColourProvider colours { get; set; } = null!;

            public ViewButton(BeatmapListingView view)
            {
                this.view = view;
                AutoSizeAxes = Axes.Both;
                Action = () => Current.Value = view;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = text = new OsuSpriteText
                {
                    Text = view.ToString(),
                    Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Current.BindValueChanged(_ => updateState(), true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                base.OnHoverLost(e);
                updateState();
            }

            private void updateState()
            {
                bool active = Current.Value == view;

                text.Colour = active || IsHovered ? colours.Content1 : colours.Light1;
                text.Font = text.Font.With(weight: active ? FontWeight.Bold : FontWeight.SemiBold);
            }
        }
    }
}

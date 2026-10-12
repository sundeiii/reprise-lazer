// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics;
using osuTK;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;

namespace osu.Game.Overlays.BeatmapListing
{
    public partial class BeatmapSearchFilterRow<T> : CompositeDrawable, IHasCurrentValue<T>
    {
        private readonly BindableWithCurrent<T> current = new BindableWithCurrent<T>();

        public Bindable<T> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        private readonly Box line;

        // The website's filter column (osu!'s search design proposal): a 3 high line over the filter, its title (10, bold) and its
        // choices under each other.
        public BeatmapSearchFilterRow(LocalisableString header)
        {
            Drawable filter;
            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;
            AddRangeInternal(new Drawable[]
            {
                line = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 3,
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Top = 9 },
                    Spacing = new Vector2(0, 4),
                    Children = new[]
                    {
                        new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Text = header
                        },
                        filter = CreateFilter()
                    }
                }
            });

            if (filter is IHasCurrentValue<T> filterWithValue)
                Current = filterWithValue.Current;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            line.Colour = colourProvider.Background3;
        }

        protected virtual Drawable CreateFilter() => new BeatmapSearchFilter();

        protected partial class BeatmapSearchFilter : TabControl<T>
        {
            public BeatmapSearchFilter()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                TabContainer.Spacing = new Vector2(0, 1);

                if (typeof(T).IsEnum)
                {
                    foreach (var val in EnumExtensions.GetValuesInOrder<T>())
                        AddItem(val);
                }
            }

            protected override Dropdown<T> CreateDropdown() => null!;

            protected override TabItem<T> CreateTabItem(T value) => new FilterTabItem<T>(value);

            protected override TabFillFlowContainer CreateTabFlow() => new TabFillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                // All of them (a single line hides those that don't fit in its width).
                AllowMultiline = true,
                // Room for the chosen one's dot.
                Padding = new MarginPadding { Left = 9 },
            };
        }
    }
}

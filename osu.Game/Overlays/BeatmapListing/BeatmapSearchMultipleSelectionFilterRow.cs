// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osuTK;
using osuTK.Graphics;
using FontWeight = osu.Game.Graphics.FontWeight;

namespace osu.Game.Overlays.BeatmapListing
{
    public partial class BeatmapSearchMultipleSelectionFilterRow<T> : BeatmapSearchFilterRow<List<T>>
        where T : Enum
    {
        public new readonly BindableList<T> Current = new BindableList<T>();

        private MultipleSelectionFilter filter = null!;

        public BeatmapSearchMultipleSelectionFilterRow(LocalisableString header)
            : base(header)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            filter.Current.BindTo(Current);
        }

        protected sealed override Drawable CreateFilter() => filter = CreateMultipleSelectionFilter();

        /// <summary>
        /// Creates a filter control that can be used to simultaneously select multiple values of type <typeparamref name="T"/>.
        /// </summary>
        protected virtual MultipleSelectionFilter CreateMultipleSelectionFilter() => new MultipleSelectionFilter();

        protected partial class MultipleSelectionFilter : FillFlowContainer<MultipleSelectionFilterTabItem>
        {
            public readonly BindableList<T> Current = new BindableList<T>();

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(0, 1);
                // Room for the chosen ones' dots.
                Padding = new MarginPadding { Left = 9 };

                AddRange(GetValues().Select(CreateTabItem));
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                foreach (var item in Children)
                    item.Active.BindValueChanged(active => toggleItem(item.Value, active.NewValue));

                Current.BindCollectionChanged(currentChanged, true);
            }

            private void currentChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                foreach (var c in Children)
                {
                    if (!c.Active.Disabled)
                        c.Active.Value = Current.Contains(c.Value);
                }
            }

            /// <summary>
            /// Returns all values to be displayed in this filter row.
            /// </summary>
            protected virtual IEnumerable<T> GetValues() => Enum.GetValues(typeof(T)).Cast<T>();

            /// <summary>
            /// Creates a <see cref="MultipleSelectionFilterTabItem"/> representing the supplied <paramref name="value"/>.
            /// </summary>
            protected virtual MultipleSelectionFilterTabItem CreateTabItem(T value) => new MultipleSelectionFilterTabItem(value);

            private void toggleItem(T value, bool active)
            {
                if (active)
                {
                    if (!Current.Contains(value))
                        Current.Add(value);
                }
                else
                    Current.Remove(value);
            }
        }

        protected partial class MultipleSelectionFilterTabItem : FilterTabItem<T>
        {
            public MultipleSelectionFilterTabItem(T value)
                : base(value)
            {
            }

            protected override bool OnClick(ClickEvent e)
            {
                base.OnClick(e);

                // this tab item implementation is not managed by a TabControl,
                // therefore we have to manually update Active state and play select sound when this tab item is clicked.
                Active.Toggle();
                SelectSample.Play();
                return true;
            }
        }
    }
}

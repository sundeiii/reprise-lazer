// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables.Cards.Web;
using osu.Game.Graphics;
using osu.Game.Resources.Localisation.Web;
using osuTK;

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>
    /// The website's categories: pills in their statuses' colours (the rest light), faded unless chosen.
    /// </summary>
    public partial class BeatmapSearchCategoryFilterRow : BeatmapSearchFilterRow<SearchCategory>
    {
        public BeatmapSearchCategoryFilterRow()
            : base(BeatmapsStrings.ListingSearchFiltersStatus)
        {
        }

        protected override Drawable CreateFilter() => new CategoryFilter();

        private partial class CategoryFilter : BeatmapSearchFilter
        {
            public CategoryFilter()
            {
                TabContainer.Spacing = new Vector2(0, 3);
                TabContainer.Padding = new MarginPadding();
            }

            protected override TabItem<SearchCategory> CreateTabItem(SearchCategory value) => new PillItem(value);
        }

        private partial class PillItem : FilterTabItem<SearchCategory>
        {
            public PillItem(SearchCategory value)
                : base(value)
            {
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                AutoSizeAxes = Axes.None;
                Size = new Vector2(90, 14);
                Masking = true;
                CornerRadius = 7;

                BeatmapOnlineStatus? status = statusFor(Value);

                AddInternal(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Depth = float.MaxValue,
                    Colour = status != null ? WebBeatmapStyle.StatusBackground(status.Value, ColourProvider) : ColourProvider.Content2,
                });

                Text.Anchor = Anchor.Centre;
                Text.Origin = Anchor.Centre;
                Text.Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold);
                Text.Text = LabelFor(Value).ToUpper();
                Text.Colour = status != null ? WebBeatmapStyle.StatusText(status.Value, ColourProvider) : ColourProvider.Background4;
                Text.Shadow = false;
                Text.UseFullGlyphHeight = false;
            }

            private static BeatmapOnlineStatus? statusFor(SearchCategory category)
            {
                switch (category)
                {
                    case SearchCategory.Ranked:
                        return BeatmapOnlineStatus.Ranked;

                    case SearchCategory.Qualified:
                        return BeatmapOnlineStatus.Qualified;

                    case SearchCategory.Loved:
                        return BeatmapOnlineStatus.Loved;

                    case SearchCategory.Pending:
                        return BeatmapOnlineStatus.Pending;

                    case SearchCategory.Wip:
                        return BeatmapOnlineStatus.WIP;

                    case SearchCategory.Graveyard:
                        return BeatmapOnlineStatus.Graveyard;

                    default:
                        return null;
                }
            }

            // The website's: 55% faded, 85% under the mouse, whole when chosen.
            protected override void UpdateState()
            {
                this.FadeTo(Active.Value ? 1 : IsHovered ? 0.85f : 0.55f, 120, Easing.OutQuint);
            }
        }
    }
}

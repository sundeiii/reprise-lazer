// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Game.Online.Leaderboards;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Overlays.BeatmapListing
{
    /// <summary>
    /// The website's "Rank Achieved": the grades' badges, four a row, faded unless chosen.
    /// </summary>
    public partial class BeatmapSearchScoreFilterRow : BeatmapSearchMultipleSelectionFilterRow<ScoreRank>
    {
        public BeatmapSearchScoreFilterRow()
            : base(BeatmapsStrings.ListingSearchFiltersRank)
        {
        }

        protected override MultipleSelectionFilter CreateMultipleSelectionFilter() => new RankFilter();

        private partial class RankFilter : MultipleSelectionFilter
        {
            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.None;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Full;
                Spacing = new Vector2(14, 10);
                Padding = new MarginPadding { Left = 19 };
                Width = 19 + 4 * 32 + 3 * 14;
            }

            protected override MultipleSelectionFilterTabItem CreateTabItem(ScoreRank value) => new RankItem(value);

            protected override IEnumerable<ScoreRank> GetValues() => base.GetValues().Where(r => r > ScoreRank.F).Reverse();
        }

        private partial class RankItem : MultipleSelectionFilterTabItem, IHasTooltip
        {
            public RankItem(ScoreRank value)
                : base(value)
            {
            }

            public LocalisableString TooltipText => Value.GetLocalisableDescription();

            [BackgroundDependencyLoader]
            private void load()
            {
                AutoSizeAxes = Axes.None;
                Size = new Vector2(32, 16);
                Text.Alpha = 0;

                AddInternal(new DrawableRank(Value)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
            }

            protected override LocalisableString LabelFor(ScoreRank value) => value.GetLocalisableDescription();

            // The website's: half faded, 80% under the mouse, whole when chosen (with the dot).
            protected override void UpdateState()
            {
                base.UpdateState();
                this.FadeTo(Active.Value ? 1 : IsHovered ? 0.8f : 0.5f, 120, Easing.OutQuint);
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.BeatmapListing
{
    public partial class BeatmapListingSearchControl : CompositeDrawable
    {
        /// <summary>
        /// Any time the text box receives key events (even while masked).
        /// </summary>
        public Action? TypingStarted;

        public Bindable<string> Query => textBox.Current;

        public BindableList<SearchGeneral> General => generalFilter.Current;

        public Bindable<RulesetInfo> Ruleset => modeFilter.Current;

        public Bindable<SearchCategory> Category => categoryFilter.Current;

        public Bindable<SearchGenre> Genre => genreFilter.Current;

        public Bindable<SearchLanguage> Language => languageFilter.Current;

        public BindableList<SearchExtra> Extra => extraFilter.Current;

        public BindableList<ScoreRank> Ranks => ranksFilter.Current;

        public Bindable<SearchPlayed> Played => playedFilter.Current;

        public Bindable<SearchExplicit> ExplicitContent => explicitContentFilter.Current;

        public APIBeatmapSet? BeatmapSet
        {
            set
            {
                // The website shows no cover behind its filters.
                if (value == null || string.IsNullOrEmpty(value.Covers.Cover))
                    return;

                beatmapCover.OnlineInfo = value;
            }
        }

        private readonly BeatmapSearchTextBox textBox;
        private readonly BeatmapSearchGeneralFilterRow generalFilter;
        private readonly BeatmapSearchRulesetFilterRow modeFilter;
        private readonly BeatmapSearchCategoryFilterRow categoryFilter;
        private readonly BeatmapSearchFilterRow<SearchGenre> genreFilter;
        private readonly BeatmapSearchFilterRow<SearchLanguage> languageFilter;
        private readonly BeatmapSearchMultipleSelectionFilterRow<SearchExtra> extraFilter;
        private readonly BeatmapSearchScoreFilterRow ranksFilter;
        private readonly BeatmapSearchFilterRow<SearchPlayed> playedFilter;
        private readonly BeatmapSearchFilterRow<SearchExplicit> explicitContentFilter;

        private readonly Box background;
        private readonly UpdateableOnlineBeatmapSetCover beatmapCover;

        public BeatmapListingSearchControl()
        {
            AutoSizeAxes = Axes.Y;
            RelativeSizeAxes = Axes.X;
            AddRangeInternal(new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    Child = beatmapCover = new TopSearchBeatmapSetCover
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                    }
                },
                // The website's (osu!'s search design proposal): the search box, then the filters in columns (Mode | General and
                // Rank Achieved, twice as wide | Categories | Language | Genre | Extra | Played and Explicit Content).
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding
                    {
                        Top = 10,
                        Bottom = 24,
                        Horizontal = 50,
                    },
                    Children = new Drawable[]
                    {
                        textBox = new BeatmapSearchTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Margin = new MarginPadding { Top = 16, Bottom = 10 },
                            TextChanged = () => TypingStarted?.Invoke(),
                        },
                        new FilterColumns
                        {
                            Columns = new[]
                            {
                                column(modeFilter = new BeatmapSearchRulesetFilterRow()),
                                column(generalFilter = new BeatmapSearchGeneralFilterRow(), ranksFilter = new BeatmapSearchScoreFilterRow()),
                                column(categoryFilter = new BeatmapSearchCategoryFilterRow()),
                                column(languageFilter = new BeatmapSearchFilterRow<SearchLanguage>(BeatmapsStrings.ListingSearchFiltersLanguage)),
                                column(genreFilter = new BeatmapSearchFilterRow<SearchGenre>(BeatmapsStrings.ListingSearchFiltersGenre)),
                                column(extraFilter = new BeatmapSearchMultipleSelectionFilterRow<SearchExtra>(BeatmapsStrings.ListingSearchFiltersExtra)),
                                column(playedFilter = new BeatmapSearchFilterRow<SearchPlayed>(BeatmapsStrings.ListingSearchFiltersPlayed),
                                    explicitContentFilter = new BeatmapSearchFilterRow<SearchExplicit>(BeatmapsStrings.ListingSearchFiltersNsfw)),
                            }
                        },
                    }
                }
            });

            generalFilter.Current.Add(SearchGeneral.FeaturedArtists);
            categoryFilter.Current.Value = SearchCategory.Leaderboard;
        }

        private IBindable<bool> allowExplicitContent = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuConfigManager config)
        {
            background.Colour = colourProvider.Background5;

            allowExplicitContent = config.GetBindable<bool>(OsuSetting.ShowOnlineExplicitContent);
            allowExplicitContent.BindValueChanged(allow =>
            {
                ExplicitContent.Value = allow.NewValue ? SearchExplicit.Show : SearchExplicit.Hide;
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            generalFilter.Ruleset.BindTo(Ruleset);
        }

        public void TakeFocus() => textBox.TakeFocus();

        private static FillFlowContainer column(params Drawable[] filters) => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 16),
            Children = filters,
        };

        /// <summary>
        /// The website's filter columns: the second twice as wide as the others, 10 apart (16 apart in rows at narrow widths, two a row).
        /// </summary>
        private partial class FilterColumns : Container
        {
            private const float gap = 10;

            public Drawable[] Columns
            {
                set => Children = value;
            }

            public FilterColumns()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            protected override void Update()
            {
                base.Update();

                if (DrawWidth < 700)
                {
                    float width = (DrawWidth - gap) / 2;
                    float y = 0;

                    for (int i = 0; i < Count; i++)
                    {
                        var c = Children[i];
                        c.Width = width;
                        c.Position = new Vector2(i % 2 * (width + gap), y);

                        if (i % 2 == 1 || i == Count - 1)
                            y += System.Math.Max(c.DrawHeight, i % 2 == 1 ? Children[i - 1].DrawHeight : 0) + 16;
                    }

                    return;
                }

                float unit = (DrawWidth - gap * (Count - 1)) / (Count + 1);
                float x = 0;

                for (int i = 0; i < Count; i++)
                {
                    var c = Children[i];
                    c.Width = i == 1 ? unit * 2 : unit;
                    c.Position = new Vector2(x, 0);
                    x += c.Width + gap;
                }
            }
        }

        private partial class BeatmapSearchTextBox : BasicSearchTextBox
        {
            /// <summary>
            /// Any time the text box receives key events (even while masked).
            /// </summary>
            public Action? TextChanged;

            protected override Color4 SelectionColour => Color4.Gray;

            public BeatmapSearchTextBox()
            {
                PlaceholderText = BeatmapsStrings.ListingSearchPrompt;
                Height = 45;
                CornerRadius = 10;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                BackgroundUnfocused = colourProvider.Background4;
                BackgroundFocused = colourProvider.Background4;
                BorderThickness = 0;
            }

            protected override bool OnKeyDown(KeyDownEvent e)
            {
                if (!base.OnKeyDown(e))
                    return false;

                TextChanged?.Invoke();
                return true;
            }

            public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
            {
                if (!base.OnPressed(e))
                    return false;

                TextChanged?.Invoke();
                return true;
            }
        }

        private partial class TopSearchBeatmapSetCover : UpdateableOnlineBeatmapSetCover
        {
            protected override bool TransformImmediately => true;
        }
    }
}

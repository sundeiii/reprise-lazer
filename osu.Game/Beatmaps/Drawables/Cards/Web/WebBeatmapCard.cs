// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Humanizer;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Beatmaps.Drawables.Cards.Buttons;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using OsuMenuItem = osu.Game.Graphics.UserInterface.OsuMenuItem;
using MenuItemType = osu.Game.Graphics.UserInterface.MenuItemType;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Resources.Localisation.Web;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Beatmaps.Drawables.Cards.Web
{
    /// <summary>
    /// The website's beatmap cards (osu-web's beatmap search after osu!'s search design proposal): Nano, Mini, Normal, Cover and List.
    /// The website's pixels are used as units.
    /// </summary>
    public partial class WebBeatmapCard : BeatmapCard
    {
        /// <summary>
        /// The card for a listing view: the game's own extra card for "Extra", else the website's.
        /// </summary>
        public static BeatmapCard Create(APIBeatmapSet set, BeatmapListingView view)
            => view == BeatmapListingView.Extra ? new ExtraCard(set) : new WebBeatmapCard(set, view);

        /// <summary>
        /// How many cards a row has in a view (on the website's desktop layout).
        /// </summary>
        public static int ColumnsFor(BeatmapListingView view) => view == BeatmapListingView.List ? 1 : 2;

        /// <summary>
        /// The gap between cards in a view.
        /// </summary>
        public static float GapFor(BeatmapListingView view)
        {
            switch (view)
            {
                case BeatmapListingView.Nano:
                    return 4;

                case BeatmapListingView.Mini:
                    return 8;

                case BeatmapListingView.List:
                    return 5;

                default:
                    return 10;
            }
        }

        public readonly BeatmapListingView View;

        protected override Drawable IdleContent => idleContent;
        protected override Drawable DownloadInProgressContent => downloadProgressBar;

        private readonly float coverWidth;
        private readonly float radius;
        private readonly float titleSize;
        private readonly float artistSize;

        private Drawable idleContent = null!;
        private BeatmapCardDownloadProgressBar downloadProgressBar = null!;

        // The favourite, discussion, download and go-to buttons, for the right-click menu too.
        private readonly List<BeatmapCardIconButton> actionButtons = new List<BeatmapCardIconButton>();

        private Drawable? playOverlay;
        private PlayButton? playButton;
        private Drawable? hoverActions;

        [Resolved]
        private OverlayColourProvider colours { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        public WebBeatmapCard(APIBeatmapSet beatmapSet, BeatmapListingView view)
            : base(beatmapSet, false)
        {
            View = view;

            switch (view)
            {
                case BeatmapListingView.Nano:
                    Height = 32;
                    coverWidth = 48;
                    radius = 6;
                    titleSize = 13;
                    artistSize = 11;
                    break;

                case BeatmapListingView.Mini:
                    Height = 60;
                    coverWidth = 80;
                    radius = 10;
                    titleSize = 14;
                    artistSize = 12;
                    break;

                case BeatmapListingView.List:
                    Height = 40;
                    coverWidth = 80;
                    radius = 6;
                    titleSize = 14;
                    artistSize = 12;
                    break;

                case BeatmapListingView.Cover:
                    Height = 200;
                    radius = 10;
                    titleSize = 18;
                    artistSize = 14;
                    break;

                default:
                    Height = 100;
                    coverWidth = 100;
                    radius = 10;
                    titleSize = 18;
                    artistSize = 14;
                    break;
            }

            Width = WIDTH;
            Action = DefaultAction;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            downloadProgressBar = new BeatmapCardDownloadProgressBar
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 4,
                State = { BindTarget = DownloadTracker.State },
                Progress = { BindTarget = DownloadTracker.Progress },
            };

            idleContent = View == BeatmapListingView.Cover ? createCover() : createRow();

            // Nano shows no buttons, but its right-click menu has them (hidden ones).
            Drawable nanoActions = View == BeatmapListingView.Nano ? createActions(FillDirection.Horizontal, true) : Empty();

            Children = new[]
            {
                idleContent,
                nanoActions,
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Left = coverWidth + radius, Right = radius },
                    Child = downloadProgressBar,
                },
            };
        }

        protected override void Update()
        {
            base.Update();
            FitToColumn(this, View);
        }

        /// <summary>
        /// Makes a card as wide as a column of its listing (the listing's width shared by its columns and gaps).
        /// </summary>
        public static void FitToColumn(BeatmapCard card, BeatmapListingView view)
        {
            if (card.Parent is not FillFlowContainer<BeatmapCard> flow)
                return;

            int columns = flow.ChildSize.X < 700 ? 1 : ColumnsFor(view);
            float width = (flow.ChildSize.X - flow.Spacing.X * (columns - 1)) / columns - 0.01f;

            if (card.Width != width)
                card.Width = width;
        }

        // The game's extra card (the website's "Extra" is its old panel), as wide as the listing's columns too.
        private partial class ExtraCard : BeatmapCardExtra
        {
            public ExtraCard(APIBeatmapSet set)
                : base(set)
            {
            }

            protected override void Update()
            {
                base.Update();
                FitToColumn(this, BeatmapListingView.Extra);
            }
        }

        // As lazer's cards: "View beatmap", then what its buttons do (favourite, discussion, download or go to it).
        public override MenuItem[] ContextMenuItems
        {
            get
            {
                var items = base.ContextMenuItems.ToList();

                foreach (var button in actionButtons)
                {
                    if (button.Enabled.Value && button.Action != null && !string.IsNullOrEmpty(button.TooltipText.ToString()))
                        items.Add(new OsuMenuItem(button.TooltipText.ToSentence(), MenuItemType.Standard, () => button.TriggerClick()));
                }

                return items.ToArray();
            }
        }

        protected override void UpdateState()
        {
            bool hovered = IsHovered;
            bool playing = playButton?.Playing.Value == true;

            playOverlay?.FadeTo(hovered || playing ? 1 : 0, 120);
            hoverActions?.FadeTo(hovered ? 1 : 0, 120);

            bool showProgress = DownloadTracker.State.Value == DownloadState.Downloading || DownloadTracker.State.Value == DownloadState.Importing;
            downloadProgressBar.FadeTo(showProgress ? 1 : 0, TRANSITION_DURATION, Easing.OutQuint);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            playButton?.Playing.BindValueChanged(_ => UpdateState(), true);
        }

        #region Rows (Nano, Mini, Normal, List)

        private Drawable createRow()
        {
            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new[]
                {
                    // The list cover, rounded at its start (the info box overlaps it by its own rounding).
                    new Container
                    {
                        Width = coverWidth + radius,
                        RelativeSizeAxes = Axes.Y,
                        Masking = true,
                        CornerRadius = radius,
                        Children = new Drawable[]
                        {
                            new UpdateableOnlineBeatmapSetCover(BeatmapSetCoverType.List)
                            {
                                RelativeSizeAxes = Axes.Both,
                                OnlineInfo = BeatmapSet,
                            },
                            createPlayOverlay(coverWidth, 62),
                            createCornerIcons(),
                        }
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = coverWidth },
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = radius,
                            Children = new[]
                            {
                                createInfoBackground(),
                                createRowContent(),
                            }
                        }
                    },
                }
            };
        }

        // The full card cover behind the info, faded into the info's colour (solid at the start, 70% at the end).
        private Drawable createInfoBackground() => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Background4,
                },
                new UpdateableOnlineBeatmapSetCover(BeatmapSetCoverType.Card)
                {
                    RelativeSizeAxes = Axes.Both,
                    OnlineInfo = BeatmapSet,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientHorizontal(colours.Background4, colours.Background4.Opacity(0.7f)),
                },
            }
        };

        private Drawable createRowContent()
        {
            switch (View)
            {
                case BeatmapListingView.Nano:
                    return new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 8 },
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 8),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new WebTitleArtistLine(BeatmapSet, titleSize, artistSize)
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                                Empty(),
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(6, 0),
                                    Children = new Drawable[]
                                    {
                                        new WebStatusPill(BeatmapSet.Status, 10, 12, 5)
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                        },
                                        createMapper(false).With(m =>
                                        {
                                            m.Anchor = Anchor.CentreLeft;
                                            m.Origin = Anchor.CentreLeft;
                                        }),
                                    }
                                },
                            }
                        }
                    };

                case BeatmapListingView.Mini:
                    return new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding { Horizontal = 10 },
                                Direction = FillDirection.Vertical,
                                Children = new[]
                                {
                                    new WebTitleArtistLine(BeatmapSet, titleSize, artistSize),
                                    createMapper(true),
                                    createStatusLine(new Vector2(3, 6), 13),
                                }
                            },
                            hoverActions = createActions(FillDirection.Horizontal, true),
                        }
                    };

                case BeatmapListingView.List:
                    return new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Absolute, 430),
                            new Dimension(GridSizeMode.Absolute, 80),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new[]
                            {
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding { Horizontal = 10 },
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        new WebTitleArtistLine(BeatmapSet, titleSize, artistSize),
                                        new FillFlowContainer
                                        {
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(6, 0),
                                            Children = new[]
                                            {
                                                new WebStatusPill(BeatmapSet.Status)
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                },
                                                createMapper(true, true).With(m =>
                                                {
                                                    m.Anchor = Anchor.CentreLeft;
                                                    m.Origin = Anchor.CentreLeft;
                                                }),
                                            }
                                        },
                                    }
                                },
                                createExplicit(),
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 2),
                                    Masking = true,
                                    Children = new Drawable[]
                                    {
                                        new WebDifficultyBars(BeatmapSet, new Vector2(3, 6), 13),
                                        createStats(false, @"MMM d, yyyy"),
                                    }
                                },
                                createActions(FillDirection.Horizontal, false),
                            }
                        }
                    };

                default:
                    return new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Top = 4, Bottom = 6, Horizontal = 10 },
                                Children = new[]
                                {
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Children = new[]
                                        {
                                            new TruncatingSpriteText
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Text = new RomanisableString(BeatmapSet.TitleUnicode, BeatmapSet.Title),
                                                Font = OsuFont.Default.With(size: titleSize, weight: FontWeight.SemiBold),
                                            },
                                            new TruncatingSpriteText
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Text = new RomanisableString(BeatmapSet.ArtistUnicode, BeatmapSet.Artist),
                                                Font = OsuFont.Default.With(size: artistSize, weight: FontWeight.SemiBold),
                                            },
                                            createMapper(true),
                                        }
                                    },
                                    createStatusLine(new Vector2(4, 8), 16).With(l =>
                                    {
                                        l.Anchor = Anchor.BottomLeft;
                                        l.Origin = Anchor.BottomLeft;
                                    }),
                                }
                            },
                            hoverActions = createActions(FillDirection.Vertical, true),
                        }
                    };
            }
        }

        // The status pill and the difficulties.
        private Drawable createStatusLine(Vector2 barSize, int maxBars) => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(6, 0),
            Children = new Drawable[]
            {
                new WebStatusPill(BeatmapSet.Status)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                new WebDifficultyBars(BeatmapSet, barSize, maxBars)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
            }
        };

        // A black "EXPLICIT" pill for explicit maps, at the end of its 80 wide column.
        private Drawable createExplicit()
        {
            if (!BeatmapSet.HasExplicitContent)
                return Empty();

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Right = 10 },
                Child = new CircularContainer
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    AutoSizeAxes = Axes.X,
                    Height = 14,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Margin = new MarginPadding { Horizontal = 8 },
                            Text = BeatmapsetsStrings.NsfwBadgeLabel.ToUpper(),
                            Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                            UseFullGlyphHeight = false,
                        },
                    }
                }
            };
        }

        #endregion

        #region Cover

        private Drawable createCover()
        {
            Container titleClamp;
            OsuTextFlowContainer title;

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = radius,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Background4,
                    },
                    new Container
                    {
                        Name = @"Top",
                        RelativeSizeAxes = Axes.X,
                        Height = 140,
                        Children = new[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                CornerRadius = 6,
                                Child = new UpdateableOnlineBeatmapSetCover(BeatmapSetCoverType.Card)
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    OnlineInfo = BeatmapSet,
                                },
                            },
                            createPlayOverlay(null, 100),
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.1f), Color4.Black.Opacity(0.6f)),
                            },
                            new FillFlowContainer
                            {
                                Position = new Vector2(10),
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(4, 0),
                                Children = new Drawable[]
                                {
                                    createIcon(FontAwesome.Solid.Film, BeatmapSet.HasVideo, new Vector2(20, 14), 5),
                                    createIcon(FontAwesome.Solid.Image, BeatmapSet.HasStoryboard, new Vector2(20, 14), 5),
                                    new WebStatusPill(BeatmapSet.Status),
                                }
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Position = new Vector2(-10, 10),
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Children = new[]
                                {
                                    createDate(@"MM/dd/yyyy", true).With(d =>
                                    {
                                        d.Anchor = Anchor.TopRight;
                                        d.Origin = Anchor.TopRight;
                                    }),
                                    createStat(FontAwesome.Solid.Heart, BeatmapSet.FavouriteCount, BeatmapsStrings.PanelFavourites(BeatmapSet.FavouriteCount.ToLocalisableString(@"N0")), true)
                                        .With(d =>
                                        {
                                            d.Anchor = Anchor.TopRight;
                                            d.Origin = Anchor.TopRight;
                                        }),
                                    createStat(FontAwesome.Solid.Play, BeatmapSet.PlayCount, BeatmapsStrings.PanelPlaycount(BeatmapSet.PlayCount.ToLocalisableString(@"N0")), true)
                                        .With(d =>
                                        {
                                            d.Anchor = Anchor.TopRight;
                                            d.Origin = Anchor.TopRight;
                                        }),
                                }
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding { Horizontal = 10, Bottom = 6 },
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    // At most two lines.
                                    titleClamp = new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Masking = true,
                                        Child = title = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: titleSize, weight: FontWeight.SemiBold))
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Text = new RomanisableString(BeatmapSet.TitleUnicode, BeatmapSet.Title),
                                        },
                                    },
                                    new TruncatingSpriteText
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Text = new RomanisableString(BeatmapSet.ArtistUnicode, BeatmapSet.Artist),
                                        Font = OsuFont.Default.With(size: artistSize, weight: FontWeight.SemiBold),
                                    },
                                }
                            },
                            new TwoLineClamp(titleClamp, title, titleSize * 1.25f * 2),
                        }
                    },
                    new Container
                    {
                        Name = @"Bottom",
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 60,
                        Child = new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            ColumnDimensions = new[]
                            {
                                new Dimension(),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            Content = new[]
                            {
                                new[]
                                {
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Padding = new MarginPadding { Horizontal = 10, Vertical = 4 },
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 2),
                                        Masking = true,
                                        Children = new[]
                                        {
                                            createMapper(true),
                                            string.IsNullOrEmpty(BeatmapSet.Source)
                                                ? Empty()
                                                : new TruncatingSpriteText
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Text = BeatmapSet.Source,
                                                    Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                                                    Colour = colours.Content2,
                                                },
                                            new WebDifficultyBars(BeatmapSet, new Vector2(8, 12), 20, 3),
                                        }
                                    },
                                    createActions(FillDirection.Horizontal, false, 10, 12),
                                }
                            }
                        }
                    },
                }
            };
        }

        // Keeps the cover's title to two lines.
        private partial class TwoLineClamp : Component
        {
            private readonly Container clamp;
            private readonly Drawable text;
            private readonly float maxHeight;

            public TwoLineClamp(Container clamp, Drawable text, float maxHeight)
            {
                this.clamp = clamp;
                this.text = text;
                this.maxHeight = maxHeight;
            }

            protected override void Update()
            {
                base.Update();
                clamp.Height = Math.Min(text.DrawHeight, maxHeight);
            }
        }

        #endregion

        #region Pieces

        // The preview button over the cover, shown under the mouse or while playing (none on nano).
        private Drawable createPlayOverlay(float? width, float buttonSize)
        {
            if (View == BeatmapListingView.Nano)
                return Empty();

            var overlay = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                AlwaysPresent = true,
                Children = new Drawable[]
                {
                    View == BeatmapListingView.Cover
                        ? Empty()
                        : new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.5f),
                        },
                    playButton = new PlayButton(BeatmapSet)
                    {
                        Anchor = View == BeatmapListingView.Cover ? Anchor.TopCentre : Anchor.Centre,
                        Origin = View == BeatmapListingView.Cover ? Anchor.TopCentre : Anchor.Centre,
                        Y = View == BeatmapListingView.Cover ? 30 : 0,
                        Size = new Vector2(buttonSize),
                    },
                }
            };

            if (width != null)
            {
                overlay.RelativeSizeAxes = Axes.Y;
                overlay.Width = width.Value;
            }

            return playOverlay = overlay;
        }

        // The video and storyboard circles at the cover's corner (mini: spread top and bottom).
        private Drawable createCornerIcons()
        {
            if (View != BeatmapListingView.Mini && View != BeatmapListingView.Normal)
                return Empty();

            if (View == BeatmapListingView.Mini)
            {
                return new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = 5, Left = 5, Bottom = 4 },
                    Children = new[]
                    {
                        createIcon(FontAwesome.Solid.Film, BeatmapSet.HasVideo, new Vector2(20), 10),
                        createIcon(FontAwesome.Solid.Image, BeatmapSet.HasStoryboard, new Vector2(20), 10).With(i =>
                        {
                            i.Anchor = Anchor.BottomLeft;
                            i.Origin = Anchor.BottomLeft;
                        }),
                    }
                };
            }

            return new FillFlowContainer
            {
                Position = new Vector2(5),
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Children = new[]
                {
                    createIcon(FontAwesome.Solid.Film, BeatmapSet.HasVideo, new Vector2(20), 10),
                    createIcon(FontAwesome.Solid.Image, BeatmapSet.HasStoryboard, new Vector2(20), 10),
                }
            };
        }

        private Drawable createIcon(IconUsage icon, bool shown, Vector2 size, float cornerRadius)
        {
            if (!shown)
                return Empty();

            return new IconCircle(icon.Equals(FontAwesome.Solid.Film) ? BeatmapsetsStrings.ShowInfoVideo : BeatmapsetsStrings.ShowInfoStoryboard)
            {
                Size = size,
                Masking = true,
                CornerRadius = cornerRadius,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.5f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(10),
                        Icon = icon,
                    },
                }
            };
        }

        private partial class IconCircle : Container, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public IconCircle(LocalisableString tooltip)
            {
                TooltipText = tooltip;
            }
        }

        // "mapped by {name}" (12, the name a link); nano's is only the name, at most 40% wide.
        private Drawable createMapper(bool withLabel, bool autoWidth = false)
        {
            var flow = new LinkFlowContainer(t =>
            {
                t.Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold);
                t.Colour = colours.Content2;
            })
            {
                AutoSizeAxes = Axes.Both,
            };

            if (withLabel)
                flow.AddText(@"mapped by ");

            Overlays.BeatmapSet.SetPersonDisplay.AddName(flow, SetPerson.CreatorOf(BeatmapSet), t => t.Colour = colours.Light1);

            if (!withLabel)
                return new Container
                {
                    AutoSizeAxes = Axes.Y,
                    Width = 120,
                    Masking = true,
                    Child = flow,
                };

            if (autoWidth)
                return flow;

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                Child = flow,
            };
        }

        // Favourites, plays and the date (12, the icons 10).
        private Drawable createStats(bool shadow, string dateFormat) => new FillFlowContainer
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(12, 0),
            Children = new[]
            {
                createStat(FontAwesome.Solid.Heart, BeatmapSet.FavouriteCount, BeatmapsStrings.PanelFavourites(BeatmapSet.FavouriteCount.ToLocalisableString(@"N0")), shadow),
                createStat(FontAwesome.Solid.Play, BeatmapSet.PlayCount, BeatmapsStrings.PanelPlaycount(BeatmapSet.PlayCount.ToLocalisableString(@"N0")), shadow),
                createDate(dateFormat, shadow),
            }
        };

        private Drawable createStat(IconUsage icon, int value, LocalisableString tooltip, bool shadow) => new Stat(tooltip)
        {
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(4, 0),
            Children = new Drawable[]
            {
                new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(10),
                    Icon = icon,
                    Shadow = shadow,
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = value.ToMetric(decimals: 1),
                    Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                    Shadow = shadow,
                },
            }
        };

        // The ranked date (or last updated) with a check: before it on the cover's corner, after it elsewhere.
        private Drawable createDate(string format, bool iconAfter)
        {
            var date = WebBeatmapStyle.ShownDate(BeatmapSet);

            if (date == null)
                return Empty();

            var icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(10),
                Icon = FontAwesome.Solid.CheckCircle,
                Shadow = iconAfter,
            };

            var text = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = date.Value.ToLocalTime().ToString(format),
                Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                Shadow = iconAfter,
            };

            return new Stat(date.Value.ToLocalTime().ToString(@"dd MMMM yyyy"))
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(4, 0),
                Children = iconAfter ? new Drawable[] { text, icon } : new Drawable[] { icon, text },
            };
        }

        private partial class Stat : FillFlowContainer, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public Stat(LocalisableString tooltip)
            {
                TooltipText = tooltip;
            }
        }

        // Favourite, discussion and download (14, white, lighter under the mouse). Normal and mini show them under the mouse only, at the end
        // over a dark fade; list and cover always.
        private Drawable createActions(FillDirection direction, bool onHover, float spacing = 8, float horizontalPadding = 14)
        {
            var buttons = new FillFlowContainer
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                AutoSizeAxes = Axes.Both,
                Direction = direction,
                Spacing = new Vector2(onHover ? 8 : spacing),
                Children = new Drawable[]
                {
                    button(new FavouriteButton(BeatmapSet) { Current = FavouriteState }),
                    button(new DiscussionButton(BeatmapSet, game)),
                    new Container
                    {
                        Size = new Vector2(22),
                        Children = new Drawable[]
                        {
                            styled(new DownloadButton(BeatmapSet) { State = { BindTarget = DownloadTracker.State } }),
                            styled(new GoToBeatmapButton(BeatmapSet, true) { State = { BindTarget = DownloadTracker.State } }),
                        }
                    },
                }
            };

            if (onHover)
            {
                return new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new ColourInfo
                            {
                                TopLeft = Color4.Transparent,
                                BottomLeft = Color4.Transparent,
                                TopRight = colours.Background6.Opacity(0.8f),
                                BottomRight = colours.Background6.Opacity(0.8f),
                            },
                        },
                        new Container
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            AutoSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Left = 24, Right = 10 },
                            Child = buttons,
                        },
                    }
                };
            }

            return new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                RelativeSizeAxes = Axes.Y,
                AutoSizeAxes = Axes.X,
                Children = new Drawable[]
                {
                    View == BeatmapListingView.List
                        ? new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colours.Background6.Opacity(0.5f),
                        }
                        : Empty(),
                    new Container
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        AutoSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = horizontalPadding },
                        Child = buttons,
                    },
                }
            };

            Drawable button(BeatmapCardIconButton b) => new Container
            {
                Size = new Vector2(22),
                Child = styled(b),
            };

            BeatmapCardIconButton styled(BeatmapCardIconButton b)
            {
                actionButtons.Add(b);

                // After the button's own colours.
                b.OnLoadComplete += _ =>
                {
                    b.IdleColour = colours.Content1;
                    b.HoverColour = colours.Light1;
                };
                return b;
            }
        }

        private partial class DiscussionButton : BeatmapCardIconButton
        {
            public DiscussionButton(APIBeatmapSet set, OsuGame? game)
            {
                Action = () => game?.OpenUrlExternally($@"/beatmapsets/{set.OnlineID}/discussion");
                TooltipText = BeatmapsetsStrings.ShowDiscussion;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Icon.Icon = FontAwesome.Solid.CommentAlt;
            }
        }

        #endregion
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Beatmaps.Drawables.Cards.Buttons;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Overlays.Notifications;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Users.Drawables;
using osu.Game.Utils;
using osu.Framework.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.BeatmapSet
{
    /// <summary>
    /// The website's beatmap page header (osu-web after osu!'s "Beatmap Info" design proposal): the cover (collapsible) with the status,
    /// the title row, the difficulty band, the details in columns, the credits and the bar with the counts and buttons.
    /// </summary>
    public partial class BeatmapSetHeaderContent : CompositeDrawable
    {
        public readonly Bindable<APIBeatmapSet?> BeatmapSet = new Bindable<APIBeatmapSet?>();

        /// <summary>
        /// The website's side gutter.
        /// </summary>
        private const float gutter = 50;

        private const float cover_collapsed_height = 53;
        private const float cover_expanded_height = 250;

        public bool DownloadButtonsVisible => downloadButtonsContainer.Any();

        public readonly BeatmapPicker Picker;

        private readonly Container cover;
        private readonly UpdateableOnlineBeatmapSetCover coverImage;
        private readonly FillFlowContainer coverPills;
        private readonly FillFlowContainer coverButtons;
        private readonly BeatmapSetOnlineStatusPill onlineStatusPill;
        private readonly Container contentArea;
        private readonly LinkFlowContainer title;
        private readonly FillFlowContainer createdBy;
        private readonly BeatmapAvailability beatmapAvailability;
        private readonly Box bandBackground;
        private readonly Box bandBorder;
        private readonly WebFailStrip failStrip;
        private readonly Container details;
        private readonly Container numbers;
        private readonly Container ratings;
        private readonly FillFlowContainer credits;
        private readonly FillFlowContainer footerCounts;
        private readonly FillFlowContainer footerButtons;
        private readonly FillFlowContainer downloadButtonsContainer;
        private readonly LoadingSpinner loading;

        private readonly BindableBool coverExpanded = new BindableBool();

        private BeatmapDownloadTracker? downloadTracker;
        private readonly Bindable<BeatmapSetFavouriteState> favouriteState = new Bindable<BeatmapSetFavouriteState>();

        [Resolved]
        private BeatmapRulesetSelector rulesetSelector { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colours { get; set; } = null!;

        [Resolved]
        private OsuColour osuColours { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private BeatmapModelDownloader? downloader { get; set; }

        private Bindable<bool> preferNoVideo = null!;

        public BeatmapSetHeaderContent()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChildren = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        cover = new Container
                        {
                            Name = @"Cover",
                            RelativeSizeAxes = Axes.X,
                            Height = cover_collapsed_height,
                            Masking = true,
                            Children = new Drawable[]
                            {
                                coverImage = new UpdateableOnlineBeatmapSetCover
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                coverPills = new FillFlowContainer
                                {
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(5, 0),
                                    Margin = new MarginPadding { Left = gutter },
                                    Child = onlineStatusPill = new BeatmapSetOnlineStatusPill
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        TextSize = 10,
                                        TextPadding = new MarginPadding { Horizontal = 18, Vertical = 4 },
                                    },
                                },
                                coverButtons = new FillFlowContainer
                                {
                                    Anchor = Anchor.BottomRight,
                                    Origin = Anchor.BottomRight,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(5, 0),
                                    Margin = new MarginPadding { Right = 10 },
                                },
                            }
                        },
                        contentArea = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Child = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    new Container
                                    {
                                        Name = @"Title row",
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Children = new Drawable[]
                                        {
                                            new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Colour = Color4.Black,
                                            }.With(b => titleBackground = b),
                                            new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Direction = FillDirection.Vertical,
                                                Spacing = new Vector2(0, 2),
                                                Padding = new MarginPadding { Top = 10, Bottom = 8, Horizontal = gutter },
                                                Children = new Drawable[]
                                                {
                                                    title = new MetadataFlowContainer(s => s.Font = OsuFont.GetFont(size: 24)),
                                                    createdBy = new FillFlowContainer
                                                    {
                                                        AutoSizeAxes = Axes.Both,
                                                        Direction = FillDirection.Horizontal,
                                                        Spacing = new Vector2(4, 0),
                                                    },
                                                }
                                            },
                                        }
                                    },
                                    beatmapAvailability = new BeatmapAvailability(),
                                    new Container
                                    {
                                        Name = @"Difficulty band",
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Children = new Drawable[]
                                        {
                                            bandBackground = new Box { RelativeSizeAxes = Axes.Both },
                                            bandBorder = new Box
                                            {
                                                Anchor = Anchor.BottomLeft,
                                                Origin = Anchor.BottomLeft,
                                                RelativeSizeAxes = Axes.X,
                                                Height = 2,
                                            },
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Padding = new MarginPadding { Top = 10, Horizontal = gutter },
                                                Child = Picker = new BeatmapPicker(),
                                            },
                                        }
                                    },
                                    new Container
                                    {
                                        Name = @"Body",
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Children = new Drawable[]
                                        {
                                            new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Colour = Color4.Black,
                                            }.With(b => bodyBackground = b),
                                            new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Direction = FillDirection.Vertical,
                                                Padding = new MarginPadding { Top = 11, Bottom = 12, Horizontal = gutter },
                                                Children = new Drawable[]
                                                {
                                                    failStrip = new WebFailStrip(),
                                                    new GridContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Margin = new MarginPadding { Top = 13 },
                                                        ColumnDimensions = new[]
                                                        {
                                                            new Dimension(GridSizeMode.Absolute, 268),
                                                            new Dimension(GridSizeMode.Absolute, 23),
                                                            new Dimension(GridSizeMode.Absolute, 2),
                                                            new Dimension(GridSizeMode.Absolute, 23),
                                                            new Dimension(GridSizeMode.Absolute, 288),
                                                            new Dimension(GridSizeMode.Absolute, 23),
                                                            new Dimension(),
                                                        },
                                                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                                        Content = new[]
                                                        {
                                                            new Drawable[]
                                                            {
                                                                details = new Container { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y },
                                                                Empty(),
                                                                new Circle
                                                                {
                                                                    Width = 2,
                                                                    Height = 150,
                                                                }.With(c => divider = c),
                                                                Empty(),
                                                                numbers = new Container { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y },
                                                                Empty(),
                                                                ratings = new Container { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y },
                                                            }
                                                        }
                                                    },
                                                    new Container
                                                    {
                                                        Name = @"Credits",
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Margin = new MarginPadding { Top = 15 },
                                                        Children = new Drawable[]
                                                        {
                                                            new Box
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Height = 2,
                                                            }.With(b => creditsLine = b),
                                                            credits = new FillFlowContainer
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                AutoSizeAxes = Axes.Y,
                                                                Direction = FillDirection.Horizontal,
                                                                Spacing = new Vector2(40, 10),
                                                                Padding = new MarginPadding { Top = 9 },
                                                            },
                                                            creditDates = new Container
                                                            {
                                                                Anchor = Anchor.TopRight,
                                                                Origin = Anchor.TopRight,
                                                                AutoSizeAxes = Axes.Both,
                                                                Padding = new MarginPadding { Top = 9 },
                                                            },
                                                        }
                                                    },
                                                }
                                            },
                                        }
                                    },
                                    new Container
                                    {
                                        Name = @"Footer",
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Children = new Drawable[]
                                        {
                                            new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Colour = Color4.Black,
                                            }.With(b => footerBackground = b),
                                            new GridContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Padding = new MarginPadding { Vertical = 8, Horizontal = gutter },
                                                ColumnDimensions = new[]
                                                {
                                                    new Dimension(),
                                                    new Dimension(GridSizeMode.AutoSize),
                                                },
                                                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                                Content = new[]
                                                {
                                                    new Drawable[]
                                                    {
                                                        footerCounts = new FillFlowContainer
                                                        {
                                                            Anchor = Anchor.CentreLeft,
                                                            Origin = Anchor.CentreLeft,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Horizontal,
                                                            Spacing = new Vector2(40, 10),
                                                        },
                                                        footerButtons = new FillFlowContainer
                                                        {
                                                            Anchor = Anchor.CentreRight,
                                                            Origin = Anchor.CentreRight,
                                                            AutoSizeAxes = Axes.Both,
                                                            Direction = FillDirection.Horizontal,
                                                            Spacing = new Vector2(5, 0),
                                                        },
                                                    }
                                                }
                                            },
                                        }
                                    },
                                }
                            }
                        },
                    }
                },
                loading = new LoadingSpinner
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.Centre,
                    Y = 150,
                    Scale = new Vector2(1.5f),
                },
            };

            downloadButtonsContainer = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5, 0),
            };

            Picker.Beatmap.ValueChanged += b => onlineStatusPill.Status = b.NewValue?.Status ?? BeatmapOnlineStatus.None;
            Picker.ShownBeatmap.ValueChanged += _ => updateBeatmap();
        }

        private Container creditDates = null!;
        private Box titleBackground = null!;
        private Box bodyBackground = null!;
        private Box footerBackground = null!;
        private Box creditsLine = null!;
        private Circle divider = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            preferNoVideo = config.GetBindable<bool>(OsuSetting.PreferNoVideo);

            titleBackground.Colour = colours.Background4;
            bodyBackground.Colour = colours.Background5;
            footerBackground.Colour = colours.Background3;
            creditsLine.Colour = colours.Background6;
            divider.Colour = colours.Background3;

            coverExpanded.BindValueChanged(e =>
            {
                cover.ResizeHeightTo(e.NewValue ? cover_expanded_height : cover_collapsed_height, 200, Easing.OutQuint);
                updateCoverRowPositions(e.NewValue);
            }, true);

            BeatmapSet.BindValueChanged(setInfo => onSetChanged(setInfo.NewValue), true);
        }

        // Collapsed, the cover's row sits in its middle; expanded, along its bottom.
        private void updateCoverRowPositions(bool expanded)
        {
            foreach (var row in new Drawable[] { coverPills, coverButtons })
            {
                row.Anchor = expanded ? row.Anchor & ~Anchor.y1 | Anchor.y2 : row.Anchor & ~Anchor.y2 | Anchor.y1;
                row.Origin = row.Anchor;
                row.Y = expanded ? -11 : 0;
            }
        }

        private void onSetChanged(APIBeatmapSet? set)
        {
            Picker.BeatmapSet = rulesetSelector.BeatmapSet = beatmapAvailability.BeatmapSet = set;
            coverImage.OnlineInfo = set;

            downloadTracker?.RemoveAndDisposeImmediately();
            downloadTracker = null;

            if (set == null)
            {
                contentArea.FadeOut(200);
                coverPills.FadeOut(200);
                coverButtons.FadeOut(200);
                loading.Show();
                downloadButtonsContainer.Clear();
                return;
            }

            loading.Hide();
            contentArea.FadeIn(500, Easing.OutQuint);
            coverPills.FadeIn(500, Easing.OutQuint);
            coverButtons.FadeIn(500, Easing.OutQuint);

            favouriteState.Value = new BeatmapSetFavouriteState(set.HasFavourited, set.FavouriteCount);

            downloadTracker = new BeatmapDownloadTracker(set);
            downloadTracker.State.BindValueChanged(_ => updateButtons());
            AddInternal(downloadTracker);

            updateCover(set);
            updateTitle(set);
            updateCredits(set);
            updateButtons();
            updateBeatmap();
        }

        #region Cover

        private void updateCover(APIBeatmapSet set)
        {
            coverPills.RemoveAll(d => d != onlineStatusPill, true);

            if (set.HasExplicitContent)
                coverPills.Add(new CoverPill(BeatmapsetsStrings.NsfwBadgeLabel.ToUpper(), Color4.Black));

            if (set.HasVideo)
                coverPills.Add(new CoverIcon(FontAwesome.Solid.Film, BeatmapsetsStrings.ShowInfoVideo, colours));

            if (set.HasStoryboard)
                coverPills.Add(new CoverIcon(FontAwesome.Solid.Image, BeatmapsetsStrings.ShowInfoStoryboard, colours));

            coverButtons.Children = new Drawable[]
            {
                new PreviewPill(set, colours),
                new CollapseToggle(colours) { Expanded = { BindTarget = coverExpanded } },
            };
        }

        private partial class CoverPill : CircularContainer
        {
            public CoverPill(LocalisableString text, Color4 background)
            {
                Anchor = Anchor.CentreLeft;
                Origin = Anchor.CentreLeft;
                AutoSizeAxes = Axes.Both;
                Masking = true;

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = background,
                    },
                    new OsuSpriteText
                    {
                        Text = text,
                        Font = OsuFont.GetFont(size: 10, weight: FontWeight.Bold),
                        Padding = new MarginPadding { Horizontal = 12, Vertical = 4 },
                    },
                };
            }
        }

        // A 38 round icon on the dark (the video and storyboard).
        private partial class CoverIcon : CircularContainer, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public CoverIcon(IconUsage icon, LocalisableString tooltip, OverlayColourProvider colours)
            {
                Anchor = Anchor.CentreLeft;
                Origin = Anchor.CentreLeft;
                Size = new Vector2(38);
                Masking = true;
                TooltipText = tooltip;

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Background4.Opacity(0.7f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(14),
                        Icon = icon,
                    },
                };
            }
        }

        // The preview: a 100x30 pill playing the set's preview, its progress in yellow along its bottom.
        private partial class PreviewPill : CircularContainer
        {
            private readonly PlayButton playButton;
            private readonly Box progress;

            public PreviewPill(APIBeatmapSet set, OverlayColourProvider colours)
            {
                Size = new Vector2(100, 30);
                Masking = true;

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Background5.Opacity(0.8f),
                    },
                    playButton = new PlayButton(set)
                    {
                        Size = new Vector2(62, 30),
                    },
                    progress = new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 3,
                        Width = 0,
                        Colour = WebBeatmapSetColours.YELLOW,
                    },
                };
            }

            protected override void Update()
            {
                base.Update();
                progress.Width = (float)playButton.Progress.Value;
            }
        }

        private partial class CollapseToggle : OsuClickableContainer
        {
            public readonly BindableBool Expanded = new BindableBool();

            private readonly SpriteIcon icon;

            public CollapseToggle(OverlayColourProvider colours)
            {
                Size = new Vector2(30);
                Masking = true;
                CornerRadius = 15;
                Action = Expanded.Toggle;

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Background5.Opacity(0.8f),
                    },
                    icon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(12),
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Expanded.BindValueChanged(e => icon.Icon = e.NewValue ? FontAwesome.Solid.ChevronUp : FontAwesome.Solid.ChevronDown, true);
            }
        }

        #endregion

        #region Title and credits

        private void updateTitle(APIBeatmapSet set)
        {
            var titleText = new RomanisableString(set.TitleUnicode, set.Title);
            var artistText = new RomanisableString(set.ArtistUnicode, set.Artist);

            title.Clear();
            title.AddLink(titleText, LinkAction.SearchBeatmapSet, LocalisableString.Interpolate($@"title=""""{titleText}"""""));
            title.AddText(@" ");
            title.AddLink(artistText, LinkAction.SearchBeatmapSet, LocalisableString.Interpolate($@"artist=""""{artistText}"""""), null, t => t.Font = OsuFont.GetFont(size: 18));

            if (set.TrackId != null)
            {
                title.AddArbitraryDrawable(Empty().With(d => d.Width = 10));
                title.AddArbitraryDrawable(new FeaturedArtistBeatmapBadge());
            }

            if (set.FeaturedInSpotlight)
            {
                title.AddArbitraryDrawable(Empty().With(d => d.Width = 10));
                title.AddArbitraryDrawable(new SpotlightBeatmapBadge());
            }

            // "created by", the avatar and the name, all centred on the line.
            createdBy.Clear();
            createdBy.Add(new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = @"created by",
                Font = OsuFont.GetFont(size: 12),
                Colour = colours.Content2,
            });

            var creator = SetPerson.CreatorOf(set);

            createdBy.Add(SetPersonDisplay.Avatar(creator, 15).With(a =>
            {
                a.Anchor = Anchor.CentreLeft;
                a.Origin = Anchor.CentreLeft;
                // Level with the text's letters (its box has room above them).
                a.Y = 1;
            }));

            var name = new LinkFlowContainer(t =>
            {
                t.Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold);
                t.Colour = colours.Light1;
            })
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
            };

            SetPersonDisplay.AddName(name, creator);
            createdBy.Add(name);
        }

        // "mapped by" and "Nominated by" with their avatars, the dates at the end.
        private void updateCredits(APIBeatmapSet set)
        {
            credits.Clear();

            credits.Add(credit(@"mapped by", new[] { SetPerson.CreatorOf(set) }));

            var nominators = SetPerson.NominatorsOf(set);

            if (nominators.Length > 0)
                credits.Add(credit(@"Nominated by", nominators));

            var dates = new FillFlowContainer
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Children = new[]
                {
                    date(@"Submitted on", set.Submitted),
                    set.Ranked != null && set.Status > BeatmapOnlineStatus.Pending
                        ? date(LocalisableString.Interpolate($"{set.Status.GetLocalisableDescription()} on"), set.Ranked)
                        : date(@"Updated on", set.LastUpdated),
                }
            };

            creditDates.Child = dates;
        }

        private Drawable credit(LocalisableString label, SetPerson[] users)
        {
            var avatars = new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(2, 0),
            };

            foreach (var user in users)
                avatars.Add(SetPersonDisplay.Avatar(user, 24));

            var names = new LinkFlowContainer(t =>
            {
                t.Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold);
                t.Colour = colours.Light1;
            })
            {
                AutoSizeAxes = Axes.Both,
            };

            for (int i = 0; i < users.Length; i++)
            {
                if (i > 0)
                    names.AddText(@", ");

                SetPersonDisplay.AddName(names, users[i]);
            }

            return new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    avatars,
                    new WebLabelledValue(label, names, colours.Content2)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                }
            };
        }

        private Drawable date(LocalisableString label, DateTimeOffset? date)
        {
            var text = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold))
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                AutoSizeAxes = Axes.Both,
            };

            text.AddText(label, t => t.Colour = colours.Content2);
            text.AddText(@" ");
            text.AddText(date?.ToLocalTime().ToString(@"dd MMM yyyy") ?? @"-", t => t.Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold));
            return text;
        }

        #endregion

        #region The shown difficulty

        // The band in the difficulty's colour, its failures, details, numbers and ratings, and its play count.
        private void updateBeatmap()
        {
            var set = BeatmapSet.Value;
            var beatmap = Picker.ShownBeatmap.Value;

            if (set == null)
                return;

            Color4 diff = beatmap != null ? osuColours.ForStarDifficulty(beatmap.StarRating) : colours.Highlight1;

            bandBackground.FadeColour(ColourInfo.GradientVertical(Interpolation.ValueAt(0.4, colours.Background4, diff, 0, 1), colours.Background4), 300, Easing.OutQuint);
            bandBorder.FadeColour(diff, 300, Easing.OutQuint);

            failStrip.SetFailTimes(beatmap?.FailTimes);

            updateDetails(set, beatmap);
            updateNumbers(beatmap);
            updateRatings(set, beatmap);
            updateCounts(set, beatmap);
        }

        private void updateDetails(APIBeatmapSet set, APIBeatmap? beatmap)
        {
            var rows = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
            };

            if (!string.IsNullOrEmpty(set.Source))
                rows.Add(detail(@"Source", f => f.AddLink(set.Source, LinkAction.SearchBeatmapSet, $@"source=""""{set.Source}""""")));

            rows.Add(detail(@"Genre", f =>
            {
                var genre = (SearchGenre)set.Genre.Id;

                if (Enum.IsDefined(genre))
                    f.AddLink(genre.GetLocalisableDescription(), LinkAction.FilterBeatmapSetGenre, genre);
                else
                    f.AddText(set.Genre.Name);
            }));

            rows.Add(Empty().With(d => d.Height = 10));

            rows.Add(detail(@"Language", f =>
            {
                var language = (SearchLanguage)set.Language.Id;

                if (Enum.IsDefined(language))
                    f.AddLink(language.GetLocalisableDescription(), LinkAction.FilterBeatmapSetLanguage, language);
                else
                    f.AddText(set.Language.Name);
            }));

            // Players' tags first, then the mapper's.
            var tags = (beatmap?.GetTopUserTags().Select(t => t.Tag.Name) ?? Enumerable.Empty<string>())
                       .Concat(set.Tags.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                       .Distinct()
                       .ToArray();

            if (tags.Length > 0)
            {
                rows.Add(detail(@"Tag", f =>
                {
                    foreach (string tag in tags)
                    {
                        f.AddLink(tag, LinkAction.SearchBeatmapSet, tag);
                        f.AddText(@" ");
                    }
                }));
            }

            details.Child = rows;
        }

        private Drawable detail(LocalisableString label, Action<LinkFlowContainer> fill)
        {
            var value = new LinkFlowContainer(t => t.Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            fill(value);

            return new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 90),
                    new Dimension(GridSizeMode.Absolute, 10),
                    new Dimension(),
                },
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                Content = new[]
                {
                    new[]
                    {
                        new OsuSpriteText
                        {
                            Text = label,
                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                        },
                        Empty(),
                        value,
                    }
                }
            };
        }

        // The object counts, then the difficulty's settings as bars out of 10 (as its mode has them), the star rating last.
        private void updateNumbers(APIBeatmap? beatmap)
        {
            if (beatmap == null)
            {
                numbers.Clear();
                return;
            }

            var rows = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Margin = new MarginPadding { Top = 10 },
            };

            void stat(LocalisableString name, float value, bool starRating = false)
            {
                var row = new WebStatRow(name, starRating);
                row.SetValue(value, starRating ? ((double)value).FormatStarRating().ToString() : value.ToString(@"0.#"));
                rows.Add(row);
            }

            switch (beatmap.RulesetID)
            {
                case 1:
                    break;

                case 3:
                    stat(BeatmapsetsStrings.ShowStatsCsMania, beatmap.CircleSize);
                    break;

                default:
                    stat(BeatmapsetsStrings.ShowStatsCs, beatmap.CircleSize);
                    break;
            }

            stat(BeatmapsetsStrings.ShowStatsDrain, beatmap.DrainRate);
            stat(BeatmapsetsStrings.ShowStatsAccuracy, beatmap.OverallDifficulty);

            if (beatmap.RulesetID == 0 || beatmap.RulesetID == 2)
                stat(BeatmapsetsStrings.ShowStatsAr, beatmap.ApproachRate);

            stat(BeatmapsetsStrings.ShowStatsStars, (float)beatmap.StarRating, true);

            numbers.Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        ColumnDimensions = new[] { new Dimension(), new Dimension(), new Dimension() },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new[]
                            {
                                count(@"Circle Count", beatmap.CircleCount),
                                count(@"Slider Count", beatmap.SliderCount),
                                count(@"Spinner Count", beatmap.SpinnerCount),
                            }
                        }
                    },
                    rows,
                }
            };
        }

        private Drawable count(LocalisableString label, int value) => new WebLabelledValue(label, new OsuSpriteText
        {
            Text = value.ToLocalisableString(@"N0"),
            Font = OsuFont.GetFont(size: 20),
        }, colours.Content2);

        // The success rate, the players' rating and its spread.
        private void updateRatings(APIBeatmapSet set, APIBeatmap? beatmap)
        {
            var column = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 14),
            };

            if (beatmap != null)
            {
                if (beatmap.PlayCount > 0)
                {
                    float rate = (float)beatmap.PassCount / beatmap.PlayCount;
                    column.Add(ratingBar(BeatmapsetsStrings.ShowInfoSuccessRate, $@"{beatmap.PassCount:N0}/{beatmap.PlayCount:N0}", $@"{rate * 100:0.00}%", rate,
                        LocalisableString.Interpolate($"{beatmap.PassCount:N0} passes of {beatmap.PlayCount:N0} plays")));
                }
                else
                {
                    column.Add(new WebLabelledValue(BeatmapsetsStrings.ShowInfoSuccessRate, new OsuSpriteText
                    {
                        Text = @"Data still being calculated...",
                        Font = OsuFont.GetFont(size: 12),
                        Colour = colours.Content2,
                    }, Color4.White));
                }
            }

            int[] spread = (set.Ratings ?? Array.Empty<int>()).Skip(1).Take(10).ToArray();

            if (spread.Length == 10 && set.Status > BeatmapOnlineStatus.Pending)
            {
                int negative = spread.Take(5).Sum();
                int positive = spread.Skip(5).Sum();
                int total = negative + positive;

                double average = total > 0 ? spread.Select((c, i) => c * (i + 1)).Sum() / (double)total : 0;

                column.Add(ratingBar(total > 0 ? LocalisableString.Interpolate($"{BeatmapsetsStrings.ShowStatsUserRating} ({average:0.00})") : BeatmapsetsStrings.ShowStatsUserRating,
                    negative.ToString(@"N0"), positive.ToString(@"N0"), total > 0 ? (float)positive / total : 0));

                int max = Math.Max(1, spread.Max());

                var bars = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 33,
                    Direction = FillDirection.Horizontal,
                    Margin = new MarginPadding { Top = 4 },
                };

                for (int i = 0; i < 10; i++)
                {
                    bars.Add(new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Width = 0.1f,
                        Padding = new MarginPadding { Horizontal = 1.5f },
                        Child = new SpreadBar(spread[i], Math.Max(0.06f, (float)spread[i] / max), WebBeatmapSetColours.RATING_SPREAD[i]),
                    });
                }

                column.Add(new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = BeatmapsetsStrings.ShowStatsRatingSpread,
                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                        },
                        bars,
                    }
                });
            }

            ratings.Child = column;
        }

        private Drawable ratingBar(LocalisableString label, string left, string right, float fraction, LocalisableString tooltip = default)
        {
            var bar = new WebBar
            {
                TrackColour = WebBeatmapSetColours.RED_3,
                FillColour = WebBeatmapSetColours.LIME_1,
            };

            bar.SetFraction(fraction);

            return new WebLabelledValue(label, new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new[]
                        {
                            new OsuSpriteText { Text = left, Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold) },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Text = right,
                                Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold),
                            },
                        }
                    },
                    bar,
                }
            }, Color4.White)
            {
                AutoSizeAxes = Axes.Y,
                RelativeSizeAxes = Axes.X,
                TooltipText = tooltip,
            };
        }

        private partial class SpreadBar : Container, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public SpreadBar(int count, float height, Color4 colour)
            {
                RelativeSizeAxes = Axes.Both;
                TooltipText = count.ToLocalisableString(@"N0");

                Child = new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.Both,
                    Height = height,
                    Colour = colour,
                };
            }
        }

        // The bar's counts: the set's plays, the difficulty's plays and the favourites.
        private void updateCounts(APIBeatmapSet set, APIBeatmap? beatmap)
        {
            footerCounts.Children = new Drawable[]
            {
                footerCount(@"Total Play Count", set.PlayCount.ToLocalisableString(@"N0"), FontAwesome.Solid.GlobeAmericas),
                footerCount(@"Difficulty Play Count", (beatmap?.PlayCount ?? 0).ToLocalisableString(@"N0"), FontAwesome.Solid.GlobeAmericas),
                footerCount(@"Favourites", favouriteState.Value.FavouriteCount > 0 ? favouriteState.Value.FavouriteCount.ToLocalisableString(@"N0") : "No favourites yet", null),
            };
        }

        private Drawable footerCount(LocalisableString label, LocalisableString value, IconUsage? icon)
        {
            var valueFlow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(4, 0),
            };

            if (icon != null)
            {
                valueFlow.Add(new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Icon = icon.Value,
                    Size = new Vector2(12),
                    Colour = colours.Content2,
                });
            }

            valueFlow.Add(new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = value,
                Font = OsuFont.GetFont(size: 14),
                Colour = colours.Content2,
            });

            return new WebLabelledValue(label, valueFlow, Color4.White);
        }

        #endregion

        #region Buttons

        // Favourite (pink, with the count), Download (with or without the video), osu!direct (downloads it into the game, or plays it once
        // there) and "…" (the discussion).
        private void updateButtons()
        {
            var set = BeatmapSet.Value;

            if (set == null || downloadTracker == null)
                return;

            downloadButtonsContainer.Clear(false);

            bool available = downloadTracker.State.Value == DownloadState.LocallyAvailable;

            if (!set.Availability.DownloadDisabled || available)
            {
                if (!available)
                    downloadButtonsContainer.Add(new DownloadButton(this, set));

                downloadButtonsContainer.Add(new DirectButton(this, set, downloadTracker.State.Value));
            }

            // The download buttons' container stays (it's only moved).
            if (downloadButtonsContainer.Parent == footerButtons)
                footerButtons.Remove(downloadButtonsContainer, false);

            footerButtons.Children = new Drawable[]
            {
                new FavouriteButton(this, set),
                downloadButtonsContainer,
                new MoreButton(this, set),
            };
        }

        private void download(APIBeatmapSet set, bool noVideo)
        {
            if (downloadTracker?.State.Value == DownloadState.NotDownloaded)
                downloader?.Download(set, noVideo);
        }

        private partial class FavouriteButton : WebPillButton
        {
            private readonly BeatmapSetHeaderContent header;
            private readonly APIBeatmapSet set;
            private PostBeatmapFavouriteRequest? request;

            [Resolved]
            private INotificationOverlay? notifications { get; set; }

            public FavouriteButton(BeatmapSetHeaderContent header, APIBeatmapSet set)
            {
                this.header = header;
                this.set = set;

                BackgroundColour = WebBeatmapSetColours.PINK_3;
                MinimumWidth = 50;
                Action = toggle;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                header.favouriteState.BindValueChanged(_ => updateContent(), true);
            }

            private void updateContent()
            {
                ClearContent();
                AddIcon(header.favouriteState.Value.Favourited ? FontAwesome.Solid.Heart : FontAwesome.Regular.Heart);
                AddText(header.favouriteState.Value.FavouriteCount.ToLocalisableString(@"N0"));
            }

            private void toggle()
            {
                if (!header.api.IsLoggedIn)
                    return;

                bool favourite = !header.favouriteState.Value.Favourited;

                request?.Cancel();
                request = new PostBeatmapFavouriteRequest(set.OnlineID, favourite ? BeatmapFavouriteAction.Favourite : BeatmapFavouriteAction.UnFavourite);
                Enabled.Value = false;

                request.Success += () =>
                {
                    var state = header.favouriteState.Value;
                    header.favouriteState.Value = new BeatmapSetFavouriteState(favourite, state.FavouriteCount + (favourite ? 1 : -1));
                    set.HasFavourited = favourite;
                    set.FavouriteCount = header.favouriteState.Value.FavouriteCount;
                    header.updateCounts(set, header.Picker.ShownBeatmap.Value);
                    header.api.LocalUserState.UpdateFavouriteBeatmapSets();
                    Enabled.Value = true;
                };

                request.Failure += e =>
                {
                    notifications?.Post(new SimpleNotification { Text = e.Message, Icon = FontAwesome.Solid.Times });
                    Enabled.Value = true;
                };

                header.api.Queue(request);
            }
        }

        private partial class DownloadButton : WebPillButton, IHasPopover
        {
            private readonly BeatmapSetHeaderContent header;
            private readonly APIBeatmapSet set;

            public DownloadButton(BeatmapSetHeaderContent header, APIBeatmapSet set)
            {
                this.header = header;
                this.set = set;

                BackgroundColour = WebBeatmapSetColours.LIME_3;
                MinimumWidth = 120;
                AddText(BeatmapsetsStrings.ShowDetailsDownloadDefault);
                AddIcon(FontAwesome.Solid.AngleDown);

                Action = () =>
                {
                    if (set.HasVideo)
                        this.ShowPopover();
                    else
                        header.download(set, false);
                };
            }

            public Popover GetPopover() => new MenuPopover(new (LocalisableString, Action)[]
            {
                (BeatmapsetsStrings.ShowDetailsDownloadVideo, () => header.download(set, false)),
                (BeatmapsetsStrings.ShowDetailsDownloadNoVideo, () => header.download(set, true)),
            });
        }

        private partial class DirectButton : WebPillButton
        {
            public DirectButton(BeatmapSetHeaderContent header, APIBeatmapSet set, DownloadState state)
            {
                BackgroundColour = WebBeatmapSetColours.LIME_3;
                MinimumWidth = 120;

                switch (state)
                {
                    case DownloadState.LocallyAvailable:
                        AddText(@"osu!direct");
                        AddIcon(FontAwesome.Solid.AngleDoubleRight);
                        Action = () => header.game?.PresentBeatmap(set, b => b.OnlineID == header.Picker.Beatmap.Value?.OnlineID);
                        break;

                    case DownloadState.Downloading:
                    case DownloadState.Importing:
                        AddText(@"Downloading...");
                        Enabled.Value = false;
                        break;

                    default:
                        AddText(@"osu!direct");
                        Action = () => header.download(set, header.preferNoVideo.Value);
                        break;
                }
            }
        }

        private partial class MoreButton : WebPillButton, IHasPopover
        {
            private readonly BeatmapSetHeaderContent header;
            private readonly APIBeatmapSet set;

            public MoreButton(BeatmapSetHeaderContent header, APIBeatmapSet set)
            {
                this.header = header;
                this.set = set;

                MinimumWidth = 40;
                AddIcon(FontAwesome.Solid.EllipsisH);
                Action = this.ShowPopover;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                BackgroundColour = header.colours.Background2;
            }

            public Popover GetPopover() => new MenuPopover(new (LocalisableString, Action)[]
            {
                (BeatmapsetsStrings.ShowDiscussion, () => header.game?.OpenUrlExternally($@"/beatmapsets/{set.OnlineID}/discussion")),
            });
        }

        private partial class MenuPopover : OsuPopover
        {
            public MenuPopover(IEnumerable<(LocalisableString Text, Action Action)> items)
            {
                var flow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                };

                foreach (var (text, action) in items)
                {
                    flow.Add(new OsuClickableContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Action = () =>
                        {
                            action();
                            this.HidePopover();
                        },
                        Child = new OsuSpriteText
                        {
                            Text = text,
                            Font = OsuFont.GetFont(size: 12, weight: FontWeight.SemiBold),
                        },
                    });
                }

                Child = flow;
            }
        }

        #endregion

        public partial class MetadataFlowContainer : LinkFlowContainer
        {
            public MetadataFlowContainer(Action<SpriteText>? defaultCreationParameters = null)
                : base(defaultCreationParameters)
            {
                TextAnchor = Anchor.BottomLeft;
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            protected override DrawableLinkCompiler CreateLinkCompiler(ITextPart textPart) => new MetadataLinkCompiler(textPart);

            public partial class MetadataLinkCompiler : DrawableLinkCompiler
            {
                public MetadataLinkCompiler(ITextPart part)
                    : base(part)
                {
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    IdleColour = Color4.White;
                }
            }
        }
    }
}

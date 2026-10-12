// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.BeatmapSet;
using osu.Game.Overlays.BeatmapSet.Scores;
using osu.Game.Overlays.Comments;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public partial class BeatmapSetOverlay : OnlineOverlay<BeatmapSetHeader>
    {
        public const float Y_PADDING = 25;
        public const float RIGHT_WIDTH = 275;

        private readonly Bindable<APIBeatmapSet> beatmapSet = new Bindable<APIBeatmapSet>();

        [Resolved]
        private IAPIProvider api { get; set; }

        [Resolved(CanBeNull = true)]
        private OsuGame game { get; set; }

        private IBindable<APIUser> apiUser;

        private (BeatmapSetLookupType type, int id)? lastLookup;

        private ScoresContainer scores;
        private CommentsSection comments;
        private LookupErrorSection lookupError;

        public BeatmapSetOverlay()
            : base(OverlayColourScheme.Blue)
        {
            Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 20),
                Children = new Drawable[]
                {
                    lookupError = new LookupErrorSection(),

                    scores = new ScoresContainer
                    {
                        Beatmap = { BindTarget = Header.HeaderContent.Picker.Beatmap }
                    },

                    comments = new CommentsSection()
                }
            };

            Header.BeatmapSet.BindTo(beatmapSet);
            comments.BeatmapSet.BindTo(beatmapSet);

            // The website's tabs: "comments" goes down to them, "discussion" opens it on the website (both then back to "info").
            Header.Current.BindValueChanged(tab =>
            {
                switch (tab.NewValue)
                {
                    case BeatmapSetTabs.Comments:
                        ScrollFlow.ScrollIntoView(comments);
                        break;

                    case BeatmapSetTabs.Discussion:
                        if (beatmapSet.Value != null)
                            game?.OpenUrlExternally($@"/beatmapsets/{beatmapSet.Value.OnlineID}/discussion");
                        break;

                    default:
                        return;
                }

                Schedule(() => Header.Current.Value = BeatmapSetTabs.Info);
            });

            Header.HeaderContent.Picker.Beatmap.ValueChanged += _ => ScrollFlow.ScrollToStart();

            hideLookupError();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            apiUser = api.LocalUser.GetBoundCopy();
            apiUser.BindValueChanged(_ => Schedule(() =>
            {
                if (api.IsLoggedIn)
                    performFetch();
            }));
        }

        protected override BeatmapSetHeader CreateHeader() => new BeatmapSetHeader();

        protected override Color4 BackgroundColour => ColourProvider.Background6;

        protected override void PopOutComplete()
        {
            base.PopOutComplete();

            beatmapSet.Value = null;
            hideLookupError();
        }

        public void FetchAndShowBeatmap(int beatmapId)
        {
            lastLookup = (BeatmapSetLookupType.BeatmapId, beatmapId);

            beatmapSet.Value = null;
            hideLookupError();

            performFetch();
            Show();
        }

        public void FetchAndShowBeatmapSet(int beatmapSetId)
        {
            lastLookup = (BeatmapSetLookupType.SetId, beatmapSetId);

            beatmapSet.Value = null;
            hideLookupError();

            performFetch();
            Show();
        }

        /// <summary>
        /// Show an already fully-populated beatmap set.
        /// </summary>
        /// <param name="set">The set to show.</param>
        public void ShowBeatmapSet(APIBeatmapSet set)
        {
            lastLookup = null;

            hideLookupError();

            beatmapSet.Value = set;
            Show();
        }

        private void performFetch()
        {
            if (!api.IsLoggedIn)
                return;

            if (lastLookup == null)
                return;

            var lookup = lastLookup.Value;

            var req = new GetBeatmapSetRequest(lookup.id, lookup.type);

            req.Success += res =>
            {
                Schedule(() =>
                {
                    if (lookupIsStale(lookup))
                        return;

                    if (res == null || res.OnlineID <= 0)
                    {
                        showLookupError(lookup);
                        return;
                    }

                    beatmapSet.Value = res;

                    if (lookup.type == BeatmapSetLookupType.BeatmapId)
                    {
                        var beatmap = Header.BeatmapSet.Value?.Beatmaps?.FirstOrDefault(b => b.OnlineID == lookup.id);

                        if (beatmap == null)
                        {
                            showLookupError(lookup);
                            return;
                        }

                        Header.HeaderContent.Picker.Beatmap.Value = beatmap;
                    }

                    hideLookupError();
                });
            };

            req.Failure += _ =>
            {
                Schedule(() =>
                {
                    if (lookupIsStale(lookup))
                        return;

                    showLookupError(lookup);
                });
            };

            API.Queue(req);
        }

        private bool lookupIsStale((BeatmapSetLookupType type, int id) lookup)
            => lastLookup == null || lastLookup.Value.type != lookup.type || lastLookup.Value.id != lookup.id;

        private void showLookupError((BeatmapSetLookupType type, int id) lookup)
        {
            beatmapSet.Value = null;

            string objectName = lookup.type == BeatmapSetLookupType.BeatmapId ? "beatmap" : "beatmapset";

            lookupError.Title.Value = "This beatmap could not be found";
            lookupError.Description.Value =
                $"The {objectName} may have been deleted, hidden, restricted, or it may not exist on this server yet.";
            lookupError.LookupText.Value = $"{objectName} id: {lookup.id}";

            lookupError.Show();

            Header.HeaderContent.Hide();

            scores.Hide();
            comments.Hide();
        }

        private void hideLookupError()
        {
            lookupError.ClearTransforms();
            lookupError.Alpha = 0;
            lookupError.Height = 0;
            lookupError.Y = 0;
            lookupError.Hide();

            Header.HeaderContent.Show();

            scores.Show();
            comments.Show();
        }

        private partial class LookupErrorSection : CompositeDrawable
        {
            public readonly Bindable<string> Title = new Bindable<string>();
            public readonly Bindable<string> Description = new Bindable<string>();
            public readonly Bindable<string> LookupText = new Bindable<string>();

            private readonly OsuSpriteText titleText;
            private readonly OsuSpriteText descriptionText;
            private readonly OsuSpriteText lookupText;

            public LookupErrorSection()
            {
                RelativeSizeAxes = Axes.X;
                Height = 0;
                Alpha = 0;
                AlwaysPresent = false;

                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(16, 20, 23, 245)
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding
                            {
                                Horizontal = 70,
                                Vertical = 35
                            },
                            Child = new Container
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Y = 90,
                                RelativeSizeAxes = Axes.X,
                                Height = 260,
                                Masking = true,
                                CornerRadius = 18,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = new Color4(35, 43, 48, 255)
                                    },
                                    new Box
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                        RelativeSizeAxes = Axes.X,
                                        Height = 4,
                                        Colour = new Color4(100, 210, 255, 255)
                                    },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 12),
                                        Children = new Drawable[]
                                        {
                                            new Container
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Size = new Vector2(74),
                                                Masking = true,
                                                CornerRadius = 37,
                                                Children = new Drawable[]
                                                {
                                                    new Box
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                        Colour = new Color4(47, 59, 66, 255)
                                                    },
                                                    new SpriteIcon
                                                    {
                                                        Anchor = Anchor.Centre,
                                                        Origin = Anchor.Centre,
                                                        Icon = FontAwesome.Solid.Search,
                                                        Size = new Vector2(32),
                                                        Colour = new Color4(160, 225, 255, 255)
                                                    }
                                                }
                                            },
                                            titleText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Font = OsuFont.Default.With(size: 30, weight: FontWeight.SemiBold),
                                                Colour = Color4.White,
                                                Text = string.Empty
                                            },
                                            descriptionText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Font = OsuFont.Default.With(size: 18),
                                                Colour = new Color4(185, 202, 210, 255),
                                                Text = string.Empty
                                            },
                                            lookupText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.TopCentre,
                                                Origin = Anchor.TopCentre,
                                                Font = OsuFont.Default.With(size: 15),
                                                Colour = new Color4(120, 145, 155, 255),
                                                Text = string.Empty
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                Title.BindValueChanged(v => titleText.Text = v.NewValue ?? string.Empty, true);
                Description.BindValueChanged(v => descriptionText.Text = v.NewValue ?? string.Empty, true);
                LookupText.BindValueChanged(v => lookupText.Text = v.NewValue ?? string.Empty, true);
            }

            public override void Show()
            {
                ClearTransforms();

                Height = 0;
                Alpha = 0;
                Y = 10;

                this.ResizeHeightTo(560, 220, Easing.OutQuint);
                this.FadeIn(220, Easing.OutQuint);
                this.MoveToY(0, 220, Easing.OutQuint);
            }

            public override void Hide()
            {
                ClearTransforms();
                Alpha = 0;
                Height = 0;
                Y = 0;
            }
        }

        private partial class CommentsSection : BeatmapSetLayoutSection
        {
            public readonly Bindable<APIBeatmapSet> BeatmapSet = new Bindable<APIBeatmapSet>();

            public CommentsSection()
            {
                CommentsContainer comments;

                Add(comments = new CommentsContainer());

                BeatmapSet.BindValueChanged(beatmapSet =>
                {
                    if (beatmapSet.NewValue?.OnlineID > 0)
                    {
                        Show();
                        comments.ShowComments(CommentableType.Beatmapset, beatmapSet.NewValue.OnlineID);
                    }
                    else
                    {
                        Hide();
                    }
                }, true);
            }
        }
    }
}
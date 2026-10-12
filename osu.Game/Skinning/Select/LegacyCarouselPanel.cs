// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Select
{
    /// <summary>
    /// One panel of osu!stable's beatmap list: the skin's "menu-button-background" tinted by state, the set's thumbnail, the title,
    /// "artist // mapper", the difficulty (bold) with the player's grade, and on difficulties the star rating as a row of the skin's stars.
    /// A group's panel is its name. Laid out in stable's 1024x768 units, scaled by <see cref="LayoutScale"/>.
    /// </summary>
    public partial class LegacyCarouselPanel : PoolableDrawable, ICarouselPanel
    {
        /// <summary>
        /// The distance between panels, measured against stable at 1920x1080 (58 in its 640x480).
        /// </summary>
        public const float PITCH = 58 * 1.6f;

        // Stable's colours: a set's panel orange until it's been played, pink after; the open set's difficulties blue;
        // the selected one white. Groups dark blue (lighter with the current map in them), green when open.
        private static readonly Color4 unplayed_colour = new Color4(233, 104, 0, 240);
        private static readonly Color4 played_colour = new Color4(235, 73, 153, 240);
        private static readonly Color4 expanded_colour = new Color4(0, 150, 236, 240);
        private static readonly Color4 selected_colour = new Color4(255, 255, 255, 220);
        private static readonly Color4 group_colour = new Color4(35, 50, 143, 255);
        private static readonly Color4 group_current_colour = new Color4(35, 90, 193, 255);
        private static readonly Color4 group_open_colour = new Color4(163, 240, 44, 255);

        /// <summary>
        /// Stable layout units to the carousel's.
        /// </summary>
        public float LayoutScale
        {
            get => layoutScale;
            set
            {
                layoutScale = value;
                Height = PITCH * value;

                if (content != null)
                    content.Scale = new Vector2(value);
            }
        }

        private float layoutScale = 1;

        /// <summary>
        /// For a set's panel: its only difficulty (shown with its stars, like stable's), or null when it has more (just the set's name then).
        /// </summary>
        public BeatmapInfo? OnlyDifficulty { get; set; }

        // Whether this panel shows a difficulty (its name, grade and stars).
        private bool showsDifficulty;

        /// <summary>
        /// The difficulty's name shown on the panel (empty for a closed set of several).
        /// </summary>
        public string DifficultyText => difficulty.Text.ToString();

        /// <summary>
        /// Whether the mouse is over the panel (stable moves it left and lightens it).
        /// </summary>
        public bool Hovered { get; private set; }

        [Resolved]
        private BeatmapCarousel? carousel { get; set; }

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        private Container content = null!;
        private Container backgroundSlot = null!;
        private Drawable background = null!;
        private LegacyCarouselThumbnail thumbnail = null!;
        private OsuSpriteText title = null!;
        private OsuSpriteText artist = null!;
        private OsuSpriteText difficulty = null!;
        private Sprite grade = null!;
        private FillFlowContainer stars = null!;

        private Color4 inactiveText = Color4.White;
        private Color4 activeText = Color4.Black;
        private Color4 colour;

        private IDisposable? scoreSubscription;

        public LegacyCarouselPanel()
        {
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            Width = 2000;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = content = new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Scale = new Vector2(layoutScale),
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    backgroundSlot = new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                    },
                    // Stable's: at 5,0 (its 640x480), 160x120 shown at 114x85, dark until the set is open.
                    thumbnail = new LegacyCarouselThumbnail
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 5.2f * 1.6f,
                        Size = new Vector2(114, 85.5f),
                    },
                    title = text(25.6f, false),
                    artist = text(19.2f, false),
                    difficulty = text(19.2f, true),
                    grade = new Sprite { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                    stars = new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                    },
                }
            };

            LayoutScale = layoutScale;
            applySkin();
        }

        // The skin's bar and text colours (again when the skin changes: panels are reused).
        private void applySkin()
        {
            inactiveText = skin.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup("SongSelectInactiveText"))?.Value ?? Color4.White;
            activeText = skin.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup("SongSelectActiveText"))?.Value ?? Color4.Black;

            var barTexture = skin.GetTexture(@"menu-button-background");

            backgroundSlot.Child = background = barTexture != null
                ? new Sprite { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft, Texture = barTexture }
                // Lazer's default skin has none: a bar like stable's default one.
                : new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(699, 98),
                    Masking = true,
                    CornerRadius = 12,
                    BorderThickness = 2,
                    BorderColour = Color4.White.Opacity(0.6f),
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = ColourInfo2.Gradient },
                };

            if (IsLoaded)
                updateState(false);
        }

        private void onSkinChanged() => Schedule(applySkin);

        /// <summary>
        /// The bar behind the panel (the skin's "menu-button-background", or one like stable's default).
        /// </summary>
        public Drawable Bar => background;

        private static OsuSpriteText text(float size, bool bold) => new OsuSpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Font = OsuFont.GetFont(size: size, weight: bold ? FontWeight.Bold : FontWeight.Regular),
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;

            Selected.BindValueChanged(_ => updateState());
            Expanded.BindValueChanged(_ => updateState());
            KeyboardSelected.BindValueChanged(_ => updateState());
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            scoreSubscription?.Dispose();
            scoreSubscription = null;
            grade.Texture = null;
            stars.Clear();
            showsDifficulty = false;

            switch (Item?.Model)
            {
                case GroupDefinition group:
                    thumbnail.Hide();
                    // Stable's: "Name (12 maps)".
                    int count = Item!.NestedItemCount;
                    title.Text = $"{group.Title} ({count} map{(count != 1 ? "s" : string.Empty)})";
                    title.Font = title.Font.With(size: 38.4f);
                    title.Position = new Vector2(15 * 1.6f, 0);
                    artist.Text = difficulty.Text = string.Empty;
                    break;

                case GroupedBeatmapSet groupedSet:
                    // Stable's: a set of several difficulties by its name until it's opened; a set of one as that difficulty.
                    showBeatmap(OnlyDifficulty ?? groupedSet.BeatmapSet.Beatmaps.OrderBy(b => b.StarRating).First(), groupedSet.BeatmapSet, OnlyDifficulty != null);
                    break;

                case GroupedBeatmap groupedBeatmap:
                    showBeatmap(groupedBeatmap.Beatmap, groupedBeatmap.Beatmap.BeatmapSet!, true);
                    break;
            }

            CurrentX = null;
            Hovered = false;
            updateState(false);
            this.FadeInFromZero(300);
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            scoreSubscription?.Dispose();
            scoreSubscription = null;
            item = null;
        }

        private void showBeatmap(BeatmapInfo beatmap, BeatmapSetInfo set, bool isDifficulty)
        {
            thumbnail.Show();
            thumbnail.Beatmap = beatmaps.GetWorkingBeatmap(beatmap);

            title.Font = title.Font.With(size: 25.6f);
            title.Text = StableMetadata.Title(beatmap.Metadata);
            artist.Text = LocalisableString.Interpolate($"{StableMetadata.Artist(beatmap.Metadata)} // {beatmap.Metadata.Author.Username}");
            difficulty.Text = isDifficulty ? beatmap.DifficultyName : string.Empty;
            showsDifficulty = isDifficulty;

            if (isDifficulty)
            {
                createStars(beatmap.StarRating);
                subscribeToRank(beatmap);
            }

            // Stable's set panels: played ones pink.
            colour = set.Beatmaps.Any(b => b.LastPlayed != null) ? played_colour : unplayed_colour;
            layoutText();
        }

        // Stable's: 10 faint stars, the rating's ones over them (the last one partly), at 60%.
        private void createStars(double rating)
        {
            var texture = skin.GetTexture(@"star");

            if (texture == null)
                return;

            double shown = Math.Min(rating, 10);

            for (int i = 0; i < 10; i++)
            {
                double fill = Math.Clamp(shown - i, 0, 1);

                stars.Add(new Container
                {
                    Size = new Vector2(texture.DisplayWidth, texture.DisplayHeight) * 0.6f,
                    Children = new Drawable[]
                    {
                        new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Texture = texture,
                            Scale = new Vector2(0.6f),
                            Colour = new Color4(255, 255, 255, 30),
                        },
                        new Sprite
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Texture = texture,
                            Scale = new Vector2(fill <= 0 ? 0 : 0.6f * (float)Math.Max(0.5, fill + i * 0.04)),
                        },
                    }
                });
            }
        }

        private void subscribeToRank(BeatmapInfo beatmap)
        {
            int userId = api.LocalUser.Value.OnlineID;
            var currentRuleset = ruleset.Value;

            scoreSubscription = realm.RegisterForNotifications(r => r.All<ScoreInfo>().Where(s => s.BeatmapHash == beatmap.Hash && !s.DeletePending), (sender, _) =>
            {
                var top = sender.Where(s => (s.UserID == userId || s.UserID <= 1) && currentRuleset.Equals(s.Ruleset))
                                .MaxBy(s => s.TotalScore);

                grade.Texture = top == null ? null : skin.GetTexture($"{DrawableRank.GetLegacyRankTextureName(top.Rank)}-small");
                layoutText();
            });
        }

        // Stable's positions (its 640x480, from the panel's left middle): the text 75 in after the thumbnail, then
        // 3 more (or 20 with a grade, which goes before it), the title 16 up, "artist // mapper" 4 up, the difficulty 7 down,
        // the stars 18 down.
        private void layoutText()
        {
            if (Item?.Model is GroupDefinition)
                return;

            bool open = Expanded.Value || Selected.Value || showsDifficulty;
            float textX = (75 + (open && grade.Texture != null ? 20 : 3) + 5) * 1.6f;

            title.Position = new Vector2(textX, -16 * 1.6f);
            artist.Position = new Vector2(textX + 1.6f, -4 * 1.6f);
            difficulty.Position = new Vector2(textX + 1.6f, 7 * 1.6f);
            grade.Position = new Vector2((5 + 75 - 1) * 1.6f, 0);
            stars.Position = new Vector2(textX, 18 * 1.6f);
        }

        private void updateState(bool animated = true)
        {
            var model = Item?.Model;
            double duration = animated ? 300 : 0;

            Color4 target;
            bool lit;

            switch (model)
            {
                case GroupDefinition:
                    target = Expanded.Value ? group_open_colour : group_colour;
                    title.Colour = Expanded.Value ? activeText : inactiveText;
                    lit = false;
                    break;

                case GroupedBeatmapSet:
                    target = colour;
                    lit = false;
                    break;

                default:
                    target = Selected.Value ? selected_colour : expanded_colour;
                    lit = true;
                    break;
            }

            // Stable lightens the panel under the mouse (and here the keyboard's).
            if (Hovered || KeyboardSelected.Value)
                target = Interpolation.ValueAt(0.4, target, Color4.White, 0, 1);

            background.FadeColour(target, duration, Easing.OutQuint);
            thumbnail.FadeColour(lit ? Color4.White : new Color4(50, 50, 50, 255), duration);

            if (model is not GroupDefinition)
            {
                var textColour = Selected.Value ? activeText : inactiveText;
                // On the open set's other difficulties, the title and artist are dimmer.
                var dim = model is GroupedBeatmap && !Selected.Value ? inactiveText.Opacity(0.5f) : textColour;

                title.FadeColour(dim, duration);
                artist.FadeColour(dim, duration);
                difficulty.FadeColour(textColour, duration);
                stars.FadeColour(textColour, duration);
            }

            // Stable shows an open set as its difficulties only.
            content.Alpha = model is GroupedBeatmapSet && Expanded.Value ? 0 : 1;

            stars.Alpha = showsDifficulty ? 1 : 0;
            grade.Alpha = showsDifficulty ? 1 : 0;
            layoutText();
        }

        /// <summary>
        /// Whether the panel shows (an open set's own panel doesn't).
        /// </summary>
        public bool Shown => content.Alpha > 0;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => Shown && background.ReceivePositionalInputAt(screenSpacePos);

        /// <summary>
        /// Where the carousel last put the panel across (it moves there smoothly, as in stable). Null when just shown.
        /// </summary>
        public float? CurrentX { get; set; }

        protected override bool OnHover(HoverEvent e)
        {
            Hovered = true;
            updateState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            Hovered = false;
            updateState();
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (item != null)
                carousel?.Activate(item);

            return true;
        }

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        // Stable's: right-clicking a beatmap opens its options.
        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != osuTK.Input.MouseButton.Right)
                return base.OnMouseDown(e);

            BeatmapInfo? beatmap = Item?.Model switch
            {
                GroupedBeatmap groupedBeatmap => groupedBeatmap.Beatmap,
                GroupedBeatmapSet groupedSet => OnlyDifficulty ?? groupedSet.BeatmapSet.Beatmaps.OrderBy(b => b.StarRating).FirstOrDefault(),
                _ => null,
            };

            if (beatmap == null || songSelect is not SongSelect select)
                return base.OnMouseDown(e);

            select.ShowStableOptions(beatmap);
            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            scoreSubscription?.Dispose();

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;
        }

        #region ICarouselPanel

        private CarouselItem? item;

        public CarouselItem? Item
        {
            get => item;
            set
            {
                if (ReferenceEquals(item, value))
                    return;

                if (item != null && value != null)
                {
                    item = value;
                    PrepareForUse();
                }
                else
                    item = value;
            }
        }

        public BindableBool Selected { get; } = new BindableBool();
        public BindableBool Expanded { get; } = new BindableBool();
        public BindableBool KeyboardSelected { get; } = new BindableBool();

        public double DrawYPosition { get; set; }

        public void Activated()
        {
        }

        #endregion

        private static class ColourInfo2
        {
            // Stable's default bar: lighter at the top.
            public static readonly Framework.Graphics.Colour.ColourInfo Gradient =
                Framework.Graphics.Colour.ColourInfo.GradientVertical(Color4.White, Color4.White.Opacity(0.8f));
        }
    }
}

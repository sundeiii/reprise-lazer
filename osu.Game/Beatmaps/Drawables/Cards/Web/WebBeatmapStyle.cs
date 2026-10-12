// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Beatmaps.Drawables.Cards.Web
{
    /// <summary>
    /// The website's beatmap colours and pieces (osu-web's beatmap search and beatmap page after osu!'s design proposals).
    /// Sizes are the website's pixels.
    /// </summary>
    public static class WebBeatmapStyle
    {
        // The website's fixed-hue colours ("-1": 100% saturation, 70% lightness).
        public static readonly Color4 LIME_1 = Color4Extensions.FromHex(@"b3ff66");
        public static readonly Color4 BLUE_1 = Color4Extensions.FromHex(@"66ccff");
        public static readonly Color4 PINK_1 = Color4Extensions.FromHex(@"ff66ab");
        public static readonly Color4 ORANGE_1 = Color4Extensions.FromHex(@"ffd966");
        public static readonly Color4 DARKORANGE_1 = Color4Extensions.FromHex(@"ff9966");

        /// <summary>
        /// A status's pill background (the website's --beatmapset-{status}-bg).
        /// </summary>
        public static Color4 StatusBackground(BeatmapOnlineStatus status, OverlayColourProvider colours)
        {
            switch (status)
            {
                case BeatmapOnlineStatus.Graveyard:
                    return Color4.Black;

                case BeatmapOnlineStatus.WIP:
                    return DARKORANGE_1;

                case BeatmapOnlineStatus.Pending:
                    return ORANGE_1;

                case BeatmapOnlineStatus.Qualified:
                    return BLUE_1;

                case BeatmapOnlineStatus.Ranked:
                case BeatmapOnlineStatus.Approved:
                    return LIME_1;

                case BeatmapOnlineStatus.Loved:
                    return PINK_1;

                default:
                    return colours.Content2;
            }
        }

        /// <summary>
        /// A status's pill text (the website's --beatmapset-{status}-colour).
        /// </summary>
        public static Color4 StatusText(BeatmapOnlineStatus status, OverlayColourProvider colours)
            => status == BeatmapOnlineStatus.Graveyard ? colours.Background1 : colours.Background3;

        /// <summary>
        /// The date the website shows for a set: when it was ranked (or loved, qualified...), else when it was last updated.
        /// </summary>
        public static DateTimeOffset? ShownDate(APIBeatmapSet set)
        {
            switch (set.Status)
            {
                case BeatmapOnlineStatus.Graveyard:
                case BeatmapOnlineStatus.WIP:
                case BeatmapOnlineStatus.Pending:
                    return set.LastUpdated;

                default:
                    return set.Ranked ?? set.LastUpdated;
            }
        }

        /// <summary>
        /// The website's difficulty groups: one per mode (osu!, taiko, catch, mania), osu!mania split by key count, each sorted by difficulty.
        /// </summary>
        public static IEnumerable<(int RulesetID, int? Keys, APIBeatmap[] Beatmaps)> DifficultyGroups(APIBeatmapSet set)
        {
            foreach (var mode in set.Beatmaps.GroupBy(b => b.RulesetID).OrderBy(g => g.Key))
            {
                if (mode.Key == 3)
                {
                    foreach (var keys in mode.GroupBy(b => (int)Math.Round(b.CircleSize)).OrderBy(g => g.Key))
                        yield return (mode.Key, keys.Key, keys.OrderBy(b => b.StarRating).ToArray());
                }
                else
                    yield return (mode.Key, null, mode.OrderBy(b => b.StarRating).ToArray());
            }
        }
    }

    /// <summary>
    /// The website's status pill: fully rounded, bold uppercase in the status's colours.
    /// </summary>
    public partial class WebStatusPill : CircularContainer
    {
        private readonly BeatmapOnlineStatus status;
        private readonly float fontSize;
        private readonly float lineHeight;
        private readonly float horizontalPadding;

        public WebStatusPill(BeatmapOnlineStatus status, float fontSize = 11, float lineHeight = 14, float horizontalPadding = 8)
        {
            this.status = status;
            this.fontSize = fontSize;
            this.lineHeight = lineHeight;
            this.horizontalPadding = horizontalPadding;

            AutoSizeAxes = Axes.X;
            Height = lineHeight;
            Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = WebBeatmapStyle.StatusBackground(status, colours),
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Margin = new MarginPadding { Horizontal = horizontalPadding },
                    Text = status.GetLocalisableDescription().ToUpper(),
                    Font = OsuFont.Default.With(size: fontSize, weight: FontWeight.Bold),
                    Colour = WebBeatmapStyle.StatusText(status, colours),
                    Shadow = false,
                    UseFullGlyphHeight = false,
                    Y = 0.5f,
                },
            };
        }
    }

    /// <summary>
    /// The website's difficulty bars: per mode its icon (osu!mania's key count after it), then one rounded bar per difficulty in its colour,
    /// with "+" past <paramref name="maxBars"/>.
    /// </summary>
    public partial class WebDifficultyBars : FillFlowContainer
    {
        private readonly APIBeatmapSet set;
        private readonly Vector2 barSize;
        private readonly float barRadius;
        private readonly int maxBars;

        public WebDifficultyBars(APIBeatmapSet set, Vector2 barSize, int maxBars, float barRadius = 10)
        {
            this.set = set;
            this.barSize = barSize;
            this.maxBars = maxBars;
            this.barRadius = barRadius;

            AutoSizeAxes = Axes.Both;
            Direction = FillDirection.Horizontal;
            Spacing = new Vector2(8, 0);
        }

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets, OsuColour colours)
        {
            foreach (var group in WebBeatmapStyle.DifficultyGroups(set))
            {
                var items = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(2, 0),
                };

                var ruleset = rulesets.GetRuleset(group.RulesetID);

                if (ruleset != null)
                {
                    items.Add(new ConstrainedIconContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(14),
                        Margin = new MarginPadding { Right = 2 },
                        Icon = ruleset.CreateInstance().CreateIcon(),
                    });
                }

                if (group.Keys != null)
                {
                    items.Add(new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Margin = new MarginPadding { Right = 2 },
                        Text = group.Keys.Value.ToString(),
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                    });
                }

                foreach (var beatmap in group.Beatmaps.Take(maxBars))
                {
                    items.Add(new Bar(beatmap)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = barSize,
                        CornerRadius = Math.Min(barRadius, barSize.X / 2),
                        Colour = colours.ForStarDifficulty(beatmap.StarRating),
                    });
                }

                if (group.Beatmaps.Length > maxBars)
                {
                    items.Add(new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = @"+",
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                    });
                }

                Add(items);
            }
        }

        private partial class Bar : Container, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public Bar(APIBeatmap beatmap)
            {
                Masking = true;
                Child = new Box { RelativeSizeAxes = Axes.Both };
                TooltipText = LocalisableString.Interpolate($"{beatmap.DifficultyName} {beatmap.StarRating.FormatStarRating()}★");
            }
        }
    }

    /// <summary>
    /// The website's one-line "Title Artist", the artist smaller, cut with an ellipsis.
    /// </summary>
    public partial class WebTitleArtistLine : CompositeDrawable
    {
        private readonly TruncatingSpriteText title;
        private readonly TruncatingSpriteText artist;

        public WebTitleArtistLine(APIBeatmapSet set, float titleSize, float artistSize)
        {
            RelativeSizeAxes = Axes.X;
            // Just the letters' height (the text's own box has room above them, which put the card's lines off centre).
            Height = titleSize * 1.05f;

            InternalChildren = new Drawable[]
            {
                title = new TruncatingSpriteText
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Text = new RomanisableString(set.TitleUnicode, set.Title),
                    Font = OsuFont.Default.With(size: titleSize, weight: FontWeight.SemiBold),
                },
                artist = new TruncatingSpriteText
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Y = -(titleSize - artistSize) * 0.2f,
                    Text = new RomanisableString(set.ArtistUnicode, set.Artist),
                    Font = OsuFont.Default.With(size: artistSize, weight: FontWeight.SemiBold),
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            title.MaxWidth = DrawWidth;
            artist.X = title.DrawWidth + 4;
            artist.MaxWidth = Math.Max(0, DrawWidth - artist.X);
            artist.Alpha = artist.MaxWidth > 10 ? 1 : 0;
        }
    }
}

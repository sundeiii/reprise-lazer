// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Ranking
{
    /// <summary>
    /// Stable's rating after a play: "Please take a second to rate this beatmap by clicking a star below!" over ten stars; clicking
    /// one votes that many (on the server's beatmap set ratings), then the set's rating shows. With a skin that draws the second page
    /// (XooMoon's vote bar is where the stars are), only the stars' places to click are there.
    /// </summary>
    public partial class LegacyRankingRating : CompositeDrawable
    {
        // Measured against stable at 1920x1080: 51 apart, centred, 43 across.
        private const float star_spacing = 51.2f;
        private const float star_size = 43;

        private static readonly Color4 message_colour = new Color4(255, 80, 0, 255);
        private static readonly Color4 star_colour = new Color4(80, 170, 255, 255);
        private static readonly Color4 star_dim_colour = new Color4(80, 170, 255, 90);

        private readonly int beatmapSetId;
        private readonly bool decorated;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private OsuSpriteText message = null!;
        private FillFlowContainer<Star> stars = null!;
        private SkinnableSound clickSound = null!;

        private bool canVote;
        private int hovered;

        public LegacyRankingRating(int beatmapSetId, bool decorated)
        {
            this.beatmapSetId = beatmapSetId;
            this.decorated = decorated;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            var texture = skin.GetTexture(@"star");

            InternalChildren = new Drawable[]
            {
                message = new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.Centre,
                    Font = OsuFont.GetFont(size: 22),
                    Colour = message_colour,
                    Alpha = decorated ? 1 : 0,
                },
                stars = new FillFlowContainer<Star>
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.Centre,
                    Y = 42,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(star_spacing - star_size, 0),
                },
                clickSound = new SkinnableSound(new SampleInfo("click-short-confirm")),
            };

            for (int i = 1; i <= 10; i++)
                stars.Add(new Star(i, texture, decorated) { Hovered = onStarHovered, Clicked = vote });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!api.IsLoggedIn || beatmapSetId <= 0)
            {
                message.Text = "Rating is not available for this beatmap.";
                return;
            }

            message.Text = "Please take a second to rate this beatmap by clicking a star below!";

            var request = new GetBeatmapsetRatingRequest(beatmapSetId);
            request.Success += rating => Schedule(() =>
            {
                if (rating.DisallowRatingReason != null)
                {
                    message.Text = rating.DisallowRatingReason;
                    return;
                }

                if (rating.UserRating is int vote)
                {
                    showVoted(vote, rating.TotalRating);
                    return;
                }

                canVote = true;
                updateStars();
            });
            api.Queue(request);
        }

        private void onStarHovered(int value)
        {
            // After voting, the vote stays lit.
            if (!canVote)
                return;

            hovered = value;
            updateStars();
        }

        private void vote(int value)
        {
            if (!canVote)
                return;

            canVote = false;
            clickSound.Play();
            message.Text = "Sending your rating...";

            var request = new PostBeatmapsetRatingRequest(beatmapSetId, value);
            request.Success += response => Schedule(() => showVoted(value, response.UpdatedRating));
            request.Failure += _ => Schedule(() =>
            {
                message.Text = "Your rating couldn't be sent.";
                canVote = true;
            });
            api.Queue(request);
        }

        private void showVoted(int vote, double total)
        {
            canVote = false;
            hovered = vote;
            message.Text = string.Create(CultureInfo.InvariantCulture, $"Thanks for rating! (you gave {vote}) This beatmap's rating is now {total:0.00}.");
            updateStars();
        }

        private void updateStars()
        {
            foreach (var star in stars)
                star.Lit = star.Value <= hovered;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            if (canVote)
                onStarHovered(0);

            base.OnHoverLost(e);
        }

        private partial class Star : CompositeDrawable
        {
            public readonly int Value;

            public System.Action<int>? Hovered;
            public System.Action<int>? Clicked;

            private readonly Drawable icon;

            public Star(int value, osu.Framework.Graphics.Textures.Texture? texture, bool visible)
            {
                Value = value;
                Size = new Vector2(star_size);

                InternalChild = icon = texture != null
                    ? new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Texture = texture,
                        Size = new Vector2(star_size),
                    }
                    : new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Icon = FontAwesome.Solid.Star,
                        Size = new Vector2(star_size),
                    };

                icon.Colour = star_dim_colour;

                // With the skin's own vote bar: not seen, still there to click.
                if (!visible)
                {
                    icon.Alpha = 0;
                    icon.AlwaysPresent = true;
                }
            }

            public bool Lit
            {
                set
                {
                    icon.FadeColour(value ? star_colour : star_dim_colour, 100);
                    icon.ScaleTo(value ? 1.1f : 1, 100);
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                Hovered?.Invoke(Value);
                return true;
            }

            protected override bool OnClick(ClickEvent e)
            {
                Clicked?.Invoke(Value);
                return true;
            }
        }
    }
}

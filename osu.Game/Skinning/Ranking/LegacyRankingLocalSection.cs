// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Scoring;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Ranking
{
    /// <summary>
    /// The "LOCAL" band of a legacy ranking screen's second page: the score's place among the local scores, the player,
    /// and the replay export and favourite options on the right.
    /// </summary>
    /// <remarks>
    /// Measured against stable's ranking screen at 1024x768, scrolled down. 204px tall; the band is 745px wide.
    /// </remarks>
    public partial class LegacyRankingLocalSection : CompositeDrawable
    {
        public const float HEIGHT = 204;
        private const float band_width = 745;

        private static readonly Color4 band_colour = new Color4(200, 160, 0, 150);
        private static readonly Color4 message_colour = new Color4(255, 214, 0, 255);

        private readonly ScoreInfo score;
        private readonly LegacyRankingDetails details;

        // The player's statistics after the score (from the server), or as they are now.
        private readonly Bindable<Users.UserStatistics?> cardStatistics = new Bindable<Users.UserStatistics?>();

        public LegacyRankingLocalSection(ScoreInfo score, LegacyRankingDetails details)
        {
            this.score = score;
            this.details = details;

            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            // With a skin that draws the second page, only the player and the options (unseen, over the skin's) show.
            bool decorated = !LegacyRankingScreen.HasSecondPageArt(skin);

            InternalChildren = new Drawable[]
            {
                // Stable keeps this band's content with the right side on wider screens (measured at 1920x1080).
                LegacyRankingScreen.CreateRightAlignedContent(
                    // From the screen's left edge to 745px into the layout, a lighter strip along the top for the message.
                    new Container
                    {
                        X = -LegacyRankingScreen.LAYOUT_SIZE.X,
                        Width = LegacyRankingScreen.LAYOUT_SIZE.X + band_width,
                        RelativeSizeAxes = Axes.Y,
                        Alpha = decorated ? 1 : 0,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = band_colour },
                            new Box { RelativeSizeAxes = Axes.X, Height = 52, Colour = new Color4(255, 210, 0, 50) },
                        }
                    }),
                LegacyRankingScreen.CreateSectionTitle("LOCAL").With(t => t.Alpha = decorated ? 1 : 0),
                LegacyRankingScreen.CreateRightAlignedContent(
                new OsuSpriteText
                {
                    Position = new Vector2(370, 30),
                    Origin = Anchor.Centre,
                    Text = details.LocalRank is int rank
                        ? $"You achieved the #{rank.ToString(CultureInfo.InvariantCulture)} score on local rankings!"
                        : string.Empty,
                    Font = OsuFont.GetFont(size: 28),
                    Colour = message_colour,
                    Alpha = decorated ? 1 : 0,
                },
                new OsuSpriteText
                {
                    Alpha = decorated ? 1 : 0,
                    Position = new Vector2(195, 74),
                    Origin = Anchor.CentreLeft,
                    Text = "Local player name:",
                    Font = OsuFont.GetFont(size: 18),
                },
                new Container
                {
                    Alpha = decorated ? 1 : 0,
                    Position = new Vector2(352, 64),
                    Size = new Vector2(208, 22),
                    Masking = true,
                    BorderColour = Color4.White,
                    BorderThickness = 2,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0, 0, 0, 100) },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 3,
                            Text = score.User.Username,
                            Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                        },
                    }
                },
                // Stable's player panel, as in song select's footer.
                new Select.LegacyFooterUser
                {
                    User = score.User,
                    Ruleset = score.Ruleset,
                    Statistics = cardStatistics,
                    Position = new Vector2(213, 100),
                }),
                new FillFlowContainer
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopLeft,
                    // 775px from the left in the 1024px layout: on wider screens it stays with the right side.
                    X = 775 - LegacyRankingScreen.LAYOUT_SIZE.X,
                    Y = 18,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 16),
                    Children = new Drawable[]
                    {
                        new LegacyOption("Export replay as .osr", details.ExportReplay, null) { ShowText = decorated },
                        new LegacyOption("Online Favourite", details.ToggleFavourite, details.Favourited) { ShowText = decorated },
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Scores don't come with their player's statistics: the current ones when the server sent none for the score.
            details.StatisticsUpdate.BindValueChanged(_ => showLatest());
            details.CurrentStatistics.BindValueChanged(_ => showLatest(), true);
        }

        private void showLatest() => cardStatistics.Value = details.StatisticsUpdate.Value?.After ?? details.CurrentStatistics.Value;

        /// <summary>
        /// An option with a round box: clicking does its action; the box is filled while its state is on.
        /// </summary>
        private partial class LegacyOption : ClickableContainer
        {
            private readonly IBindable<bool>? state;
            private Circle fill = null!;
            private readonly OsuSpriteText label;

            /// <summary>
            /// False when the skin draws the option's words itself: only the circle shows (the option still takes clicks there).
            /// </summary>
            public bool ShowText { get; init; } = true;

            public LegacyOption(string text, Action? action, IBindable<bool>? state)
            {
                this.state = state;

                Action = action;
                Enabled.Value = action != null;
                AutoSizeAxes = Axes.Both;

                Children = new Drawable[]
                {
                    new Container
                    {
                        Origin = Anchor.CentreLeft,
                        Y = 12,
                        Size = new Vector2(14),
                        Masking = true,
                        CornerRadius = 7,
                        BorderColour = new Color4(255, 120, 180, 255),
                        BorderThickness = 2,
                        Child = fill = new Circle
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                            Colour = new Color4(255, 120, 180, 255),
                        },
                    },
                    label = new OsuSpriteText
                    {
                        X = 28,
                        Text = text,
                        Font = OsuFont.GetFont(size: 21),
                        AlwaysPresent = true,
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Alpha = Enabled.Value ? 1 : 0.5f;
                label.Alpha = ShowText ? 1 : 0;
                state?.BindValueChanged(s => fill.Alpha = s.NewValue ? 1 : 0, true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (Enabled.Value)
                    fill.FadeTo(state?.Value == true ? 1 : 0.5f, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                fill.FadeTo(state?.Value == true ? 1 : 0, 100);
                base.OnHoverLost(e);
            }
        }
    }
}

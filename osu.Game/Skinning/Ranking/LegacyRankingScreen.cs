// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Ranking
{
    /// <summary>
    /// osu!stable's ranking screen for a score. The first page has the top bar, the panel, the grade, the graph, the perfect stamp
    /// and the buttons; scrolling down (or the "Online Ranking" button) shows the second page: local and online rankings and the beatmap.
    /// The top bar stays where it is.
    /// </summary>
    /// <remarks>
    /// Laid out in the legacy 1024x768 layout, which widens with the screen (elements on the right stay on the right).
    /// </remarks>
    public partial class LegacyRankingScreen : CompositeDrawable
    {
        public static readonly Vector2 LAYOUT_SIZE = new Vector2(1024, 768);

        /// <summary>
        /// How far the second page is below the first: the screen without the top bar.
        /// </summary>
        public const float PAGE_DISTANCE = 672;

        private const float header_height = 96;

        private readonly Container pages;
        private readonly bool hasSecondPage;
        private readonly Drawable onlineRankingButton;

        /// <param name="score">The score to show.</param>
        /// <param name="health">The health over the play for the graph (see <see cref="LegacyRankingGraph"/>); empty for none.</param>
        /// <param name="retry">The retry button's action, or null for no retry button.</param>
        /// <param name="watchReplay">The watch replay button's action, or null for no watch replay button.</param>
        /// <param name="details">What the second page shows and what its options do.</param>
        public LegacyRankingScreen(ScoreInfo score, IReadOnlyList<Vector2> health, Action? retry, Action? watchReplay, LegacyRankingDetails? details = null)
        {
            details ??= new LegacyRankingDetails();
            hasSecondPage = details.HasSecondPage;

            RelativeSizeAxes = Axes.Both;

            InternalChild = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = LAYOUT_SIZE,
                Strategy = DrawSizePreservationStrategy.Minimum,
                Children = new Drawable[]
                {
                    pages = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new LegacyRankingPanel(score),
                            new LegacyRankingGrade(score.Rank),
                            new LegacyRankingGraph(health),
                            new LegacyRankingPerfect(score),
                            new LegacyRankingMods(score),
                            new LegacyRankingButtons(retry, watchReplay),
                            onlineRankingButton = details.HasSecondPage ? new OnlineRankingButton(ShowSecondPage) : Empty(),
                            new FillFlowContainer
                            {
                                Y = PAGE_DISTANCE + header_height,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    new LegacyRankingLocalSection(score, details),
                                    new LegacyRankingOnlineSection(score, details),
                                    new LegacyRankingBeatmapSection(score, details),
                                }
                            },
                        }
                    },
                    new LegacyRankingHeader(score),
                    // Stable's: the skin's "menu-back", on both pages.
                    details.Back == null
                        ? Empty()
                        : new ClickableContainer
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            AutoSizeAxes = Axes.Both,
                            Action = details.Back,
                            Child = new Select.LegacyBackButton
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                            },
                        },
                }
            };
        }

        public bool ShowingSecondPage { get; private set; }

        public void ShowSecondPage() => showPage(true);

        public void ShowFirstPage() => showPage(false);

        private void showPage(bool second)
        {
            if (second && !hasSecondPage)
                return;

            ShowingSecondPage = second;
            pages.MoveToY(second ? -PAGE_DISTANCE : 0, 500, Easing.OutQuint);

            // It's at the bottom of the first page: on the second it would show through the top bar.
            onlineRankingButton.FadeTo(second ? 0 : 1, 200);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (!hasSecondPage)
                return false;

            showPage(e.ScrollDelta.Y < 0);
            return true;
        }

        /// <summary>
        /// A second-page band's content: laid out in the 1024px layout and, like stable's, centred on wider screens
        /// (the bands themselves go across).
        /// </summary>
        public static Container CreateCentredContent(params Drawable[] children) => new Container
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            Width = LAYOUT_SIZE.X,
            RelativeSizeAxes = Axes.Y,
            Children = children,
        };

        /// <summary>
        /// A second-page band's content laid out in the 1024px layout and kept with the right side on wider screens
        /// (stable's local band).
        /// </summary>
        public static Container CreateRightAlignedContent(params Drawable[] children) => new Container
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Width = LAYOUT_SIZE.X,
            RelativeSizeAxes = Axes.Y,
            Children = children,
        };

        /// <summary>
        /// Whether the skin is an old one (skin.ini's Version 1.0, which is also a skin without one): stable lays out its ranking screen
        /// a little differently for those (the panel, grade and graph higher, the score smaller, the buttons "ranking-*" rather than "pause-*").
        /// </summary>
        public static bool UsesOldLayout(ISkinSource skin) =>
            skin.GetConfig<SkinConfiguration.LegacySetting, decimal>(SkinConfiguration.LegacySetting.Version)?.Value <= 1.0m;

        /// <summary>
        /// Whether the skin draws the second page itself: its "ranking-panel" reaches down into it (XooMoon's does).
        /// Stable then shows only the second page's data (the player, the numbers, the options to click) over the skin's art,
        /// without its bands, titles and labels.
        /// </summary>
        public static bool HasSecondPageArt(ISkinSource skin) => (skin.GetTexture(@"ranking-panel")?.DisplayHeight ?? 0) > 700;

        /// <summary>
        /// A band's faint title on its left ("LOCAL", "ONLINE", "BEATMAP").
        /// </summary>
        public static Drawable CreateSectionTitle(string text) => new OsuSpriteText
        {
            Y = -6,
            Text = text,
            Font = OsuFont.GetFont(size: 58, weight: FontWeight.Light),
            Colour = new Color4(255, 255, 255, 60),
        };

        /// <summary>
        /// The purple "Online Ranking" button at the bottom of the first page, which scrolls to the second.
        /// </summary>
        private partial class OnlineRankingButton : ClickableContainer
        {
            public OnlineRankingButton(Action action)
            {
                Action = action;
                Anchor = Anchor.TopCentre;
                Origin = Anchor.BottomCentre;
                Y = LAYOUT_SIZE.Y;
                // 360 to 668 in the 1024px layout: centred 2px left of the middle.
                X = -2;
                Size = new Vector2(308, 40);

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(120, 30, 210, 255),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = "▼ Online Ranking ▼",
                        Font = OsuFont.GetFont(size: 28),
                    },
                };
            }
        }
    }
}

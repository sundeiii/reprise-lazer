// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Ranking
{
    /// <summary>
    /// The "BEATMAP" band of a legacy ranking screen's second page: the beatmap, how often it's been played and passed,
    /// and below it where stable asks for a rating.
    /// </summary>
    /// <remarks>
    /// Measured against stable's ranking screen at 1024x768, scrolled down. The band is 53px tall; the rating line 91px below it.
    /// </remarks>
    public partial class LegacyRankingBeatmapSection : CompositeDrawable
    {
        public const float BAND_HEIGHT = 53;

        private static readonly Color4 band_colour = new Color4(0, 130, 255, 80);

        private readonly ScoreInfo score;
        private readonly LegacyRankingDetails details;

        private OsuSpriteText counts = null!;

        public LegacyRankingBeatmapSection(ScoreInfo score, LegacyRankingDetails details)
        {
            this.score = score;
            this.details = details;

            RelativeSizeAxes = Axes.X;
            Height = 180;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            var beatmap = score.BeatmapInfo;

            // A skin that draws the second page draws this band too: stable shows none of it then (the rating's stars are
            // still there to click, on the skin's vote bar).
            bool decorated = !LegacyRankingScreen.HasSecondPageArt(skin);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = BAND_HEIGHT,
                    Colour = band_colour,
                    Alpha = decorated ? 1 : 0,
                },
                LegacyRankingScreen.CreateSectionTitle("BEATMAP").With(t => t.Alpha = decorated ? 1 : 0),
                // Stable keeps these at the left on wider screens (measured at 1920x1080).
                new OsuSpriteText
                {
                    Position = new Vector2(275, 3),
                    Text = beatmap == null
                        ? string.Empty
                        : StableMetadata.ArtistTitle(beatmap.Metadata, after: $" [{beatmap.DifficultyName}] by {beatmap.Metadata.Author.Username}"),
                    Font = OsuFont.GetFont(size: 21, weight: FontWeight.Light),
                    Alpha = decorated ? 1 : 0,
                },
                counts = new OsuSpriteText
                {
                    Position = new Vector2(275, 25),
                    Font = OsuFont.GetFont(size: 19, weight: FontWeight.Light),
                    Alpha = decorated ? 1 : 0,
                },
                // Stable's rating: the message 91 below the band's top, the stars under it.
                new LegacyRankingRating(beatmap?.BeatmapSet?.OnlineID ?? -1, decorated) { Y = 91 },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            details.OnlineBeatmap.BindValueChanged(b => showCounts(b.NewValue), true);
        }

        private void showCounts(APIBeatmap? beatmap)
        {
            if (beatmap == null)
            {
                counts.Text = string.Empty;
                return;
            }

            var since = beatmap.BeatmapSet?.Ranked ?? beatmap.BeatmapSet?.Submitted;
            string plays = $"{beatmap.PlayCount.ToString(@"#,0", CultureInfo.InvariantCulture)} plays";
            string sinceText = since != null ? $" since {since.Value.ToString(@"MMM, yyyy", CultureInfo.InvariantCulture)}" : string.Empty;
            string passRate = beatmap.PlayCount > 0
                ? $" | {(beatmap.PassCount * 100.0 / beatmap.PlayCount).ToString(@"0", CultureInfo.InvariantCulture)}% pass rate"
                : string.Empty;

            counts.Text = plays + sinceText + passRate;
        }
    }
}

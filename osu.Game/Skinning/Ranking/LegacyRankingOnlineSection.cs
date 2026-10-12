// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Ranking
{
    /// <summary>
    /// The "ONLINE" band of a legacy ranking screen's second page, as modern stable's: "Beatmap Ranking" (this play against the player's
    /// best before it: its place, accuracy, combo, score and pp) and "Overall Ranking" (the player's rank, accuracy, max combo, ranked
    /// and total score and pp), each with how much it changed, green where it went up.
    /// </summary>
    /// <remarks>
    /// Measured against stable's ranking screen at 1024x768, scrolled down. 272px tall.
    /// </remarks>
    public partial class LegacyRankingOnlineSection : CompositeDrawable
    {
        public const float HEIGHT = 272;

        private const float header_y = 75;
        private const float row_y = 85;
        private const float row_height = 36;
        private const float row_spacing = 38;

        private static readonly Color4 band_colour = new Color4(255, 0, 170, 70);
        private static readonly Color4 label_colour = new Color4(255, 190, 40, 255);
        private static readonly Color4 dark_cell = new Color4(30, 30, 30, 200);
        private static readonly Color4 better_cell = new Color4(100, 150, 20, 230);

        // Columns: left edge and width, measured against stable at 1920x1080 (the layout centred: 171 in).
        private static readonly (float X, float Width) label_column = (5, 158);
        private static readonly (float X, float Width) overall_column = (165, 110);
        private static readonly (float X, float Width) accuracy_column = (277, 110);
        private static readonly (float X, float Width) max_combo_column = (389, 110);
        private static readonly (float X, float Width) ranked_score_column = (501, 174);
        private static readonly (float X, float Width) total_score_column = (677, 174);
        private static readonly (float X, float Width) performance_column = (853, 110);

        private readonly Bindable<ScoringMode> scoringMode = new Bindable<ScoringMode>();

        private readonly ScoreInfo score;
        private readonly LegacyRankingDetails details;

        private Container cells = null!;
        private bool decorated;

        public LegacyRankingOnlineSection(ScoreInfo score, LegacyRankingDetails details)
        {
            this.score = score;
            this.details = details;

            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, OsuConfigManager config)
        {
            config.BindWith(OsuSetting.ScoreDisplayMode, scoringMode);

            // With a skin that draws the second page, only the numbers show (over the skin's cells).
            decorated = !LegacyRankingScreen.HasSecondPageArt(skin);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = band_colour,
                    Alpha = decorated ? 1 : 0,
                },
                LegacyRankingScreen.CreateSectionTitle("ONLINE").With(t => t.Alpha = decorated ? 1 : 0),
                LegacyRankingScreen.CreateCentredContent(
                    header("Overall", overall_column),
                    header("Accuracy", accuracy_column),
                    header("Max Combo", max_combo_column),
                    header("Ranked Score", ranked_score_column),
                    header("Total Score", total_score_column),
                    header("Performance", performance_column),
                    cells = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    }).With(c =>
                {
                    // The headers.
                    foreach (var header in c.Children.OfType<OsuSpriteText>())
                        header.Alpha = decorated ? 1 : 0;
                }),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            details.StatisticsUpdate.BindValueChanged(_ => showCells());
            details.UserBestPosition.BindValueChanged(_ => showCells());
            details.PreviousBest.BindValueChanged(_ => showCells());
            details.Performance.BindValueChanged(_ => showCells());
            scoringMode.BindValueChanged(_ => showCells());
            details.CurrentStatistics.BindValueChanged(_ => showCells(), true);
        }

        private void showCells()
        {
            var update = details.StatisticsUpdate.Value;

            // Without a before-and-after from the server: the player's statistics now, without changes.
            var before = update?.Before;
            var after = update?.After ?? details.CurrentStatistics.Value;

            float second = row_y + row_spacing;

            cells.Children = beatmapRow(row_y).Concat(new[]
            {
                cell(label_column, second, dark_cell, "Overall Ranking", null, label_colour, 16).With(c => c.Alpha = decorated ? 1 : 0),
                // A rank goes up as its number goes down.
                compare(overall_column, second, after?.GlobalRank, before?.GlobalRank, r => $"#{r.ToString(@"#,0", culture)}", d => signed(-d, @"#,0", "#"), lowerIsBetter: true),
                compare(accuracy_column, second, after?.Accuracy, before?.Accuracy, a => $"{a.ToString(@"0.00", culture)}%", d => signed(d, @"0.00") + "%"),
                cell(max_combo_column, second, dark_cell, after != null ? $"{after.MaxCombo.ToString(@"#,0", culture)}x" : "-", "-"),
                compare(ranked_score_column, second, after?.RankedScore, before?.RankedScore, v => v.ToString(@"#,0", culture), d => signed(d, @"#,0")),
                compare(total_score_column, second, after?.TotalScore, before?.TotalScore, v => v.ToString(@"#,0", culture), d => signed(d, @"#,0")),
                compare(performance_column, second, (double?)after?.PP, (double?)before?.PP, p => $"{p.ToString(@"#,0", culture)}pp", d => signed(d, @"#,0") + "pp"),
            }).ToArray();
        }

        // Numbers as the player's system writes them, as stable does.
        private static CultureInfo culture => CultureInfo.CurrentCulture;

        // "Beatmap Ranking": this play against the player's best before it (green where this one is better, or where there wasn't one).
        private IEnumerable<Drawable> beatmapRow(float y)
        {
            var previous = details.PreviousBest.Value;
            var mode = scoringMode.Value;

            int? position = score.Position ?? details.UserBestPosition.Value;

            yield return cell(label_column, y, dark_cell, "Beatmap Ranking", null, label_colour, 16).With(c => c.Alpha = decorated ? 1 : 0);
            yield return cell(overall_column, y, position != null && (previous == null || score.Position != null) ? better_cell : dark_cell,
                position is int p ? $"#{p.ToString(@"#,0", culture)}" : "-", null);
            yield return compare(accuracy_column, y, score.Accuracy * 100, previous?.Accuracy * 100, a => $"{a.ToString(@"0.00", culture)}%", d => signed(d, @"0.00") + "%",
                newIsBetter: previous == null);
            yield return compare(max_combo_column, y, score.MaxCombo, previous?.MaxCombo, c => $"{c.ToString(@"#,0", culture)}x", d => signed(d, @"#,0") + "x",
                newIsBetter: previous == null);
            yield return compare(ranked_score_column, y, score.GetDisplayScore(mode), previous?.GetDisplayScore(mode), v => v.ToString(@"#,0", culture), d => signed(d, @"#,0"),
                newIsBetter: previous == null);
            yield return cell(total_score_column, y, dark_cell, "-", null);
            yield return compare(performance_column, y, details.Performance.Value, previous?.PP, pp => $"{pp.ToString(@"#,0", culture)}pp", d => signed(d, @"#,0") + "pp",
                newIsBetter: previous == null);
        }

        // A value with how much it changed under it (green when it went up, or down for ranks).
        private static Drawable compare<T>((float X, float Width) column, float y, T? now, T? before, Func<T, string> format, Func<double, string> change,
                                           bool lowerIsBetter = false, bool newIsBetter = false)
            where T : struct, IConvertible
        {
            if (now is not T value)
                return cell(column, y, dark_cell, "-", null);

            double current = value.ToDouble(CultureInfo.InvariantCulture);
            double? previous = before?.ToDouble(CultureInfo.InvariantCulture);
            double difference = previous is double b ? current - b : 0;
            bool better = previous == null ? newIsBetter : lowerIsBetter ? difference < 0 : difference > 0;

            return cell(column, y, better ? better_cell : dark_cell, format(value),
                previous != null && difference != 0 ? $"({change(difference)})" : previous == null && !newIsBetter ? "-" : null);
        }

        private static Drawable header(string text, (float X, float Width) column) => new OsuSpriteText
        {
            X = column.X + column.Width / 2,
            Y = header_y,
            Origin = Anchor.BottomCentre,
            Text = text,
            Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
        };

        private static Drawable cell((float X, float Width) column, float y, Color4 background, string value, string? change, Color4? textColour = null, float size = 15,
                                     float? backingWidth = null) => new Container
        {
            Position = new Vector2(column.X, y),
            Size = new Vector2(column.Width, row_height),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = backingWidth == null ? Axes.Both : Axes.Y,
                    Width = backingWidth ?? 1,
                    Colour = background,
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = value,
                            // Bold only for the large ones (the label and the map rank).
                            Font = OsuFont.GetFont(size: size, weight: size > 15 ? FontWeight.Bold : FontWeight.Regular),
                            Colour = textColour ?? Color4.White,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = change ?? string.Empty,
                            Font = OsuFont.GetFont(size: 14),
                            Alpha = change == null ? 0 : 1,
                        },
                    }
                },
            }
        };

        // "+12" / "-12" ("+#12" for ranks, as stable's).
        private static string signed(double value, string format, string prefix = "") =>
            (value > 0 ? "+" : "-") + prefix + Math.Abs(value).ToString(format, culture);
    }
}

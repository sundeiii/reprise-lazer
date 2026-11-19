// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Overlays.Profile.Header.Components
{
    /// <summary>
    /// Reprise: the profile's "Solo Play" card, as the website's (detail-stats-v2/solo.tsx): the global ranking and the country one under
    /// it, pp and medals side by side; the grade counts in the middle; the rank graph ("No recent plays" without one) at the bottom.
    /// </summary>
    public partial class SoloPlayCard : SectionedProfileCard
    {
        private readonly Dictionary<ScoreRank, MainDetails.ScoreRankInfo> scoreRankInfos = new Dictionary<ScoreRank, MainDetails.ScoreRankInfo>();

        private GlobalRankDisplay globalRank = null!;
        private CardValueDisplay countryRank = null!;
        private CardValueDisplay ppInfo = null!;
        private CardValueDisplay medalInfo = null!;
        private RankGraph rankGraph = null!;

        public SoloPlayCard()
            : base(FontAwesome.Solid.User, UsersStrings.ShowSoloTitle)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            TopContent.Children = new Drawable[]
            {
                globalRank = new GlobalRankDisplay(),
                countryRank = new CardValueDisplay(CardValueSize.RankSmall) { Label = UsersStrings.ShowRankCountrySimple },
                // profile-detail-stats-card__values--row: pp and medals sharing the width.
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 5), new Dimension() },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            ppInfo = new CardValueDisplay(CardValueSize.Plain) { Label = "pp" },
                            Empty(),
                            medalInfo = new CardValueDisplay(CardValueSize.Plain) { Label = UsersStrings.ShowStatsMedals },
                        }
                    }
                },
            };

            // profile-rank-count--v2: the five grades, 16px, 8 apart, centred.
            MiddleContent.Child = new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8),
                Children = new[]
                {
                    scoreRankInfos[ScoreRank.XH] = new MainDetails.ScoreRankInfo(ScoreRank.XH, 16),
                    scoreRankInfos[ScoreRank.X] = new MainDetails.ScoreRankInfo(ScoreRank.X, 16),
                    scoreRankInfos[ScoreRank.SH] = new MainDetails.ScoreRankInfo(ScoreRank.SH, 16),
                    scoreRankInfos[ScoreRank.S] = new MainDetails.ScoreRankInfo(ScoreRank.S, 16),
                    scoreRankInfos[ScoreRank.A] = new MainDetails.ScoreRankInfo(ScoreRank.A, 16),
                }
            };

            // profile-detail-stats-card__chart: inset 15 from the right. The graph says "No recent plays" itself when unranked.
            BottomContent.Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Right = 15 },
                Child = rankGraph = new RankGraph { RelativeSizeAxes = Axes.Both },
            };
        }

        protected override void UpdateDisplay(UserProfileData? data)
        {
            var user = data?.User;
            bool restricted = user?.IsRestricted == true;
            bool hasStatistics = user?.HasStatistics == true;

            globalRank.HighestRank.Value = user?.RankHighest;
            globalRank.UserStatistics.Value = !restricted ? user?.Statistics : null;

            countryRank.Value.Text = !restricted && hasStatistics
                ? user!.Statistics.CountryRank?.ToLocalisableString("\\##,##0") ?? (LocalisableString)"-"
                : (LocalisableString)"-";
            countryRank.Value.TooltipText = !restricted ? MainDetails.GetCountryRankTooltipText(user) : default;

            ppInfo.Value.Text = !restricted && hasStatistics
                ? user!.Statistics.PP?.ToLocalisableString("#,##0") ?? (LocalisableString)"0"
                : (LocalisableString)"-";
            ppInfo.Value.TooltipText = !restricted ? MainDetails.GetPPInfoTooltipText(user) : default;
            medalInfo.Value.Text = restricted ? "-" : (user?.Achievements?.Length ?? 0).ToLocalisableString("#,##0");

            foreach (var info in scoreRankInfos)
                info.Value.RankCount = !restricted && hasStatistics ? user!.Statistics.GradesCount[info.Key] : 0;

            rankGraph.Statistics.Value = !restricted ? user?.Statistics : null;
        }
    }
}

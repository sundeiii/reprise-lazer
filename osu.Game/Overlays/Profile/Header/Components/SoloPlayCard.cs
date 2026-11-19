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
    /// Reprise: the profile's "Solo Play" card, as the website's: global and country ranking, pp and medals, the grade counts, and the
    /// rank graph.
    /// </summary>
    public partial class SoloPlayCard : ProfileStatsCard
    {
        private readonly Dictionary<ScoreRank, MainDetails.ScoreRankInfo> scoreRankInfos = new Dictionary<ScoreRank, MainDetails.ScoreRankInfo>();

        private GlobalRankDisplay globalRank = null!;
        private ProfileValueDisplay countryRank = null!;
        private ProfileValueDisplay ppInfo = null!;
        private ProfileValueDisplay medalInfo = null!;
        private RankGraph rankGraph = null!;
        private Container rankedPart = null!;

        public SoloPlayCard()
            : base(FontAwesome.Solid.User, "Solo Play")
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Body.Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(20, 0),
                    Children = new Drawable[]
                    {
                        globalRank = new GlobalRankDisplay(),
                        countryRank = new ProfileValueDisplay(true) { Title = UsersStrings.ShowRankCountrySimple },
                    }
                },
                rankedPart = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(10, 0),
                            Children = new Drawable[]
                            {
                                ppInfo = new ProfileValueDisplay { Title = "pp" },
                                medalInfo = new ProfileValueDisplay { Title = UsersStrings.ShowStatsMedals },
                            }
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(4),
                            Children = new[]
                            {
                                scoreRankInfos[ScoreRank.XH] = new MainDetails.ScoreRankInfo(ScoreRank.XH),
                                scoreRankInfos[ScoreRank.X] = new MainDetails.ScoreRankInfo(ScoreRank.X),
                                scoreRankInfos[ScoreRank.SH] = new MainDetails.ScoreRankInfo(ScoreRank.SH),
                                scoreRankInfos[ScoreRank.S] = new MainDetails.ScoreRankInfo(ScoreRank.S),
                                scoreRankInfos[ScoreRank.A] = new MainDetails.ScoreRankInfo(ScoreRank.A),
                            }
                        },
                    }
                },
                rankGraph = new RankGraph
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 60,
                },
            };
        }

        protected override void UpdateDisplay(UserProfileData? data)
        {
            var user = data?.User;
            bool restricted = user?.IsRestricted == true;
            bool hasStatistics = user?.HasStatistics == true;

            globalRank.HighestRank.Value = user?.RankHighest;
            globalRank.UserStatistics.Value = !restricted ? user?.Statistics : null;

            countryRank.Content.Text = !restricted && hasStatistics
                ? user!.Statistics.CountryRank?.ToLocalisableString("\\##,##0") ?? (LocalisableString)"-"
                : (LocalisableString)"-";
            countryRank.Content.TooltipText = !restricted ? MainDetails.GetCountryRankTooltipText(user) : default;

            ppInfo.Content.Text = !restricted && hasStatistics
                ? user!.Statistics.PP?.ToLocalisableString("#,##0") ?? (LocalisableString)"0"
                : (LocalisableString)"-";
            ppInfo.Content.TooltipText = !restricted ? MainDetails.GetPPInfoTooltipText(user) : default;
            medalInfo.Content.Text = restricted ? "-" : user?.Achievements?.Length.ToString() ?? "0";

            foreach (var info in scoreRankInfos)
                info.Value.RankCount = !restricted && hasStatistics ? user!.Statistics.GradesCount[info.Key] : 0;

            rankGraph.Statistics.Value = !restricted ? user?.Statistics : null;

            rankedPart.Alpha = restricted ? 0 : 1;
            rankGraph.Alpha = restricted ? 0 : 1;
        }
    }
}

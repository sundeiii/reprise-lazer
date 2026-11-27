// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Scoring;
using osu.Game.Localisation;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Screens.Ranking.Expanded.Statistics
{
    public partial class PerformanceStatistic : StatisticDisplay, IHasTooltip
    {
        public LocalisableString TooltipText { get; private set; }

        private readonly ScoreInfo score;

        private readonly Bindable<int> performance = new Bindable<int>();

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private RollingCounter<int> counter = null!;

        public PerformanceStatistic(ScoreInfo score)
            : base(BeatmapsetsStrings.ShowScoreboardHeaderspp)
        {
            this.score = score;
        }

        [Resolved]
        private IAPIProvider? api { get; set; }

        /// <summary>Reprise: how many more times to ask the server while it's still working the play's pp out.</summary>
        private int serverAttemptsLeft = 3;

        /// <summary>Reprise: the server's pp came in (Reprise's own rules); the local estimate no longer shows.</summary>
        private bool fromServer;

        [BackgroundDependencyLoader]
        private void load(BeatmapDifficultyCache difficultyCache, CancellationToken? cancellationToken)
        {
            fetchFromServer();

            if (score.PP.HasValue)
            {
                setPerformanceValue(score, score.PP.Value);
            }
            else
            {
                Task.Run(async () =>
                {
                    var attributes = await difficultyCache.GetDifficultyAsync(score.BeatmapInfo!, score.Ruleset, score.Mods, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
                    var performanceCalculator = score.Ruleset.CreateInstance().CreatePerformanceCalculator();

                    // Performance calculation requires the beatmap and ruleset to be locally available. If not, return a default value.
                    if (attributes?.DifficultyAttributes == null || performanceCalculator == null)
                        return;

                    var result = await performanceCalculator.CalculateAsync(score, attributes.Value.DifficultyAttributes, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);

                    Schedule(() => setPerformanceValue(score, result.Total));
                }, cancellationToken ?? CancellationToken.None);
            }
        }

        /// <summary>
        /// Reprise: the play's pp as the server counts it (Relax, Difficulty Adjust and a DT's speed by Reprise's rules,
        /// which the local calculator doesn't know), or what it would be worth on a map that gives none.
        /// </summary>
        private void fetchFromServer()
        {
            if (api == null || score.OnlineID <= 0 || serverAttemptsLeft-- <= 0)
                return;

            var request = new GetReprisePerformanceRequest(score.OnlineID);
            request.Success += result => Schedule(() =>
            {
                if (result.PP is not double pp)
                {
                    // Still being worked out: ask again in a few seconds.
                    Scheduler.AddDelayed(fetchFromServer, 3000);
                    return;
                }

                fromServer = true;
                performance.Value = (int)Math.Round(pp, MidpointRounding.AwayFromZero);

                if (!result.Ranked)
                {
                    Alpha = 0.5f;
                    TooltipText = "pp if ranked (not counted)";
                }
                else if (score.Rank != ScoreRank.F)
                {
                    Alpha = 1f;
                    TooltipText = default;
                }
            });
            api.Queue(request);
        }

        private void setPerformanceValue(ScoreInfo scoreInfo, double? pp)
        {
            if (fromServer)
                return;

            if (pp.HasValue)
            {
                performance.Value = (int)Math.Round(pp.Value, MidpointRounding.AwayFromZero);

                if (!scoreInfo.BeatmapInfo!.Status.GrantsPerformancePoints())
                {
                    Alpha = 0.5f;
                    // Reprise: shown as what it would be worth (the server's number replaces this estimate when it comes).
                    TooltipText = "pp if ranked (not counted)";
                }
                else if (hasUnrankedMods(scoreInfo))
                {
                    Alpha = 0.5f;
                    TooltipText = ResultsScreenStrings.NoPPForUnrankedMods;
                }
                else if (scoreInfo.Rank == ScoreRank.F)
                {
                    Alpha = 0.5f;
                    TooltipText = ResultsScreenStrings.NoPPForFailedScores;
                }
                else
                {
                    Alpha = 1f;
                    TooltipText = default;
                }
            }
        }

        private static bool hasUnrankedMods(ScoreInfo scoreInfo)
        {
            IEnumerable<Mod> modsToCheck = scoreInfo.Mods;

            if (scoreInfo.IsLegacyScore)
                modsToCheck = modsToCheck.Where(m => m is not ModClassic);

            return modsToCheck.Any(m => !m.Ranked);
        }

        public override void Appear()
        {
            base.Appear();
            counter.Current.BindTo(performance);
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellationTokenSource.Cancel();
            base.Dispose(isDisposing);
        }

        protected override Drawable CreateContent() => counter = new StatisticCounter
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre
        };
    }
}

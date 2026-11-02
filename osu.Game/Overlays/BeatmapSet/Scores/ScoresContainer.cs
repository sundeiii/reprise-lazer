// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;
using osuTK;
using APIUser = osu.Game.Online.API.Requests.Responses.APIUser;

namespace osu.Game.Overlays.BeatmapSet.Scores
{
    public partial class ScoresContainer : BeatmapSetLayoutSection
    {
        private const int spacing = 15;

        public readonly Bindable<APIBeatmap> Beatmap = new Bindable<APIBeatmap>();
        private readonly Bindable<IRulesetInfo> ruleset = new Bindable<IRulesetInfo>();
        private readonly Bindable<BeatmapLeaderboardScope> scope = new Bindable<BeatmapLeaderboardScope>(BeatmapLeaderboardScope.Global);
        private readonly IBindable<APIUser> user = new Bindable<APIUser>();

        private readonly Box background;
        private readonly ScoreTable scoreTable;
        private readonly FillFlowContainer topScoresContainer;
        private readonly LoadingLayer loading;
        private readonly LeaderboardModSelector modSelector;
        private readonly NoScoresPlaceholder noScoresPlaceholder;
        private readonly NotSupporterPlaceholder notSupporterPlaceholder;
        private readonly NoTeamPlaceholder noTeamPlaceholder;

        [Resolved]
        private IAPIProvider api { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; }

        /// <summary>
        /// Reprise: with Relax or Autopilot on (the toolbar's switch), the scores are that mode's (osurx, ...), as song select's.
        /// </summary>
        [Resolved(CanBeNull = true)]
        private IBindable<IReadOnlyList<Mod>> selectedMods { get; set; }

        private GetScoresRequest getScoresRequest;

        /// <summary>The leaderboard's ruleset: the beatmap's, or its Relax/Autopilot mode when that's on.</summary>
        private IRulesetInfo requestRuleset
        {
            get
            {
                var beatmapRuleset = Beatmap.Value.Ruleset;
                return rulesets.GetRuleset(beatmapRuleset.OnlineID)?.ForMods(selectedMods?.Value ?? Array.Empty<Mod>()) ?? beatmapRuleset;
            }
        }

        private CancellationTokenSource loadCancellationSource;

        protected APIScoresCollection Scores
        {
            set => Schedule(() =>
            {
                loadCancellationSource?.Cancel();
                loadCancellationSource = new CancellationTokenSource();

                topScoresContainer.Clear();
                scoreTable.ClearScores();
                scoreTable.Hide();

                loading.Hide();
                loading.FinishTransforms();

                if (value?.Scores.Any() != true)
                    return;

                var apiBeatmap = Beatmap.Value;

                Debug.Assert(apiBeatmap != null);

                // TODO: temporary. should be removed once `OrderByTotalScore` can accept `IScoreInfo`.
                var beatmapInfo = new BeatmapInfo
                {
#pragma warning disable 618
                    MaxCombo = apiBeatmap.MaxCombo,
#pragma warning restore 618
                    Status = apiBeatmap.Status,
                    MD5Hash = apiBeatmap.MD5Hash
                };

                var scores = value.Scores.Select(s => s.ToScoreInfo(rulesets, beatmapInfo)).OrderByServerOr(variant).ToArray();
                var topScore = scores.First();

                scoreTable.DisplayScores(scores, apiBeatmap.Status.GrantsPerformancePoints());
                scoreTable.Show();

                var userScore = value.UserScore;
                var userScoreInfo = userScore?.Score.ToScoreInfo(rulesets, beatmapInfo);

                topScoresContainer.Add(new DrawableTopScore(topScore));

                if (userScoreInfo != null && userScoreInfo.OnlineID != topScore.OnlineID)
                    topScoresContainer.Add(new DrawableTopScore(userScoreInfo, userScore.Position));
            });
        }

        public ScoresContainer()
        {
            AddRange(new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING },
                    Margin = new MarginPadding { Vertical = 20 },
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, spacing),
                            Children = new Drawable[]
                            {
                                new LeaderboardScopeSelector
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Current = { BindTarget = scope }
                                },
                                modSelector = new LeaderboardModSelector
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Ruleset = { BindTarget = ruleset }
                                }
                            }
                        },
                        new Container
                        {
                            AutoSizeAxes = Axes.Y,
                            RelativeSizeAxes = Axes.X,
                            Margin = new MarginPadding { Top = spacing },
                            Children = new Drawable[]
                            {
                                noScoresPlaceholder = new NoScoresPlaceholder
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Alpha = 0,
                                    AlwaysPresent = true,
                                    Margin = new MarginPadding { Vertical = 10 }
                                },
                                noTeamPlaceholder = new NoTeamPlaceholder
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Margin = new MarginPadding { Vertical = 10 },
                                    Alpha = 0,
                                },
                                notSupporterPlaceholder = new NotSupporterPlaceholder
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Margin = new MarginPadding { Vertical = 10 },
                                    Alpha = 0,
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, spacing),
                                    Children = new Drawable[]
                                    {
                                        topScoresContainer = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 5),
                                        },
                                        scoreTable = new ScoreTable
                                        {
                                            Anchor = Anchor.TopCentre,
                                            Origin = Anchor.TopCentre,
                                        }
                                    }
                                },
                            }
                        }
                    },
                },
                loading = new LoadingLayer()
            });
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            background.Colour = colourProvider.Background5;

            user.BindTo(api.LocalUser);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            scope.BindValueChanged(_ => getScores());
            ruleset.BindValueChanged(_ => getScores());

            modSelector.SelectedMods.CollectionChanged += (_, _) => getScores();

            // Turning Relax or Autopilot on or off is another leaderboard.
            selectedMods?.BindValueChanged(mods =>
            {
                if (Beatmap.Value?.Ruleset is not IRulesetInfo beatmapRuleset || rulesets.GetRuleset(beatmapRuleset.OnlineID) is not RulesetInfo r)
                    return;
                if (r.ForMods(mods.OldValue ?? Array.Empty<Mod>()).ShortName != r.ForMods(mods.NewValue ?? Array.Empty<Mod>()).ShortName)
                    getScores();
            });

            Beatmap.BindValueChanged(onBeatmapChanged);
            user.BindValueChanged(onUserChanged, true);
        }

        private void onBeatmapChanged(ValueChangedEvent<APIBeatmap> beatmap)
        {
            var beatmapRuleset = beatmap.NewValue?.Ruleset;

            if (ruleset.Value?.OnlineID == beatmapRuleset?.OnlineID)
            {
                modSelector.DeselectAll();
                ruleset.TriggerChange();
            }
            else
                ruleset.Value = beatmapRuleset;

            scope.Value = BeatmapLeaderboardScope.Global;
        }

        private void onUserChanged(ValueChangedEvent<APIUser> user)
        {
            if (modSelector.SelectedMods.Any())
                modSelector.DeselectAll();
            else
                getScores();
        }

        private void getScores()
        {
            getScoresRequest?.Cancel();
            getScoresRequest = null;

            noScoresPlaceholder.Hide();
            noTeamPlaceholder.Hide();
            notSupporterPlaceholder.Hide();

            if (Beatmap.Value == null || Beatmap.Value.OnlineID <= 0 || (Beatmap.Value.Status <= BeatmapOnlineStatus.Pending))
            {
                Scores = null;
                Hide();
                return;
            }

            if ((scope.Value == BeatmapLeaderboardScope.Team) && user.Value.Team == null)
            {
                Scores = null;
                noTeamPlaceholder.Show();
                return;
            }

            if (scope.Value.RequiresSupporter(modSelector.SelectedMods.Count > 0) && !userIsSupporter)
            {
                Scores = null;
                notSupporterPlaceholder.Show();
                return;
            }

            Show();
            loading.Show();

            var ruleset = requestRuleset;
            getScoresRequest = new GetScoresRequest(Beatmap.Value, ruleset, scope.Value, modSelector.SelectedMods);
            getScoresRequest.Success += scores =>
            {
                variant = ruleset.ShortName != Beatmap.Value.Ruleset.ShortName;
                Scores = scores;

                if (!scores.Scores.Any())
                    noScoresPlaceholder.ShowWithScope(scope.Value);
            };

            api.Queue(getScoresRequest);
        }

        /// <summary>Whether the scores shown are a Relax/Autopilot mode's (kept in the server's order).</summary>
        private bool variant;

        private bool userIsSupporter => api.IsLoggedIn && api.LocalUser.Value.IsSupporter;
    }
}

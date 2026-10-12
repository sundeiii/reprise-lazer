// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Online;
using osu.Game.Users;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Placeholders;
using osu.Game.Overlays;
using osu.Game.Overlays.Volume;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Screens.Ranking.Statistics.Session;
using osu.Game.Skinning;
using osu.Game.Skinning.Ranking;
using osuTK;
using Realms;

namespace osu.Game.Screens.Ranking
{
    [Cached]
    public abstract partial class ResultsScreen : ScreenWithBeatmapBackground, IKeyBindingHandler<GlobalAction>, IHasStableLayout
    {
        protected const float BACKGROUND_BLUR = 10;
        private static readonly float screen_height = 768 - TwoLayerButton.SIZE_EXTENDED.Y;

        public override bool DisallowExternalBeatmapRulesetChanges => true;

        public override bool? AllowGlobalTrackControl => true;

        protected override OverlayActivation InitialOverlayActivationMode => OverlayActivation.UserTriggered;

        public readonly Bindable<ScoreInfo?> SelectedScore = new Bindable<ScoreInfo?>();

        public readonly ScoreInfo? Score;

        protected ScorePanelList ScorePanelList { get; private set; } = null!;

        protected VerticalScrollContainer VerticalScrollContent { get; private set; } = null!;
        protected int? StableCountryMapRank { get; set; }

        [Resolved]
        private Player? player { get; set; }

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private SessionStatsStore? sessionStats { get; set; }

        private bool skipExitTransition;

        protected StatisticsPanel StatisticsPanel { get; private set; } = null!;

        private Drawable bottomPanel = null!;
        private StableResultsPanel? stableResultsPanel;
        private Container<ScorePanel> detachedPanelContainer = null!;

        private Task lastFetchTask = Task.CompletedTask;

        /// <summary>
        /// Whether the user can retry the beatmap from the results screen.
        /// </summary>
        public bool AllowRetry { get; init; }

        /// <summary>
        /// Whether the user can watch the replay of the completed play from the results screen.
        /// </summary>
        public bool AllowWatchingReplay { get; init; } = true;

        /// <summary>
        /// Whether the provided score is for a local user's play.
        /// This will trigger elements like the user's ranking to display.
        /// </summary>
        public bool IsLocalPlay { get; init; }

        private Sample? popInSample;

        // osu!stable's layout instead of this screen's, with a legacy skin (OsuSetting.StableLayouts).
        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private Bindable<bool> stableLayouts = null!;
        private Drawable lazerContent = null!;
        private StableLayoutContainer stableContent = null!;
        private ReplayDownloadButton? replayButton;
        private FavouriteButton? favouriteButton;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private UserStatisticsWatcher? userStatisticsWatcher { get; set; }

        [Resolved]
        private LocalUserStatisticsProvider? localUserStatistics { get; set; }

        [Resolved]
        private OsuGame? osuGame { get; set; }

        private bool stableShown;

        public bool StableLayoutShown => stableShown;

        // osu!stable has no toolbar: it's hidden while osu!stable's layout is shown.
        public override bool HideOverlaysOnEnter => stableShown;

        private readonly Bindable<UserStatistics?> stableCurrentStatistics = new Bindable<UserStatistics?>();
        private readonly Bindable<int?> stableUserBest = new Bindable<int?>();
        private readonly Bindable<double?> stableToNextRank = new Bindable<double?>();
        private GetScoresRequest? stableScoresRequest;
        private GetUserRankingsRequest? stableRankingsRequest;
        private int? stableRankingsFor;

        private readonly Bindable<ScoreBasedUserStatisticsUpdate?> stableStatisticsUpdate = new Bindable<ScoreBasedUserStatisticsUpdate?>();
        private readonly IBindable<ScoreBasedUserStatisticsUpdate?> latestStatisticsUpdate = new Bindable<ScoreBasedUserStatisticsUpdate?>();
        private readonly Bindable<APIBeatmap?> stableOnlineBeatmap = new Bindable<APIBeatmap?>();
        private readonly Bindable<bool> stableFavourited = new Bindable<bool>();
        private GetBeatmapRequest? stableBeatmapRequest;
        private ScoreInfo? stableScore;

        // The online table's "Beatmap Ranking" row: the player's best before this play, and this play's pp.
        private readonly Bindable<SoloScoreInfo?> stablePreviousBest = new Bindable<SoloScoreInfo?>();
        private readonly Bindable<double?> stablePerformance = new Bindable<double?>();
        private GetUserBeatmapScoresRequest? stablePreviousBestRequest;
        private CancellationTokenSource? stablePerformanceCancellation;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        protected ResultsScreen(ScoreInfo? score)
        {
            Score = score;

            SelectedScore.Value = score;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            FillFlowContainer buttons;

            popInSample = audio.Samples.Get(@"UI/overlay-pop-in");

            // only finished plays by the local user have hit events to summarise (this excludes replays and autoplay).
            if (Score != null
                && IsLocalPlay
                && player != null
                && player is not ReplayPlayer
                && !Score.User.IsBot
                && Score.HitEvents.Count > 0)
            {
                sessionStats?.Record(Score);
            }

            bool playApplause = Score != null
                                && player != null
                                && !Score.User.IsBot;

            bool showProfileUpdate = Score != null
                                    && IsLocalPlay
                                    && player != null
                                    && player is not ReplayPlayer
                                    && !Score.User.IsBot;

            bool useCleanResultsScreen = config.Get<ResultScreenStyle>(OsuSetting.ResultScreenStyle) == ResultScreenStyle.Clean;

            InternalChild = lazerContent = new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            VerticalScrollContent = new VerticalScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                ScrollbarVisible = false,
                                Child = new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        new GlobalScrollAdjustsVolume(),
                                        Score != null && useCleanResultsScreen
                                            ? new FillFlowContainer
                                            {
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                AutoSizeAxes = Axes.Both,
                                                Direction = FillDirection.Horizontal,
                                                Spacing = new Vector2(18, 0),
                                                Children = new Drawable[]
                                                {
                                                    stableResultsPanel = new StableResultsPanel(Score, playApplause),
                                                    showProfileUpdate
                                                        ? new StableProfileDeltaPanel(Score, true)
                                                        : new Container
                                                        {
                                                            Anchor = Anchor.Centre,
                                                            Origin = Anchor.Centre,
                                                            Size = Vector2.Zero,
                                                        },
                                                }
                                            }
                                            : new Container(),
                                        StatisticsPanel = new StatisticsPanel
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Score = { BindTarget = SelectedScore },
                                            AchievedScore = IsLocalPlay && Score != null ? Score : null,
                                        },
                                        ScorePanelList = new ScorePanelList
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Alpha = useCleanResultsScreen ? 0 : 1,
                                            HandleInput = !useCleanResultsScreen,
                                            SelectedScore = { BindTarget = SelectedScore },
                                            PostExpandAction = () => StatisticsPanel.ToggleVisibility()
                                        },
                                        detachedPanelContainer = new Container<ScorePanel>
                                        {
                                            RelativeSizeAxes = Axes.Both
                                        },
                                    }
                                }
                            },
                        },
                        new[]
                        {
                            bottomPanel = new Container
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                RelativeSizeAxes = Axes.X,
                                Height = TwoLayerButton.SIZE_EXTENDED.Y,
                                Alpha = 0,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4Extensions.FromHex("#333")
                                    },
                                    buttons = new FillFlowContainer
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        AutoSizeAxes = Axes.Both,
                                        Spacing = new Vector2(5),
                                        Direction = FillDirection.Horizontal
                                    },
                                }
                            }
                        }
                    },
                    RowDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize)
                    }
                }
            };

            if (Score != null)
            {
                // only show flair / animation when arriving after watching a play that isn't autoplay.
                bool shouldFlair = !useCleanResultsScreen && showProfileUpdate;

                ScorePanelList.AddScore(Score, shouldFlair);
                // this is mostly for medal display.
                // we don't want the medal animation to trample on the results screen animation, so we (ab)use `OverlayActivationMode`
                // to give the results screen enough time to play the animation out before the medals can be shown.
                Scheduler.AddDelayed(() => OverlayActivationMode.Value = OverlayActivation.All, shouldFlair ? AccuracyCircle.TOTAL_DURATION + 1000 : 0);
            }

            bool allowHotkeyRetry = false;

            if (AllowWatchingReplay)
            {
                buttons.Add(replayButton = new ReplayDownloadButton(SelectedScore.Value)
                {
                    Score = { BindTarget = SelectedScore },
                    Width = 300
                });

                // for simplicity, only allow this when coming from a replay player where we know the replay is ready to be played.
                //
                // if we show it in all cases, consider the case where a user comes from song select and potentially has to download
                // the replay before it can be played back. it wouldn't flow well with the quick retry in such a case.
                allowHotkeyRetry = player is ReplayPlayer;
            }

            if (player != null && AllowRetry)
            {
                buttons.Add(new RetryButton { Width = 300 });
                allowHotkeyRetry = true;
            }

            if (allowHotkeyRetry)
            {
                AddInternal(new HotkeyRetryOverlay
                {
                    Action = () =>
                    {
                        if (!this.IsCurrentScreen()) return;

                        skipExitTransition = true;
                        player?.Restart(true);
                    },
                });
            }

            if (Score?.BeatmapInfo != null)
                buttons.Add(new CollectionButton(Score.BeatmapInfo));

            if (Score?.BeatmapInfo?.BeatmapSet != null && Score.BeatmapInfo.BeatmapSet.OnlineID > 0)
                buttons.Add(favouriteButton = new FavouriteButton(Score.BeatmapInfo.BeatmapSet));

            // This screen keeps running underneath osu!stable's layout (fetching scores, the applause, ...), just unseen.
            lazerContent.AlwaysPresent = true;

            AddInternal(stableContent = new StableLayoutContainer
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (localUserStatistics != null)
                localUserStatistics.StatisticsUpdated += onLocalStatisticsUpdated;

            StatisticsPanel.State.BindValueChanged(onStatisticsStateChanged, true);

            stableLayouts = config.GetBindable<bool>(OsuSetting.StableLayouts);
            stableLayouts.BindValueChanged(_ => updateLayout());
            skinManager.CurrentSkin.BindValueChanged(_ => updateLayout());
            SelectedScore.BindValueChanged(_ => updateLayout(), true);

            if (userStatisticsWatcher != null)
            {
                latestStatisticsUpdate.BindTo(userStatisticsWatcher.LatestUpdate);
                latestStatisticsUpdate.BindValueChanged(update =>
                {
                    if (stableScore != null && update.NewValue?.Score.MatchesOnlineID(stableScore) == true)
                        stableStatisticsUpdate.Value = update.NewValue;
                }, true);
            }

            stableStatisticsUpdate.BindValueChanged(_ => updateToNextRank());
            // Processed: its own id is known, so it's left out of "before".
            stableStatisticsUpdate.BindValueChanged(_ => fetchPreviousBest());
            stableCurrentStatistics.BindValueChanged(_ => updateToNextRank());

            favouriteButton?.FavouriteState.BindValueChanged(state => stableFavourited.Value = state.NewValue.Favourited, true);

            fetchScores(null);
        }

        protected override void Update()
        {
            base.Update();

            if (ScorePanelList.IsScrolledToStart)
                fetchScores(-1);
            else if (ScorePanelList.IsScrolledToEnd)
                fetchScores(1);
        }

        /// <summary>
        /// Shows osu!stable's layout for the selected score when the setting is on and the skin is a legacy one, else this screen's.
        /// </summary>
        private void updateLayout()
        {
            var score = SelectedScore.Value;
            bool stable = stableLayouts.Value && skinManager.CurrentSkin.Value is LegacySkin && score != null;

            lazerContent.Alpha = stable ? 0 : 1;
            stableContent.Alpha = stable ? 1 : 0;

            // osu!stable's layout has the skin's back button instead of the game's.
            BackButtonVisibility.Value = !stable;

            if (stable != stableShown)
            {
                stableShown = stable;

                if (this.IsCurrentScreen())
                {
                    if (stable)
                        osuGame?.Toolbar.Hide();
                    else
                        osuGame?.Toolbar.Show();
                }
            }

            if (!stable || score == stableScore)
                return;

            stableScore = score;

            Action? retry = player != null && AllowRetry && score == Score
                ? () =>
                {
                    if (!this.IsCurrentScreen()) return;

                    skipExitTransition = true;
                    player?.Restart(true);
                }
                : null;

            Action? watchReplay = replayButton != null ? () => replayButton.Activate() : null;

            // The second page's data for this score: what's known now, and what arrives later.
            stableStatisticsUpdate.Value = latestStatisticsUpdate.Value?.Score.MatchesOnlineID(score!) == true ? latestStatisticsUpdate.Value : null;
            stableOnlineBeatmap.Value = null;
            stableBeatmapRequest?.Cancel();

            if (api.IsLoggedIn && score!.BeatmapInfo?.OnlineID > 0)
            {
                var request = stableBeatmapRequest = new GetBeatmapRequest(score.BeatmapInfo);
                request.Success += beatmap => Schedule(() =>
                {
                    if (stableBeatmapRequest == request)
                        stableOnlineBeatmap.Value = beatmap;
                });
                api.Queue(request);
            }

            // The player's statistics now, for their own scores (the server may send no before-and-after for one).
            stableCurrentStatistics.Value = isOwnScore(score!) ? localUserStatistics?.GetStatisticsFor(score!.Ruleset.ForMods(score.Mods)) : null;

            // Not after watching a replay (stable's online ranking is for scores just set).
            bool hasSecondPage = player != null && player is not ReplayPlayer && score == Score;

            // The second page's online parts: the player's best place on the beatmap, and how far the next player up is.
            stableUserBest.Value = null;
            stableToNextRank.Value = null;
            stableRankingsFor = null;

            if (hasSecondPage && api.IsLoggedIn && score.BeatmapInfo?.OnlineID > 0)
            {
                var scoresRequest = stableScoresRequest = new GetScoresRequest(score.BeatmapInfo, score.Ruleset);
                scoresRequest.Success += scores => Schedule(() =>
                {
                    if (stableScoresRequest == scoresRequest)
                        stableUserBest.Value = scores.UserScore?.Position;
                });
                api.Queue(scoresRequest);
            }

            stablePreviousBest.Value = null;
            stablePerformance.Value = null;

            if (hasSecondPage)
            {
                fetchPreviousBest();
                calculatePerformance(score!);
            }

            var details = new LegacyRankingDetails
            {
                LocalRank = localRankOf(score!),
                PreviousBest = stablePreviousBest,
                Performance = stablePerformance,
                StatisticsUpdate = stableStatisticsUpdate,
                CurrentStatistics = stableCurrentStatistics,
                // Only after playing it: a score opened from a leaderboard (or a replay) has no rankings page.
                HasSecondPage = hasSecondPage,
                UserBestPosition = stableUserBest,
                ToNextRank = stableToNextRank,
                OnlineBeatmap = stableOnlineBeatmap,
                ExportReplay = replayButton != null ? replayButton.Export : null,
                ToggleFavourite = favouriteButton != null ? () => favouriteButton.TriggerClick() : null,
                Favourited = stableFavourited,
                Back = () =>
                {
                    if (this.IsCurrentScreen() && !OnBackButton())
                        this.Exit();
                },
            };

            // Health over the play isn't recorded with a score yet: the graph shows without its line.
            stableContent.Child = new LegacyRankingScreen(score!, Array.Empty<Vector2>(), retry, watchReplay, details);
        }

        // The player's best on the beatmap other than this play (by score), for the "Beatmap Ranking" row.
        private void fetchPreviousBest()
        {
            var score = stableScore;

            if (score == null || score != Score || !api.IsLoggedIn || score.BeatmapInfo?.OnlineID is not int beatmapId || beatmapId <= 0)
                return;

            stablePreviousBestRequest?.Cancel();
            var request = stablePreviousBestRequest = new GetUserBeatmapScoresRequest(beatmapId, api.LocalUser.Value.OnlineID, score.Ruleset);

            request.Success += response => Schedule(() =>
            {
                if (stablePreviousBestRequest != request)
                    return;

                long thisScore = stableStatisticsUpdate.Value?.Score.OnlineID ?? score.OnlineID;

                stablePreviousBest.Value = response.Scores
                                                   // Not this play (before it's processed, by when it was set and its score).
                                                   .Where(s => thisScore > 0
                                                       ? s.OnlineID != thisScore
                                                       : !(s.TotalScore == score.TotalScore && Math.Abs((s.EndedAt - score.Date).TotalMinutes) < 2))
                                                   .MaxBy(s => s.TotalScore);
            });

            api.Queue(request);
        }

        // This play's pp: the server's once processed, else worked out here (as the game's own results screen does).
        private void calculatePerformance(ScoreInfo score)
        {
            stablePerformanceCancellation?.Cancel();

            if (score.PP is double pp)
            {
                stablePerformance.Value = pp;
                return;
            }

            var cancellation = stablePerformanceCancellation = new CancellationTokenSource();

            Task.Run(async () =>
            {
                var attributes = await difficultyCache.GetDifficultyAsync(score.BeatmapInfo!, score.Ruleset, score.Mods, cancellation.Token).ConfigureAwait(false);
                var calculator = score.Ruleset.CreateInstance().CreatePerformanceCalculator();

                if (attributes?.DifficultyAttributes == null || calculator == null)
                    return;

                var result = await calculator.CalculateAsync(score, attributes.Value.DifficultyAttributes, cancellation.Token).ConfigureAwait(false);
                Schedule(() =>
                {
                    if (!cancellation.IsCancellationRequested)
                        stablePerformance.Value = result.Total;
                });
            }, cancellation.Token);
        }

        // The performance to pass the next player up: from the rankings page they're on (50 a page).
        private void updateToNextRank()
        {
            var statistics = stableStatisticsUpdate.Value?.After ?? stableCurrentStatistics.Value;

            if (stableScore == null || statistics?.GlobalRank is not int rank || rank <= 1 || statistics.PP is not decimal pp)
            {
                stableToNextRank.Value = null;
                return;
            }

            if (stableRankingsFor == rank || !api.IsLoggedIn)
                return;

            stableRankingsFor = rank;

            int nextRank = rank - 1;
            int page = (nextRank - 1) / 50 + 1;
            var request = stableRankingsRequest = new GetUserRankingsRequest(stableScore.Ruleset, UserRankingsType.Performance, page);

            request.Success += rankings => Schedule(() =>
            {
                if (stableRankingsRequest != request)
                    return;

                int index = nextRank - 1 - (page - 1) * 50;
                var next = index < rankings.Users.Count ? rankings.Users[index] : null;

                stableToNextRank.Value = next?.PP is decimal nextPP ? Math.Max(0, (double)(nextPP - pp)) : null;
            });

            api.Queue(request);
        }

        // By id, or by name: a stored score's user may not carry the id the server knows the player by.
        private bool isOwnScore(ScoreInfo score)
        {
            var localUser = api.LocalUser.Value;

            if (localUser.OnlineID <= 1)
                return false;

            return score.UserID == localUser.OnlineID || string.Equals(score.User.Username, localUser.Username, StringComparison.OrdinalIgnoreCase);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (localUserStatistics != null)
                localUserStatistics.StatisticsUpdated -= onLocalStatisticsUpdated;

            stablePerformanceCancellation?.Cancel();
            stablePreviousBestRequest?.Cancel();

            base.Dispose(isDisposing);
        }

        private void onLocalStatisticsUpdated(UserStatisticsUpdate update)
        {
            // Statistics that arrive after the stable layout was made (the first fetch, or a refresh).
            Schedule(() =>
            {
                if (stableScore != null && isOwnScore(stableScore) && update.Ruleset.ShortName == stableScore.Ruleset.ForMods(stableScore.Mods).ShortName)
                    stableCurrentStatistics.Value = update.NewStatistics;
            });
        }

        /// <summary>
        /// The score's place among the local scores on its beatmap and ruleset (1 for the best), or null when it isn't stored locally.
        /// </summary>
        private int? localRankOf(ScoreInfo score)
        {
            if (score.BeatmapInfo == null)
                return null;

            return realm.Run(r =>
            {
                var local = r.All<ScoreInfo>()
                             .Filter($"{nameof(ScoreInfo.BeatmapInfo)}.{nameof(BeatmapInfo.ID)} == $0"
                                     + $" AND {nameof(ScoreInfo.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $1"
                                     + $" AND {nameof(ScoreInfo.DeletePending)} == false", score.BeatmapInfo.ID, score.Ruleset.ShortName)
                             .AsEnumerable()
                             .Select(s => (s.ID, s.TotalScore))
                             .ToList();

                if (local.All(s => s.ID != score.ID))
                    return (int?)null;

                return local.Count(s => s.TotalScore > score.TotalScore) + 1;
            });
        }

        /// <summary>
        /// osu!stable's layout over this screen: the mouse doesn't reach the screen underneath (the keyboard does, for back and retry).
        /// </summary>
        private partial class StableLayoutContainer : Container
        {
            protected override bool Handle(UIEvent e)
            {
                if (Alpha > 0 && e is MouseEvent or ScrollEvent)
                    return true;

                return base.Handle(e);
            }
        }

        #region Applause

        private PoolableSkinnableSample? rankApplauseSound;

        public void PlayApplause(ScoreRank rank)
        {
            const double applause_volume = 0.8f;

            // None after watching a replay.
            if (!this.IsCurrentScreen() || player is ReplayPlayer)
                return;

            rankApplauseSound?.Dispose();

            var applauseSamples = new List<string>();

            if (rank >= ScoreRank.B)
                // when rank is B or higher, play legacy applause sample on legacy skins.
                applauseSamples.Insert(0, @"applause");

            switch (rank)
            {
                default:
                case ScoreRank.D:
                    applauseSamples.Add(@"Results/applause-d");
                    break;

                case ScoreRank.C:
                    applauseSamples.Add(@"Results/applause-c");
                    break;

                case ScoreRank.B:
                    applauseSamples.Add(@"Results/applause-b");
                    break;

                case ScoreRank.A:
                    applauseSamples.Add(@"Results/applause-a");
                    break;

                case ScoreRank.S:
                case ScoreRank.SH:
                case ScoreRank.X:
                case ScoreRank.XH:
                    applauseSamples.Add(@"Results/applause-s");
                    break;
            }

            LoadComponentAsync(rankApplauseSound = new PoolableSkinnableSample(new SampleInfo(applauseSamples.ToArray())), s =>
            {
                if (!this.IsCurrentScreen() || s != rankApplauseSound)
                    return;

                AddInternal(rankApplauseSound);

                rankApplauseSound.VolumeTo(applause_volume);
                rankApplauseSound.Play();
            });
        }

        #endregion

        /// <summary>
        /// Fetches the next page of scores in the given direction.
        /// </summary>
        /// <param name="direction">The direction, or <c>null</c> to fetch any scores.</param>
        private void fetchScores(int? direction)
        {
            Debug.Assert(direction == null || direction == -1 || direction == 1);

            if (!lastFetchTask.IsCompleted)
                return;

            lastFetchTask = Task.Run(async () =>
            {
                ScoreInfo[] scores;

                switch (direction)
                {
                    default:
                        scores = await FetchScores().ConfigureAwait(false);
                        break;

                    case -1:
                    case 1:
                        scores = await FetchNextPage(direction.Value).ConfigureAwait(false);
                        break;
                }

                await addScores(scores).ConfigureAwait(false);
            });
        }

        /// <summary>
        /// Performs a fetch/refresh of scores to be displayed.
        /// </summary>
        protected virtual Task<ScoreInfo[]> FetchScores() => Task.FromResult<ScoreInfo[]>([]);

        /// <summary>
        /// Performs a fetch of the next page of scores. This is invoked every frame.
        /// </summary>
        /// <param name="direction">The fetch direction. -1 to fetch scores greater than the current start of the list, and 1 to fetch scores lower than the current end of the list.</param>
        protected virtual Task<ScoreInfo[]> FetchNextPage(int direction) => Task.FromResult<ScoreInfo[]>([]);

        private Task addScores(ScoreInfo[] scores)
        {
            var tcs = new TaskCompletionSource();

            Schedule(() =>
            {
                foreach (var s in scores)
                {
                    var panel = ScorePanelList.AddScore(s);
                    if (detachedPanel != null)
                        panel.Alpha = 0;
                }

                // allow a frame for scroll container to adjust its dimensions with the added scores before fetching again.
                Schedule(tcs.SetResult);

                if (ScorePanelList.IsEmpty)
                {
                    // This can happen if for example a beatmap that is part of a playlist hasn't been played yet.
                    VerticalScrollContent.Add(new MessagePlaceholder(LeaderboardStrings.NoRecordsYet));
                }
                updateStablePanelMapRank(scores);
                OnScoresAdded(scores);
            });

            return tcs.Task;
        }

        /// <summary>
        /// Invoked after online scores are fetched and added to the list.
        /// </summary>
        /// <param name="scores">The scores that were added.</param>
        protected virtual void OnScoresAdded(ScoreInfo[] scores)
        {
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            ApplyToBackground(b =>
            {
                b.BlurAmount.Value = BACKGROUND_BLUR;
                b.FadeColour(OsuColour.Gray(0.5f), 250);
            });

            bottomPanel.FadeTo(1, 250);

            popInSample?.Play();
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (base.OnExiting(e))
                return true;

            // This is a stop-gap safety against components holding references to gameplay after exiting the gameplay flow.
            // Right now, HitEvents are only used up to the results screen. If this changes in the future we need to remove
            // HitObject references from HitEvent.
            Score?.HitEvents.Clear();

            if (!skipExitTransition)
                this.FadeOut(100);

            rankApplauseSound?.Stop();
            return false;
        }

        public override bool OnBackButton()
        {
            if (StatisticsPanel.State.Value == Visibility.Visible)
            {
                StatisticsPanel.Hide();
                return true;
            }

            return false;
        }

        private ScorePanel? detachedPanel;

        private void updateStablePanelMapRank(ScoreInfo[] scores)
        {
            if (Score == null || stableResultsPanel == null)
                return;

            int? globalRank = Score.Position;

            if (globalRank == null || globalRank <= 0)
            {
                for (int i = 0; i < scores.Length; i++)
                {
                    if (Score.OnlineID > 0 && scores[i].OnlineID == Score.OnlineID)
                    {
                        globalRank = scores[i].Position ?? i + 1;
                        break;
                    }

                    if (scores[i].User.OnlineID == Score.User.OnlineID
                        && scores[i].TotalScore == Score.TotalScore
                        && scores[i].MaxCombo == Score.MaxCombo)
                    {
                        globalRank = scores[i].Position ?? i + 1;
                        break;
                    }
                }
            }

            stableResultsPanel.UpdateMapRanks(globalRank, StableCountryMapRank);
        }

        private void onStatisticsStateChanged(ValueChangedEvent<Visibility> state)
        {
            if (state.NewValue == Visibility.Visible)
            {
                Debug.Assert(SelectedScore.Value != null);
                // Detach the panel in its original location, and move into the desired location in the local container.
                var expandedPanel = ScorePanelList.GetPanelForScore(SelectedScore.Value);
                var screenSpacePos = expandedPanel.ScreenSpaceDrawQuad.TopLeft;

                // Detach and move into the local container.
                ScorePanelList.Detach(expandedPanel);
                detachedPanelContainer.Add(expandedPanel);

                // Move into its original location in the local container first, then to the final location.
                float origLocation = detachedPanelContainer.ToLocalSpace(screenSpacePos).X;
                expandedPanel.MoveToX(origLocation)
                             .Then()
                             .MoveToX(StatisticsPanel.SIDE_PADDING, 400, Easing.OutElasticQuarter);

                // Hide contracted panels.
                foreach (var contracted in ScorePanelList.GetScorePanels().Where(p => p.State == PanelState.Contracted))
                    contracted.FadeOut(150, Easing.OutQuint);
                ScorePanelList.HandleInput = false;

                // Dim background.
                ApplyToBackground(b => b.FadeColour(OsuColour.Gray(0.4f), 400, Easing.OutQuint));

                detachedPanel = expandedPanel;
            }
            else if (detachedPanel != null)
            {
                var screenSpacePos = detachedPanel.ScreenSpaceDrawQuad.TopLeft;

                // Remove from the local container and re-attach.
                detachedPanelContainer.Remove(detachedPanel, false);
                ScorePanelList.Attach(detachedPanel);

                // Move into its original location in the attached container first, then to the final location.
                float origLocation = detachedPanel.Parent!.ToLocalSpace(screenSpacePos).X;
                detachedPanel.MoveToX(origLocation)
                             .Then()
                             .MoveToX(0, 250, Easing.OutElasticQuarter);

                // Show contracted panels.
                foreach (var contracted in ScorePanelList.GetScorePanels().Where(p => p.State == PanelState.Contracted))
                    contracted.FadeIn(150, Easing.OutQuint);
                ScorePanelList.HandleInput = true;

                // Un-dim background.
                ApplyToBackground(b => b.FadeColour(OsuColour.Gray(0.5f), 250, Easing.OutQuint));

                detachedPanel = null;
            }
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.QuickExit:
                    if (this.IsCurrentScreen())
                    {
                        this.Exit();
                        return true;
                    }

                    break;

                case GlobalAction.Select:
                    if (SelectedScore.Value != null)
                        StatisticsPanel.ToggleVisibility();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            // Match stable behaviour of only alt-scroll adjusting volume.
            // This is the same behaviour as the song selection screen.
            if (!e.CurrentState.Keyboard.AltPressed)
                return true;

            return base.OnScroll(e);
        }

        protected partial class VerticalScrollContainer : OsuScrollContainer
        {
            protected override Container<Drawable> Content => content;

            private readonly Container content;

            protected override bool OnScroll(ScrollEvent e) => !e.ControlPressed && !e.AltPressed && !e.ShiftPressed && !e.SuperPressed;

            public VerticalScrollContainer()
            {
                Masking = false;

                base.Content.Add(content = new Container { RelativeSizeAxes = Axes.X });
            }

            protected override void Update()
            {
                base.Update();
                content.Height = Math.Max(screen_height, DrawHeight);
            }
        }
    }
}






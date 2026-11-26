// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Online;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    public partial class ReplayDownloadButton : CompositeDrawable, IKeyBindingHandler<GlobalAction>
    {
        public readonly Bindable<ScoreInfo?> Score = new Bindable<ScoreInfo?>();

        protected readonly Bindable<DownloadState> State = new Bindable<DownloadState>();

        private DownloadButton button = null!;
        private ShakeContainer shakeContainer = null!;

        private ScoreDownloadTracker? downloadTracker;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        private ReplayAvailability replayAvailability
        {
            get
            {
                if (State.Value == DownloadState.LocallyAvailable)
                    return ReplayAvailability.Local;

                // Reprise: a spectated play's replay, here to be saved (it has no score on the server to download from).
                if (SaveLocalReplay != null && Score.Value is { OnlineID: <= 0, LegacyOnlineID: <= 0 })
                    return ReplayAvailability.Unsaved;

                if (Score.Value?.HasOnlineReplay == true)
                    return ReplayAvailability.Online;

                return ReplayAvailability.NotAvailable;
            }
        }

        /// <summary>
        /// Reprise: saves the replay when it's only here (a spectated play's: no score on the server to download it from), and
        /// gives the saved score, or null. Used instead of a download for a score with no online id.
        /// </summary>
        public Func<Task<ScoreInfo?>>? SaveLocalReplay { get; init; }

        public ReplayDownloadButton(ScoreInfo? score)
        {
            Score.Value = score;
            Size = new Vector2(50, 30);
        }

        /// <summary>
        /// Does what clicking this button does (watch the replay, or download it first), for a button elsewhere.
        /// </summary>
        public void Activate() => button.TriggerClick();

        /// <summary>
        /// Exports the replay as an .osr (downloading it first if needed), as the export key binding does.
        /// </summary>
        public void Export()
        {
            if (State.Value == DownloadState.LocallyAvailable)
            {
                State.BindValueChanged(exportWhenReady, true);
            }
            else
            {
                // A download needs to be performed before we can export this replay.
                button.TriggerClick();
                if (button.Enabled.Value)
                    State.BindValueChanged(exportWhenReady, true);
            }
        }

        [BackgroundDependencyLoader]
        private void load(OsuGame? game, ScoreModelDownloader scoreDownloader)
        {
            InternalChild = shakeContainer = new ShakeContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = button = new DownloadButton
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };

            button.Action = () =>
            {
                switch (State.Value)
                {
                    case DownloadState.LocallyAvailable:
                        game?.PresentScore(Score.Value!, ScorePresentType.Gameplay);
                        break;

                    case DownloadState.NotDownloaded:
                        if (SaveLocalReplay != null && Score.Value is { OnlineID: <= 0, LegacyOnlineID: <= 0 } unsaved)
                        {
                            saveLocally(unsaved, SaveLocalReplay);
                            break;
                        }

                        scoreDownloader.Download(Score.Value!);
                        break;

                    case DownloadState.Importing:
                    case DownloadState.Downloading:
                        shakeContainer.Shake();
                        break;
                }
            };

            Score.BindValueChanged(score =>
            {
                // An export may be pending from the last score.
                // Reset this to meet user expectations (a new score which has just been switched to shouldn't export)
                State.ValueChanged -= exportWhenReady;

                track(score.NewValue);
            }, true);

            State.BindValueChanged(state =>
            {
                button.State.Value = state.NewValue;
                updateState();
            }, true);
        }

        private void track(ScoreInfo? score)
        {
            downloadTracker?.RemoveAndDisposeImmediately();
            downloadTracker = null;
            State.SetDefault();

            if (score != null)
            {
                AddInternal(downloadTracker = new ScoreDownloadTracker(score)
                {
                    State = { BindTarget = State }
                });
            }

            updateState();
        }

        // Reprise: the replay saved here, then tracked as any local one (its hash known now), so the button watches or exports it.
        private void saveLocally(ScoreInfo score, Func<Task<ScoreInfo?>> save)
        {
            State.Value = DownloadState.Importing;

            save().ContinueWith(t => Schedule(() =>
            {
                if (t.IsCompletedSuccessfully && t.GetResultSafely() is ScoreInfo saved && Score.Value == score)
                {
                    score.ID = saved.ID;
                    score.Hash = saved.Hash;
                    track(score);
                }
                else
                {
                    State.Value = DownloadState.NotDownloaded;
                }
            }));
        }

        #region Export via hotkey logic (also in SaveFailedScoreButton)

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.SaveReplay:
                    button.TriggerClick();
                    return true;

                case GlobalAction.ExportReplay:
                    Export();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        private void exportWhenReady(ValueChangedEvent<DownloadState> state)
        {
            if (state.NewValue != DownloadState.LocallyAvailable) return;

            scoreManager.Export(Score.Value!);

            State.ValueChanged -= exportWhenReady;
        }

        #endregion

        private void updateState()
        {
            switch (replayAvailability)
            {
                case ReplayAvailability.Local:
                    button.TooltipText = @"watch replay";
                    button.Enabled.Value = true;
                    break;

                case ReplayAvailability.Online:
                    button.TooltipText = @"download replay";
                    button.Enabled.Value = true;
                    break;

                case ReplayAvailability.Unsaved:
                    button.TooltipText = @"save replay";
                    button.Enabled.Value = true;
                    break;

                default:
                    button.TooltipText = @"replay unavailable";
                    button.Enabled.Value = false;
                    break;
            }
        }

        private enum ReplayAvailability
        {
            Local,
            Online,
            Unsaved,
            NotAvailable,
        }
    }
}

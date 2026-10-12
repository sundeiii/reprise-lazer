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
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.StateChanges;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Overlays.Volume;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning;
using osu.Game.Skinning.Select;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    public abstract partial class SongSelect : ScreenWithBeatmapBackground, IKeyBindingHandler<GlobalAction>, ISongSelect, IHandlePresentBeatmap, IProvideCursor, IHasStableLayout
    {
        /// <summary>
        /// A debounce that governs how long after a panel is selected before the rest of song select (and the game at large)
        /// updates to show that selection.
        ///
        /// This is intentionally slightly higher than key repeat, but low enough to not impede user experience.
        /// </summary>
        public const int SELECTION_DEBOUNCE = 150;

        /// <summary>
        /// A general "global" debounce to be applied to anything aggressive difficulty calculation at song select,
        /// either after selection or after a panel comes on screen. Value should be low enough that users don't complain,
        /// but otherwise as high as possible to reduce overheads.
        /// </summary>
        public const int DIFFICULTY_CALCULATION_DEBOUNCE = 150;

        private const float logo_scale = 0.4f;
        private const double fade_duration = 300;

        public const float WEDGE_CONTENT_MARGIN = CORNER_RADIUS_HIDE_OFFSET + OsuGame.SCREEN_EDGE_MARGIN;
        public const float CORNER_RADIUS_HIDE_OFFSET = 20f;
        public const float ENTER_DURATION = 600;

        /// <summary>
        /// Whether this song select instance should take control of the global track,
        /// applying looping and preview offsets.
        /// </summary>
        protected bool ControlGlobalMusic { get; init; } = true;

        /// <summary>
        /// Whether this song select instance should allow scoping down to a specific beatmap set,
        /// exposing other difficulties that are otherwise hidden by filter criteria.
        /// </summary>
        protected bool SupportScoping { init => scopedBeatmapSet.Disabled = !value; }

        /// <summary>
        /// Whether the osu! logo should be shown at the bottom-right of the screen.
        /// </summary>
        protected bool ShowOsuLogo { get; init; } = true;

        /// <summary>
        /// Additional padding to be added to the title wedge.
        /// Generally set to show external content in this space.
        /// </summary>
        public float TopPadding { get; init; }

        private ModSelectOverlay modSelectOverlay = null!;
        private ModSpeedHotkeyHandler modSpeedHotkeyHandler = null!;

        // Blue is the most neutral choice, so I'm using that for now.
        // Purple makes the most sense to match the "gameplay" flow, but it's a bit too strong for the current design.
        // TODO: Colour scheme choice should probably be customisable by the user.
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        private BeatmapCarousel carousel = null!;

        protected FilterControl FilterControl { get; private set; } = null!;

        private BeatmapTitleWedge titleWedge = null!;
        private BeatmapDetailsArea detailsArea = null!;
        private FillFlowContainer wedgesContainer = null!;
        private Box rightGradientBackground = null!;
        private Container mainContent = null!;
        private SkinnableContainer skinnableContent = null!;

        private GridContainer mainGridContainer = null!;

        private NoResultsPlaceholder noResultsPlaceholder = null!;

        public override bool? ApplyModTrackAdjustments => true;

        // osu!stable's footer replaces it with a legacy skin (OsuSetting.StableLayouts).
        public override bool ShowFooter => !stableFooterShown;

        // osu!stable has no toolbar: it's hidden while osu!stable's layout is shown.
        public override bool HideOverlaysOnEnter => stableFooterShown;

        [Resolved(canBeNull: true)]
        private OsuGame? osuGame { get; set; }

        private Sample? errorSample;

        [Resolved]
        private OsuGameBase? game { get; set; }

        [Resolved]
        private OsuLogo? logo { get; set; }

        [Resolved]
        private BeatmapSetOverlay? beatmapOverlay { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private ManageCollectionsDialog? collectionsDialog { get; set; }

        [Resolved]
        private DifficultyRecommender? difficultyRecommender { get; set; }

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved]
        private IOverlayManager? overlayManager { get; set; }

        private InputManager inputManager = null!;

        private readonly RealmPopulatingOnlineLookupSource onlineLookupSource = new RealmPopulatingOnlineLookupSource();

        private Bindable<bool> configBackgroundBlur = null!;
        private Bindable<bool> showConvertedBeatmaps = null!;

        private IDisposable? modSelectOverlayRegistration;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private Bindable<bool> stableLayouts = null!;
        private Bindable<StableLayoutVersion> stableLayoutVersion = null!;
        private IBindable<Skin> currentSkin = null!;
        private bool stableFooterShown;

        public bool StableLayoutShown => stableFooterShown;
        private Container stableFooterContainer = null!;
        private StableTopContainer stableTopContainer = null!;
        private Container stableLayoutContainer = null!;
        private Container stableDropdownsContainer = null!;
        private Container stableModeMenuContainer = null!;
        private LegacyModeMenu? stableModeMenu;
        private Container stableLeaderboardContainer = null!;
        private LegacySongSelectLeaderboard? stableLeaderboard;
        private LegacyFooter? stableFooter;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, OsuConfigManager config)
        {
            errorSample = audio.Samples.Get(@"UI/generic-error");

            AddRangeInternal(new Drawable[]
            {
                new GlobalScrollAdjustsVolume(),
                onlineLookupSource,
                mainContent = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Bottom = ScreenFooter.HEIGHT },
                    Child = new OsuContextMenuContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new PopoverContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Width = 0.6f,
                                    Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.3f), Color4.Black.Opacity(0f)),
                                },
                                mainGridContainer = new GridContainer // used for max width implementation
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Content = new[]
                                    {
                                        new[]
                                        {
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                // Ensure the left components are on top of the carousel both visually (although they should never overlay)
                                                // but more importantly, for input purposes to allow the scroll-to-selection logic to override carousel's
                                                // screen-wide scroll handling.
                                                Depth = float.MinValue,
                                                Shear = OsuGame.SHEAR,
                                                Padding = new MarginPadding
                                                {
                                                    Top = -CORNER_RADIUS_HIDE_OFFSET,
                                                    Left = -CORNER_RADIUS_HIDE_OFFSET,
                                                },
                                                Children = new Drawable[]
                                                {
                                                    new Container
                                                    {
                                                        // Pad enough to only reset scroll when well into the left wedge areas.
                                                        Padding = new MarginPadding { Right = 40 },
                                                        RelativeSizeAxes = Axes.Both,
                                                        Child = new LeftSideInteractionContainer(() => carousel.ScrollToSelection())
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                        },
                                                    },
                                                    wedgesContainer = new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                        Spacing = new Vector2(0f, 4f),
                                                        Direction = FillDirection.Vertical,
                                                        Children = new Drawable[]
                                                        {
                                                            new ShearAligningWrapper(titleWedge = new BeatmapTitleWedge
                                                            {
                                                                TopPadding = TopPadding,
                                                            }),
                                                            new ShearAligningWrapper(detailsArea = new BeatmapDetailsArea()),
                                                        },
                                                    },
                                                }
                                            },
                                            Empty(),
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Children = new Drawable[]
                                                {
                                                    rightGradientBackground = new Box
                                                    {
                                                        Anchor = Anchor.TopRight,
                                                        Origin = Anchor.TopRight,
                                                        Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.0f), Color4.Black.Opacity(0.5f)),
                                                        RelativeSizeAxes = Axes.Both,
                                                    },
                                                    new Container
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                        Padding = new MarginPadding
                                                        {
                                                            Top = FilterControl.HEIGHT_FROM_SCREEN_TOP + 5,
                                                            Bottom = 5,
                                                        },
                                                        Children = new Drawable[]
                                                        {
                                                            carousel = new BeatmapCarousel
                                                            {
                                                                BleedTop = FilterControl.HEIGHT_FROM_SCREEN_TOP + 5,
                                                                BleedBottom = ScreenFooter.HEIGHT + 5,
                                                                RelativeSizeAxes = Axes.Both,
                                                                RequestPresentBeatmap = b => SelectAndRun(b, OnStart),
                                                                RequestSelection = queueBeatmapSelection,
                                                                RequestRecommendedSelection = requestRecommendedSelection,
                                                                NewItemsPresented = newItemsPresented,
                                                            },
                                                            noResultsPlaceholder = new NoResultsPlaceholder
                                                            {
                                                                RequestClearFilterText = () => FilterControl.Search(string.Empty)
                                                            }
                                                        }
                                                    },
                                                    FilterControl = new FilterControl
                                                    {
                                                        Anchor = Anchor.TopRight,
                                                        Origin = Anchor.TopRight,
                                                        RelativeSizeAxes = Axes.X,
                                                        ScopedBeatmapSet = { BindTarget = ScopedBeatmapSet },
                                                    },
                                                }
                                            },
                                        },
                                    }
                                },
                            }
                        },
                    }
                },
                skinnableContent = new SkinnableContainer(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.SongSelect))
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                },
                modSpeedHotkeyHandler = new ModSpeedHotkeyHandler(),
                // osu!stable's layout is 768 high (wider on wide screens), scaled to the screen like stable's.
                stableLayoutContainer = new DrawSizePreservingFillContainer
                {
                    TargetDrawSize = new Vector2(1024, 768),
                    Strategy = DrawSizePreservationStrategy.Minimum,
                    Children = new Drawable[]
                    {
                        stableLeaderboardContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                        stableTopContainer = new StableTopContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = LegacySongSelectTop.HEIGHT,
                        },
                        stableFooterContainer = new PopoverContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                        // Stable's group and sort choices are above the footer (some skins draw the top with the footer's images).
                        stableDropdownsContainer = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = LegacySongSelectTop.HEIGHT,
                        },
                        stableModeMenuContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                    }
                },
            });

            stableLayouts = config.GetBindable<bool>(OsuSetting.StableLayouts);
            stableLayoutVersion = config.GetBindable<StableLayoutVersion>(OsuSetting.StableLayoutVersion);
            stableFooterShown = wantsStableFooter;
            if (stableFooterShown)
                stableFooterContainer.Child = stableFooter = createStableFooter();
            applyStableTop();

            LoadComponent(modSelectOverlay = CreateModSelectOverlay());

            configBackgroundBlur = config.GetBindable<bool>(OsuSetting.SongSelectBackgroundBlur);
            configBackgroundBlur.BindValueChanged(e =>
            {
                if (!this.IsCurrentScreen())
                    return;

                updateBackgroundDim();
            });

            showConvertedBeatmaps = config.GetBindable<bool>(OsuSetting.ShowConvertedBeatmaps);
        }

        // Colour scheme for mod overlay is left as default (green) to match mods button.
        // Not sure about this, but we'll iterate based on feedback.
        protected virtual ModSelectOverlay CreateModSelectOverlay() => new UserModSelectOverlay
        {
            ShowPresets = true,
        };

        private void requestRecommendedSelection(IEnumerable<GroupedBeatmap> groupedBeatmaps)
        {
            var recommendedBeatmap = difficultyRecommender?.GetRecommendedBeatmap(groupedBeatmaps.Select(gb => gb.Beatmap)) ?? groupedBeatmaps.First().Beatmap;
            queueBeatmapSelection(groupedBeatmaps.First(bug => bug.Beatmap.Equals(recommendedBeatmap)));
        }

        /// <summary>
        /// Called when a selection is made to progress away from the song select screen.
        ///
        /// This is the default action which should be provided to <see cref="SelectAndRun"/>.
        /// </summary>
        protected abstract void OnStart();

        public override IReadOnlyList<ScreenFooterButton> CreateFooterButtons() => new ScreenFooterButton[]
        {
            new FooterButtonMods(modSelectOverlay)
            {
                Hotkey = GlobalAction.ToggleModSelection,
                Mods = Mods,
                Ruleset = Ruleset,
                RequestDeselectAllMods = () =>
                {
                    if (modSelectOverlay.State.Value == Visibility.Visible)
                        modSelectOverlay.DeselectAll();
                    else
                        Mods.Value = Array.Empty<Mod>();
                }
            },
            new FooterButtonRandom
            {
                NextRandom = () =>
                {
                    if (!carousel.NextRandom())
                        errorSample?.Play();
                },
                PreviousRandom = () =>
                {
                    if (!carousel.PreviousRandom())
                        errorSample?.Play();
                }
            },
            new FooterButtonOptions
            {
                Hotkey = GlobalAction.ToggleBeatmapOptions,
            }
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            modSelectOverlayRegistration = overlayManager?.RegisterBlockingOverlay(modSelectOverlay);

            stableLayouts.BindValueChanged(_ => updateStableFooter());
            // Another version: its look again (as with another skin).
            stableLayoutVersion.BindValueChanged(_ => updateStableFooter());
            // A copy: unbound when this screen goes (the skin manager's stays).
            currentSkin = skinManager.CurrentSkin.GetBoundCopy();
            currentSkin.BindValueChanged(_ => updateStableFooter());

            inputManager = GetContainingInputManager()!;

            FilterControl.CriteriaChanged += criteriaChanged;

            modSelectOverlay.State.BindValueChanged(v =>
            {
                if (!this.IsCurrentScreen())
                    return;

                if (ShowOsuLogo)
                    logo?.FadeTo(v.NewValue == Visibility.Visible ? 0f : 1f, 200, Easing.OutQuint);
            });
        }

        protected override void Update()
        {
            base.Update();

            detailsArea.Height = wedgesContainer.ChildSize.Y - titleWedge.LayoutSize.Y - 4;

            // osu!stable's list is laid out in its 768-high units, like the rest of its layout.
            carousel.StableLayoutScale = DrawHeight / 768f;

            float widescreenBonusWidth = Math.Max(0, DrawWidth / DrawHeight - 2f);

            mainGridContainer.ColumnDimensions = new[]
            {
                new Dimension(GridSizeMode.Relative, 0.5f, maxSize: 700 + widescreenBonusWidth * 100),
                new Dimension(),
                new Dimension(GridSizeMode.Relative, 0.5f, minSize: 500, maxSize: 700 + widescreenBonusWidth * 300),
            };

            if (this.IsCurrentScreen())
                updateDebounce();
        }

        #region Selection debounce

        private BeatmapInfo? debounceQueuedSelection;
        private double debounceElapsedTime;

        private void debounceQueueSelection(BeatmapInfo beatmap)
        {
            debounceQueuedSelection = beatmap;
            debounceElapsedTime = 0;
        }

        private void updateDebounce()
        {
            if (debounceQueuedSelection == null) return;

            double elapsed = Clock.ElapsedFrameTime;

            // When a key is being held, assume the user is traversing the carousel using key repeat.
            // We want to change panels less often in this state (basically making debounce longer than initial key repeat, at least).
            double debounceInterval = inputManager.CurrentState.Keyboard.Keys.HasAnyButtonPressed ? SELECTION_DEBOUNCE * 2 : SELECTION_DEBOUNCE;

            // avoid debounce running early if there's a single long frame.
            if (!DebugUtils.IsNUnitRunning && Clock.FramesPerSecond > 0)
                elapsed = Math.Min(1000 / Clock.FramesPerSecond, elapsed);

            debounceElapsedTime += elapsed;

            if (debounceElapsedTime >= debounceInterval)
                performDebounceSelection();
        }

        private void performDebounceSelection()
        {
            if (debounceQueuedSelection == null) return;

            try
            {
                if (Beatmap.Value.BeatmapInfo.Equals(debounceQueuedSelection))
                    return;

                var workingBeatmap = beatmaps.GetWorkingBeatmap(debounceQueuedSelection);
                if (!checkBeatmapValidForSelection(workingBeatmap.BeatmapInfo))
                    return;

                Beatmap.Value = workingBeatmap;
            }
            finally
            {
                cancelDebounceSelection();
            }
        }

        private void cancelDebounceSelection()
        {
            debounceQueuedSelection = null;
            debounceElapsedTime = 0;
        }

        #endregion

        #region Audio

        [Resolved]
        private MusicController music { get; set; } = null!;

        private readonly WeakReference<ITrack?> lastTrack = new WeakReference<ITrack?>(null);

        /// <summary>
        /// Ensures some music is playing for the current track.
        /// Will resume playback from a manual user pause if the track has changed.
        /// </summary>
        private void ensurePlayingSelected()
        {
            if (!ControlGlobalMusic)
                return;

            ITrack track = music.CurrentTrack;

            bool isNewTrack = !lastTrack.TryGetTarget(out var last) || last != track;

            if (!track.IsRunning && (music.UserPauseRequested != true || isNewTrack))
            {
                Logger.Log($"Song select decided to {nameof(ensurePlayingSelected)}");

                // Only restart playback if a new track.
                // This is important so that when exiting gameplay, the track is not restarted back to the preview point.
                music.Play(isNewTrack);
            }

            lastTrack.SetTarget(track);
        }

        private bool isHandlingLooping;

        private void beginLooping()
        {
            Debug.Assert(!isHandlingLooping);

            isHandlingLooping = true;

            ensureTrackLooping(Beatmap.Value, TrackChangeDirection.None);

            music.TrackChanged += ensureTrackLooping;
        }

        private void endLooping()
        {
            // may be called multiple times during screen exit process.
            if (!isHandlingLooping)
                return;

            music.CurrentTrack.Looping = isHandlingLooping = false;

            music.TrackChanged -= ensureTrackLooping;
        }

        private void ensureTrackLooping(IWorkingBeatmap beatmap, TrackChangeDirection changeDirection)
            => beatmap.PrepareTrackForPreview(true);

        #endregion

        #region Selection handling

        /// <summary>
        /// Finalises selection on the given <see cref="BeatmapInfo"/> and runs the provided action if possible.
        /// </summary>
        /// <param name="beatmap">The beatmap which should be selected. If not provided, the current globally selected beatmap will be used.</param>
        /// <param name="startAction">The action to perform if conditions are met to be able to proceed. May not be invoked if in an invalid state.</param>
        protected void SelectAndRun(BeatmapInfo beatmap, Action startAction)
        {
            if (!this.IsCurrentScreen())
                return;

            if (!checkBeatmapValidForSelection(beatmap))
                return;

            // To ensure sanity, cancel any pending selection as we are about to force a selection.
            // Carousel selection will update to the forced selection via a call of `ensureGlobalBeatmapValid` below, or when song select becomes current again.
            cancelDebounceSelection();

            // Forced refetch is important here to guarantee correct invalidation across all difficulties (editor specific).
            Beatmap.Value = beatmaps.GetWorkingBeatmap(beatmap, true);

            if (Beatmap.IsDefault)
                return;

            startAction();
        }

        /// <summary>
        /// Prepares the proposed beatmap for global selection based on a carousel user-performed action.
        /// </summary>
        /// <remarks>
        /// Calling this method will:
        /// - Immediately update the selection the carousel.
        /// - After <see cref="SELECTION_DEBOUNCE"/>, update the global beatmap. This in turn causes song select visuals (title, details, leaderboard) to update.
        ///   This debounce is intended to avoid high overheads from churning lookups while a user is changing selection via rapid keyboard operations.
        /// </remarks>
        /// <param name="groupedBeatmap">The beatmap to be selected.</param>
        private void queueBeatmapSelection(GroupedBeatmap groupedBeatmap)
        {
            if (!this.IsCurrentScreen())
                return;

            carousel.CurrentGroupedBeatmap = groupedBeatmap;

            // Debounce consideration is to avoid beatmap churn on key repeat selection.
            debounceQueueSelection(groupedBeatmap.Beatmap);
        }

        private bool ensureGlobalBeatmapValid()
        {
            if (!this.IsCurrentScreen())
                return false;

            performDebounceSelection();

            // While filtering, let's not ever attempt to change selection.
            // This will be resolved after the filter completes, see `newItemsPresented`.
            if (IsFiltering)
                return false;

            // Refetch to be confident that the current selection is still valid. It may have been deleted or hidden.
            var currentBeatmap = beatmaps.GetWorkingBeatmap(Beatmap.Value.BeatmapInfo, true);
            bool validSelection = checkBeatmapValidForSelection(currentBeatmap.BeatmapInfo);

            if (validSelection)
            {
                carousel.CurrentBeatmap = currentBeatmap.BeatmapInfo;
                return true;
            }

            // If there was no beatmap selected, pick a random one.
            if (Beatmap.IsDefault)
            {
                validSelection = carousel.NextRandom();
                performDebounceSelection();
                return validSelection;
            }

            // If a previous non-default selection became non-valid, it was likely hidden or deleted.
            if (!validSelection)
            {
                // In the case a difficulty was hidden or removed, prefer selecting another difficulty from the same set.
                var activeSet = currentBeatmap.BeatmapSetInfo;

                var validBeatmaps = activeSet.Beatmaps.Where(checkBeatmapValidForSelection).ToArray();

                if (validBeatmaps.Any())
                {
                    var beatmap = difficultyRecommender?.GetRecommendedBeatmap(validBeatmaps) ?? validBeatmaps.First();
                    carousel.CurrentBeatmap = beatmap;
                    debounceQueueSelection(beatmap);
                    return true;
                }
            }

            // If all else fails, use the default beatmap.
            Beatmap.SetDefault();
            performDebounceSelection();

            return validSelection;
        }

        private bool checkBeatmapValidForSelection(BeatmapInfo beatmap)
        {
            if (!beatmap.AllowGameplayWithRuleset(Ruleset.Value, showConvertedBeatmaps.Value))
                return false;

            if (beatmap.Hidden)
                return false;

            if (beatmap.BeatmapSet == null)
                return false;

            if (beatmap.BeatmapSet.Protected || beatmap.BeatmapSet.DeletePending)
                return false;

            return true;
        }

        #endregion

        #region Transitions

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            this.FadeIn();
            onArrivingAtScreen();
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);

            this.FadeIn(fade_duration, Easing.OutQuint);
            onArrivingAtScreen();

            ensureGlobalBeatmapValid();

            detailsArea.Refresh();
            stableLeaderboard?.Refetch();

            if (ControlGlobalMusic)
            {
                // restart playback on returning to song select, regardless.
                // not sure this should be a permanent thing (we may want to leave a user pause paused even on returning)
                music.ResetTrackAdjustments();
                music.Play(requestedByUser: true);
            }
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            carousel.VisuallyFocusSelected = true;

            this.FadeOut(fade_duration, Easing.OutQuint);
            onLeavingScreen();

            base.OnSuspending(e);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            this.FadeOut(fade_duration, Easing.OutQuint);
            onLeavingScreen();

            return base.OnExiting(e);
        }

        private void onArrivingAtScreen()
        {
            // The game shows the toolbar when this screen's pushed, before it's loaded and knows it shows osu!stable's layout.
            if (stableFooterShown)
                osuGame?.Toolbar.Hide();

            modSelectOverlay.Beatmap.BindTo(Beatmap);
            modSelectOverlay.Ruleset.BindTo(Ruleset);
            // required due to https://github.com/ppy/osu-framework/issues/3218
            modSelectOverlay.SelectedMods.Disabled = false;
            modSelectOverlay.SelectedMods.BindTo(Mods);

            carousel.VisuallyFocusSelected = false;

            if (ControlGlobalMusic)
            {
                // Avoid abruptly starting playback at preview point.
                // Importantly, this should be done before looping is setup to ensure we get the correct imminent `IsPlaying` state.
                if (!music.IsPlaying)
                {
                    music.DuckMomentarily(0, new DuckParameters
                    {
                        DuckDuration = 0,
                        DuckVolumeTo = 0,
                        RestoreDuration = 800,
                        RestoreEasing = Easing.OutQuint
                    });
                }

                beginLooping();
            }

            Beatmap.BindValueChanged(updateVariousState, true);
        }

        private void updateVariousState(ValueChangedEvent<WorkingBeatmap> e)
        {
            if (!this.IsCurrentScreen())
                return;

            ensureGlobalBeatmapValid();

            ensurePlayingSelected();
            updateBackgroundDim();
            updateWedgeVisibility();
            fetchOnlineInfo(force: ReferenceEquals(e.OldValue, e.NewValue));
        }

        private void onLeavingScreen()
        {
            restoreBackground();

            Beatmap.ValueChanged -= updateVariousState;

            modSelectOverlay.SelectedMods.UnbindFrom(Mods);
            modSelectOverlay.Ruleset.UnbindFrom(Ruleset);
            modSelectOverlay.Beatmap.UnbindFrom(Beatmap);

            updateWedgeVisibility();

            endLooping();
        }

        protected override void LogoArriving(OsuLogo logo, bool resuming)
        {
            base.LogoArriving(logo, resuming);

            if (!ShowOsuLogo)
                return;

            if (logo.Alpha > 0.8f && resuming)
                startTrackingLogo(logo, 400, Easing.OutQuint);
            else
            {
                logo.Hide();
                logo.ScaleTo(0.2f);
                startTrackingLogo(logo);
            }

            logo.FadeIn(240, Easing.OutQuint);
            logo.ScaleTo(logo_scale, 240, Easing.OutQuint);

            logo.Action = () =>
            {
                ensureGlobalBeatmapValid();
                SelectAndRun(Beatmap.Value.BeatmapInfo, OnStart);
                return false;
            };
        }

        protected override void LogoSuspending(OsuLogo logo)
        {
            base.LogoSuspending(logo);

            if (!ShowOsuLogo)
                return;

            stopTrackingLogo();
        }

        protected override void LogoExiting(OsuLogo logo)
        {
            base.LogoExiting(logo);

            if (!ShowOsuLogo)
                return;

            stopTrackingLogo();

            logo.ScaleTo(0.2f, 120, Easing.Out);
            logo.FadeOut(120, Easing.Out);
        }

        private void updateWedgeVisibility()
        {
            // Ensure we don't show an invalid selection before the carousel has finished initially filtering.
            // This avoids a flicker of a placeholder or invalid beatmap before a proper selection.
            //
            // After the carousel finishes filtering, it will attempt a selection then call this method again.
            if (!CarouselItemsPresented && !checkBeatmapValidForSelection(Beatmap.Value.BeatmapInfo))
                return;

            if (carousel.VisuallyFocusSelected)
            {
                titleWedge.Hide();
                detailsArea.Hide();
                FilterControl.Hide();
            }
            else
            {
                titleWedge.Show();
                detailsArea.Show();
                FilterControl.Show();
            }
        }

        private void updateBackgroundDim() => ApplyToBackground(backgroundModeBeatmap =>
        {
            backgroundModeBeatmap.Beatmap = Beatmap.Value;
            backgroundModeBeatmap.IgnoreUserSettings.Value = true;

            backgroundModeBeatmap.DimWhenUserSettingsIgnored.Value = 0.1f;

            // Required to undo results screen dimming the background.
            // Probably needs more thought because this needs to be in every `ApplyToBackground` currently to restore sane defaults.
            backgroundModeBeatmap.FadeColour(Color4.White, 250);

            bool backgroundRevealActive = revealBackgroundDelegate?.State == ScheduledDelegate.RunState.Running || revealBackgroundDelegate?.State == ScheduledDelegate.RunState.Complete;
            backgroundModeBeatmap.BlurAmount.Value = configBackgroundBlur.Value && !backgroundRevealActive ? 20 : 0f;
        });

        #endregion

        #region Filtering

        /// <summary>
        /// Whether the carousel has finished initial presentation of beatmap panels.
        /// </summary>
        public bool CarouselItemsPresented { get; private set; }

        /// <summary>
        /// Whether the carousel is or will be undergoing a filter operation.
        /// </summary>
        public bool IsFiltering => carousel.IsFiltering || filterDebounce?.State == ScheduledDelegate.RunState.Waiting;

        private const double filter_delay = 250;

        private ScheduledDelegate? filterDebounce;

        private void criteriaChanged(FilterCriteria criteria)
        {
            filterDebounce?.Cancel();

            // The first filter needs to be applied immediately as this triggers the initial carousel load.
            bool isFirstFilter = filterDebounce == null;

            // Criteria change may have included a ruleset change which made the current selection invalid.
            bool isSelectionValid = checkBeatmapValidForSelection(Beatmap.Value.BeatmapInfo);

            // (Logged to find where a search stops reaching the list in osu!stable's layout.)
            Logger.Log($"Song select criteria changed (search \"{criteria.SearchText}\", present {IsPresent}, current {this.IsCurrentScreen()})");

            filterDebounce = Scheduler.AddDelayed(() =>
            {
                Logger.Log("Song select filtering the list");
                carousel.Filter(criteria, !isSelectionValid);
            }, isFirstFilter || !isSelectionValid ? 0 : filter_delay);
        }

        private void newItemsPresented(IEnumerable<CarouselItem> carouselItems)
        {
            if (carousel.Criteria == null)
                return;

            CarouselItemsPresented = true;

            updateNoResultsPlaceholder();

            FilterControl.StatusText = SongSelectStrings.MatchesCount(carousel.MatchedBeatmapsCount);

            // If there's already a selection update in progress, let's not interrupt it.
            // Interrupting could cause the debounce interval to be reduced.
            //
            // `ensureGlobalBeatmapValid` is run post-selection which will resolve any pending incompatibilities (see `Beatmap` bindable callback).
            if (debounceQueuedSelection == null)
                ensureGlobalBeatmapValid();

            updateWedgeVisibility();
        }

        private void updateNoResultsPlaceholder()
        {
            int count = carousel.MatchedBeatmapsCount;

            if (count == 0)
            {
                if (noResultsPlaceholder.State.Value == Visibility.Hidden)
                {
                    // Duck audio temporarily when the no results placeholder becomes visible.
                    //
                    // Temporary ducking makes it easier to avoid scenarios where the ducking interacts badly
                    // with other global UI components (like overlays).
                    music.DuckMomentarily(400, new DuckParameters
                    {
                        DuckVolumeTo = 1,
                        DuckCutoffTo = 500,
                        DuckDuration = 250,
                        RestoreDuration = 2000,
                    });
                }

                noResultsPlaceholder.Show();
                noResultsPlaceholder.Filter = carousel.Criteria!;

                rightGradientBackground.ResizeWidthTo(3, 1000, Easing.OutPow10);
            }
            else
            {
                noResultsPlaceholder.Hide();

                rightGradientBackground.ResizeWidthTo(1, 400, Easing.OutPow10);
            }
        }

        #endregion

        #region Background reveal

        private ScheduledDelegate? revealBackgroundDelegate;

        public CursorContainer? Cursor => null;
        bool IProvideCursor.ProvidingUserCursor => revealBackgroundDelegate?.Completed == true;

        protected override bool OnHover(HoverEvent e) => true;

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            var containingInputManager = GetContainingInputManager();

            // I don't know why this works, but it does.
            // If the carousel panels are hovered, hovered no longer contains the screen.
            // Maybe there's a better way of doing this, but I couldn't immediately find a good setup.
            bool mouseDownPriority = containingInputManager!.HoveredDrawables.Contains(this);

            // Touch input synthesises right clicks, which allow absolute scroll of the carousel.
            // For simplicity, disable this functionality on mobile.
            bool isTouchInput = e.CurrentState.Mouse.LastSource is ISourcedFromTouch;

            if (!carousel.AbsoluteScrolling && !isTouchInput && mouseDownPriority && revealBackgroundDelegate == null)
            {
                revealBackgroundDelegate = Scheduler.AddDelayed(() =>
                {
                    if (containingInputManager.DraggedDrawable != null)
                    {
                        revealBackgroundDelegate = null;
                        return;
                    }

                    mainContent.ResizeWidthTo(1.2f, 600, Easing.OutQuint);
                    mainContent.ScaleTo(1.2f, 600, Easing.OutQuint);
                    mainContent.FadeOut(200, Easing.OutQuint);

                    skinnableContent.ResizeWidthTo(1.2f, 600, Easing.OutQuint);
                    skinnableContent.ScaleTo(1.2f, 600, Easing.OutQuint);
                    skinnableContent.FadeOut(200, Easing.OutQuint);

                    updateBackgroundDim();

                    Footer?.Hide();
                    stableFooterContainer.FadeOut(200, Easing.OutQuint);
                }, 200);
            }

            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            restoreBackground();
            base.OnMouseUp(e);
        }

        private void restoreBackground()
        {
            if (revealBackgroundDelegate == null)
                return;

            if (revealBackgroundDelegate.State == ScheduledDelegate.RunState.Complete)
            {
                mainContent.ResizeWidthTo(1f, 500, Easing.OutQuint);
                mainContent.ScaleTo(1, 500, Easing.OutQuint);
                mainContent.FadeIn(500, Easing.OutQuint);

                skinnableContent.ResizeWidthTo(1f, 500, Easing.OutQuint);
                skinnableContent.ScaleTo(1, 500, Easing.OutQuint);
                skinnableContent.FadeIn(500, Easing.OutQuint);

                if (!stableFooterShown)
                    Footer?.Show();
                stableFooterContainer.FadeIn(500, Easing.OutQuint);
            }

            revealBackgroundDelegate.Cancel();
            revealBackgroundDelegate = null;

            updateBackgroundDim();
        }

        #endregion

        #region Input

        public virtual bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (!this.IsCurrentScreen()) return false;

            if (game == null)
                return false;

            var flattenedMods = ModUtils.FlattenMods(game.AvailableMods.Value.SelectMany(kv => kv.Value));

            // Stable's beatmap options, random and mod selection (the game's own buttons aren't there).
            if (e.Action == GlobalAction.ToggleBeatmapOptions && stableFooterShown && !e.Repeat)
            {
                // Pressed again: closed, as F1 does the mods.
                if (stableOptions != null)
                    stableOptions.Close();
                else
                    showStableOptions();

                return true;
            }

            // Stable's random (F2, back with Shift+F2).
            if ((e.Action == GlobalAction.SelectNextRandom || e.Action == GlobalAction.SelectPreviousRandom) && stableFooterShown && !e.Repeat)
            {
                if (!(e.Action == GlobalAction.SelectNextRandom ? carousel.NextRandom() : carousel.PreviousRandom()))
                    errorSample?.Play();

                return true;
            }

            if (e.Action == GlobalAction.ToggleModSelection && stableFooterShown && !e.Repeat)
            {
                if (stableMods != null)
                    stableMods.Close();
                else
                    showStableMods();

                return true;
            }

            switch (e.Action)
            {
                case GlobalAction.Select:
                    // in most circumstances this is handled already by the carousel itself, but there are cases where it will not be.
                    // one of which is filtering out all visible beatmaps and attempting to start gameplay.
                    // in that case, users still expect a `Select` press to advance to gameplay anyway, using the ambient selected beatmap if there is one,
                    // which matches the behaviour resulting from clicking the osu! cookie in that scenario.
                    ensureGlobalBeatmapValid();
                    SelectAndRun(Beatmap.Value.BeatmapInfo, OnStart);
                    return true;

                case GlobalAction.IncreaseModSpeed:
                    return modSpeedHotkeyHandler.ChangeSpeed(0.05, flattenedMods);

                case GlobalAction.DecreaseModSpeed:
                    return modSpeedHotkeyHandler.ChangeSpeed(-0.05, flattenedMods);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat) return false;

            switch (e.Key)
            {
                case Key.Delete:
                    if (e.ShiftPressed)
                    {
                        if (!Beatmap.IsDefault)
                            Delete(Beatmap.Value.BeatmapSetInfo);
                        return true;
                    }

                    break;
            }

            return base.OnKeyDown(e);
        }

        #endregion

        #region Online lookups

        public enum BeatmapSetLookupStatus
        {
            InProgress,
            Completed,
        }

        public class BeatmapSetLookupResult
        {
            public BeatmapSetLookupStatus Status { get; }
            public APIBeatmapSet? Result { get; }

            private BeatmapSetLookupResult(BeatmapSetLookupStatus status, APIBeatmapSet? result)
            {
                Status = status;
                Result = result;
            }

            public static BeatmapSetLookupResult InProgress() => new BeatmapSetLookupResult(BeatmapSetLookupStatus.InProgress, null);
            public static BeatmapSetLookupResult Completed(APIBeatmapSet? beatmapSet) => new BeatmapSetLookupResult(BeatmapSetLookupStatus.Completed, beatmapSet);
        }

        /// <summary>
        /// Result of the latest online beatmap set lookup.
        /// Note that this being <see langword="null"/> or <see cref="BeatmapSetLookupResult.InProgress"/> is different from
        /// being a <see cref="BeatmapSetLookupResult.Completed"/> with a <see cref="BeatmapSetLookupResult.Result"/> of null.
        /// The former indicates a lookup never occurring or being in progress, while the latter indicates a completed lookup with no result.
        /// </summary>
        [Cached(typeof(IBindable<BeatmapSetLookupResult?>))]
        private readonly Bindable<BeatmapSetLookupResult?> lastLookupResult = new Bindable<BeatmapSetLookupResult?>();

        private CancellationTokenSource? onlineLookupCancellation;
        private Task<APIBeatmapSet?>? currentOnlineLookup;

        private void fetchOnlineInfo(bool force = false)
        {
            var beatmapSetInfo = Beatmap.Value.BeatmapSetInfo;

            if (lastLookupResult.Value?.Result?.OnlineID == beatmapSetInfo.OnlineID && !force)
                return;

            onlineLookupCancellation?.Cancel();
            onlineLookupCancellation = null;

            if (beatmapSetInfo.OnlineID < 0)
            {
                lastLookupResult.Value = BeatmapSetLookupResult.Completed(null);
                return;
            }

            lastLookupResult.Value = BeatmapSetLookupResult.InProgress();
            onlineLookupCancellation = new CancellationTokenSource();
            currentOnlineLookup = onlineLookupSource.GetBeatmapSetAsync(beatmapSetInfo.OnlineID, onlineLookupCancellation.Token);
            currentOnlineLookup.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                    Schedule(() => lastLookupResult.Value = BeatmapSetLookupResult.Completed(t.GetResultSafely()));

                if (t.Exception != null)
                {
                    Logger.Log($"Error when fetching online beatmap set: {t.Exception}", LoggingTarget.Network);
                    Schedule(() => lastLookupResult.Value = BeatmapSetLookupResult.Completed(null));
                }
            });
        }

        #endregion

        #region osu!stable's footer

        private bool wantsStableFooter => stableLayouts.Value && skinManager.CurrentSkin.Value is LegacySkin;

        private void startTrackingLogo(OsuLogo logo, float duration = 0, Easing easing = Easing.None)
        {
            if (stableFooterShown)
                stableFooter?.StartTrackingLogo(logo, duration, easing);
            else
                Footer?.StartTrackingLogo(logo, duration, easing);
        }

        private void stopTrackingLogo()
        {
            stableFooter?.StopTrackingLogo();
            Footer?.StopTrackingLogo();
        }

        /// <summary>
        /// Swaps between this screen's footer and osu!stable's when the setting or the skin changes, and rebuilds osu!stable's
        /// for another legacy skin (it takes its images when it's made).
        /// </summary>
        private void updateStableFooter()
        {
            if (!wantsStableFooter && !stableFooterShown)
                return;

            bool tracking = this.IsCurrentScreen() && logo != null && ShowOsuLogo;

            if (tracking)
                stopTrackingLogo();

            bool wasShown = stableFooterShown;
            stableFooterShown = wantsStableFooter;

            applyStableTop();

            // Still osu!stable's list, with another skin: its panels again, with the new skin's images.
            if (wasShown && stableFooterShown)
                carousel.RefreshStablePanels();

            if (stableFooterShown)
            {
                stableFooterContainer.Child = stableFooter = createStableFooter();

                if (this.IsCurrentScreen())
                {
                    Footer?.Hide();
                    osuGame?.Toolbar.Hide();
                }
            }
            else
            {
                stableFooterContainer.Clear();
                stableFooter = null;

                if (this.IsCurrentScreen())
                {
                    Footer?.Show();
                    osuGame?.Toolbar.Show();
                }
            }

            if (tracking)
            {
                // The new footer has to be in place before the logo can follow it.
                if (stableFooter != null && !stableFooter.IsLoaded)
                    stableFooter.OnLoadComplete += _ => startTrackingLogo(logo!, 200, Easing.OutQuint);
                else
                    startTrackingLogo(logo!, 200, Easing.OutQuint);
            }
        }

        /// <summary>
        /// osu!stable's top and leaderboard instead of the title wedge, the filter control and the details area. The filter control
        /// stays (unseen) for typing and filtering.
        /// </summary>
        private void applyStableTop()
        {
            bool stable = stableFooterShown;

            // Not the wedges or the filter control themselves: they show and hide with their own transitions.
            // Their wrappers hide them (the details area's leaderboard then doesn't fetch either); the filter control stays
            // (for typing and filtering), drawn transparent.
            titleWedge.Parent!.Alpha = stable ? 0 : 1;
            detailsArea.Parent!.Alpha = stable ? 0 : 1;
            carousel.StableLayout = stable;
            FilterControl.Colour = stable ? Colour4.Transparent : Colour4.White;

            // osu!stable's footer has the skin's back button: not the game's as well (it shows when coming back to this screen otherwise).
            BackButtonVisibility.Value = !stable;

            if (stable)
            {
                var top = new LegacySongSelectTop(FilterControl.SearchText);
                stableTopContainer.Child = top;
                stableLeaderboardContainer.Child = stableLeaderboard = new LegacySongSelectLeaderboard();
                stableDropdownsContainer.Children = new Drawable[] { top.Dropdowns, stableLeaderboard.Controls };
                stableModeMenuContainer.Child = stableModeMenu = new LegacyModeMenu();
            }
            else
            {
                stableTopContainer.Clear();
                stableLeaderboardContainer.Clear();
                stableDropdownsContainer.Clear();
                stableModeMenuContainer.Clear();
                stableLeaderboard = null;
                stableModeMenu = null;
            }
        }

        /// <summary>
        /// osu!stable's top: the mouse doesn't reach song select's own (unseen) controls underneath it.
        /// </summary>
        private partial class StableTopContainer : Container
        {
            protected override bool Handle(UIEvent e)
            {
                if (Children.Count > 0 && e is MouseEvent)
                    return true;

                return base.Handle(e);
            }
        }

        private LegacyFooter createStableFooter()
        {
            return new LegacyFooter
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                BackAction = () =>
                {
                    if (this.IsCurrentScreen())
                        this.Exit();
                },
                // Stable's list of the modes.
                ModeAction = () => stableModeMenu?.ToggleVisibility(),
                ModsAction = showStableMods,
                RandomAction = () =>
                {
                    if (!carousel.NextRandom())
                        errorSample?.Play();
                },
                OptionsAction = showStableOptions,
            };
        }

        private LegacyButtonDialog? stableMods;

        /// <summary>
        /// Stable's mod selection (its dialog), with the game's own for mods' settings.
        /// </summary>
        private void showStableMods()
        {
            if (stableMods != null || !this.IsCurrentScreen())
                return;

            stableModeMenuContainer.Add(stableMods = new LegacyButtonDialog(
                "Mods provide different ways to enjoy gameplay. Some have an effect on the score you can achieve during ranked play. Others are just for fun.", new[]
            {
                new LegacyButtonDialog.DialogButton("Reset all mods", new Color4(255, 69, 0, 255), () => Mods.Value = Array.Empty<Mod>(), Closes: false),
                // Stable has no settings for mods (DT's speed and so on): the game's mod selection has them.
                new LegacyButtonDialog.DialogButton("Mod settings", new Color4(70, 130, 180, 255), () => modSelectOverlay.Show()),
                new LegacyButtonDialog.DialogButton("Close", new Color4(128, 128, 128, 255), null),
            })
            {
                Content = new LegacyModSelect(),
                ButtonsTop = LegacyModSelect.BUTTONS_TOP,
                Closed = () => stableMods = null,
            });
        }

        private LegacyButtonDialog? stableOptions;

        /// <summary>
        /// Stable's beatmap options: its dialog, with this screen's actions for them.
        /// </summary>
        private void showStableOptions()
        {
            if (!Beatmap.IsDefault)
                ShowStableOptions(Beatmap.Value.BeatmapInfo);
        }

        /// <summary>
        /// Stable's beatmap options for <paramref name="beatmap"/> (from F3, the options button, or right-clicking its panel).
        /// </summary>
        public void ShowStableOptions(BeatmapInfo beatmap)
        {
            if (stableOptions != null || !this.IsCurrentScreen())
                return;

            var actions = GetForwardActions(beatmap).ToList();

            // This screen's action with the given text (the ones the game's own options offer).
            Action? action(LocalisableString text) => actions.FirstOrDefault(a => a.Text.Value.ToString() == text.ToString())?.Action.Value;

            var markPlayed = action(SongSelectStrings.RemoveFromPlayed) ?? action(SongSelectStrings.MarkAsPlayed);

            var buttons = new List<LegacyButtonDialog.DialogButton>
            {
                new LegacyButtonDialog.DialogButton("Manage collections", new Color4(154, 205, 50, 255), ManageCollections),
                new LegacyButtonDialog.DialogButton("Delete...", new Color4(255, 69, 0, 255), () => Delete(beatmap.BeatmapSet!)),
            };

            // Stable's colours: purple for these two, orange-red for editing.
            if (markPlayed != null)
                buttons.Add(new LegacyButtonDialog.DialogButton(beatmap.LastPlayed == null ? "Mark as played" : "Remove from Unplayed", new Color4(208, 125, 216, 255), markPlayed));

            if (action(SongSelectStrings.ClearAllLocalScores) is Action clearScores)
                buttons.Add(new LegacyButtonDialog.DialogButton("Clear local scores", new Color4(208, 125, 216, 255), clearScores));

            if (action(ButtonSystemStrings.Edit.ToSentence()) is Action edit)
                buttons.Add(new LegacyButtonDialog.DialogButton("Edit", new Color4(255, 69, 0, 255), edit));

            // The game's own: the beatmap's page, and its link.
            if (action(CommonStrings.Details) is Action details)
                buttons.Add(new LegacyButtonDialog.DialogButton("View beatmap", new Color4(66, 135, 245, 255), details));

            if (action(CommonStrings.CopyLink) is Action copyLink)
                buttons.Add(new LegacyButtonDialog.DialogButton("Copy link", new Color4(66, 135, 245, 255), copyLink));

            buttons.Add(new LegacyButtonDialog.DialogButton("Cancel", new Color4(128, 128, 128, 255), null));

            stableModeMenuContainer.Add(stableOptions = new LegacyButtonDialog($"This beatmap: {beatmap.GetDisplayTitle()}", buttons)
            {
                Closed = () => stableOptions = null,
            });
        }

        #endregion

        #region Implementation of ISongSelect

        void ISongSelect.AddToSearch(string query) => FilterControl.AddToSearch(query);

        bool ISongSelect.CanPresentScore => true;

        void ISongSelect.PresentScore(ScoreInfo score, ScorePresentType presentType)
        {
            switch (presentType)
            {
                case ScorePresentType.Results:
                    Debug.Assert(Beatmap.Value.BeatmapInfo.Equals(score.BeatmapInfo));
                    Debug.Assert(Ruleset.Value.Equals(score.Ruleset));

                    this.Push(new SoloResultsScreen(score));
                    break;

                case ScorePresentType.Gameplay:
                    (game as OsuGame)?.PresentScore(score, presentType);
                    break;
            }
        }

        #endregion

        #region IHandlePresentBeatmap

        void IHandlePresentBeatmap.PresentBeatmap(WorkingBeatmap workingBeatmap, RulesetInfo ruleset)
        {
            unscopeBeatmapSet(restorePreviousSelection: false);
            cancelDebounceSelection();

            var beatmapInfo = workingBeatmap.BeatmapInfo;

            // Don't change the local ruleset if the user is on another ruleset and is showing converted beatmaps.
            // Eventually we probably want to check whether conversion is actually possible for the current ruleset.
            bool requiresRulesetSwitch = !beatmapInfo.Ruleset.Equals(Ruleset.Value)
                                         && (beatmapInfo.Ruleset.OnlineID > 0 || !showConvertedBeatmaps.Value);

            if (requiresRulesetSwitch)
            {
                Ruleset.Value = beatmapInfo.Ruleset;
                Beatmap.Value = workingBeatmap;

                Logger.Log($"Completing {nameof(IHandlePresentBeatmap.PresentBeatmap)} with beatmap {workingBeatmap} ruleset {beatmapInfo.Ruleset}");
            }
            else
            {
                Beatmap.Value = workingBeatmap;

                Logger.Log($"Completing {nameof(IHandlePresentBeatmap.PresentBeatmap)} with beatmap {workingBeatmap} (maintaining ruleset)");
            }
        }

        #endregion

        #region Beatmap management

        [Resolved]
        private ManageCollectionsDialog? manageCollectionsDialog { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        public virtual IEnumerable<OsuMenuItem> GetForwardActions(BeatmapInfo beatmap)
        {
            yield return new OsuMenuItem(GlobalActionKeyBindingStrings.Select, MenuItemType.Highlighted, () => SelectAndRun(beatmap, OnStart))
            {
                Icon = FontAwesome.Solid.Check
            };

            yield return new OsuMenuItemSpacer();

            if (beatmap.OnlineID > 0)
            {
                yield return new OsuMenuItem(CommonStrings.Details, MenuItemType.Standard, () => beatmapOverlay?.FetchAndShowBeatmap(beatmap.OnlineID));

                if (beatmap.GetOnlineURL(api, Ruleset.Value) is string url)
                    yield return new OsuMenuItem(CommonStrings.CopyLink, MenuItemType.Standard, () => (game as OsuGame)?.CopyToClipboard(url));
            }

            yield return new OsuMenuItemSpacer();

            foreach (var i in CreateCollectionMenuActions(beatmap))
                yield return i;
        }

        protected IEnumerable<OsuMenuItem> CreateCollectionMenuActions(BeatmapInfo beatmap)
        {
            var collectionItems = realm.Realm.All<BeatmapCollection>()
                                       .OrderBy(c => c.Name)
                                       .AsEnumerable()
                                       .Select(c => new CollectionToggleMenuItem(c.ToLive(realm), beatmap)).Cast<OsuMenuItem>().ToList();

            collectionItems.Add(new OsuMenuItem(CommonStrings.Manage, MenuItemType.Standard, () => manageCollectionsDialog?.Show()));

            yield return new OsuMenuItem(CommonStrings.Collections) { Items = collectionItems };
        }

        public void ManageCollections() => collectionsDialog?.Show();

        public void Delete(BeatmapSetInfo beatmapSet) => dialogOverlay?.Push(new BeatmapDeleteDialog(beatmapSet));

        public void RestoreAllHidden(BeatmapSetInfo beatmapSet) => beatmaps.Restore(beatmapSet);

        private GroupedBeatmap? beforeScopedSelection;

        private readonly Bindable<BeatmapSetInfo?> scopedBeatmapSet = new Bindable<BeatmapSetInfo?>();
        public IBindable<BeatmapSetInfo?> ScopedBeatmapSet => scopedBeatmapSet;

        public void ScopeToBeatmapSet(BeatmapSetInfo beatmapSet)
        {
            beforeScopedSelection = carousel.CurrentGroupedBeatmap;

            scopedBeatmapSet.Value = beatmapSet;
        }

        public void UnscopeBeatmapSet() => unscopeBeatmapSet(restorePreviousSelection: true);

        private void unscopeBeatmapSet(bool restorePreviousSelection)
        {
            if (scopedBeatmapSet.Value == null)
                return;

            if (beforeScopedSelection != null && restorePreviousSelection)
                queueBeatmapSelection(beforeScopedSelection);

            scopedBeatmapSet.Value = null;
            beforeScopedSelection = null;
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            modSelectOverlayRegistration?.Dispose();
        }
    }
}

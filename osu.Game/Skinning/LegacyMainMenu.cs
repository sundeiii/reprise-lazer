// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Screens.Menu;
using osu.Game.Skinning.Select;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Skinning
{
    /// <summary>
    /// osu!stable's main menu (its 2015 one): its own logo ("menu-osu", throbbing with the beat) left of the middle, the button images to
    /// its right (Play, Edit, Options, Exit; Play opens Freeplay, Multiplayer and Back), black bars along the top and bottom with the player
    /// and how many beatmaps there are, how long the game's been running and the time at the top left, a tip, the copyright at the bottom
    /// left and the osu!direct tab on the right edge. Left alone for six seconds, the buttons go and the logo glides to the middle.
    /// The buttons do what the game's own menu's do.
    /// </summary>
    /// <remarks>
    /// From stable's source (b20151228): its positions are from the middle of its 640x480 field, x1.6 here (the 768-high layout); the images
    /// are stable's own. The game's logo stays underneath, unseen (it takes the clicks and goes on into song select), with stable's drawn over it.
    /// </remarks>
    public partial class LegacyMainMenu : CompositeDrawable
    {
        /// <summary>
        /// The logo's middle, left of the screen's (in the 768-high layout), while the buttons are out.
        /// </summary>
        public const float LOGO_OFFSET = -120 * 1.6f;

        /// <summary>
        /// The clickable circle of stable's logo (300 across in its field), relative to the screen's height.
        /// </summary>
        public const float LOGO_HEIGHT = 300 * 1.6f / 768;

        // "menu-osu" (540 across) over that circle.
        private const float logo_image_size = 540;

        private const double idle_time = 6000;

        /// <summary>
        /// Where the logo's middle goes (relative to a screen of <paramref name="screenSize"/>): left of the middle, or in it when idle.
        /// </summary>
        public static Vector2 LogoPosition(Vector2 screenSize, bool idle)
        {
            if (idle || screenSize.X <= 0)
                return new Vector2(0.5f);

            // The layout's 768 high (wider on wide screens, but never narrower than 1024).
            float unit = Math.Min(screenSize.Y / 768, screenSize.X / 1024);
            return new Vector2(0.5f + LOGO_OFFSET * unit / screenSize.X, 0.5f);
        }

        private static readonly string[] tips =
        {
            "Try watching others as they play using spectator mode.",
            "You can press F2 in song select to pick a random beatmap.",
            "Right-click a beatmap in song select for its options.",
            "Press F1 in song select to choose mods.",
            "Hold ~ (tilde) during a beatmap to skip its intro.",
            "You can drag and drop beatmaps and skins onto the game to import them.",
            "Change your skin from the options to give the game a whole new look.",
            "Press Ctrl+T to bring the toolbar back if you miss it.",
        };

        private readonly ButtonSystem buttons;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private MusicController music { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private NowPlayingOverlay? nowPlaying { get; set; }

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private Container buttonLayer = null!;
        private StableLogo stableLogo = null!;
        private FillFlowContainer<OsuSpriteText> info = null!;
        private TruncatingSpriteText nowPlayingText = null!;

        // Reprise: the song's title in its original language when the setting prefers it.
        [Resolved]
        private osu.Framework.Configuration.FrameworkConfigManager frameworkConfig { get; set; } = null!;

        private IBindable<bool> preferOriginal = null!;

        private string titleOf(WorkingBeatmap working) => StableMetadata.ArtistTitle(working.Metadata).GetPreferred(preferOriginal.Value);
        private Sprite nowPlayingBar = null!;
        private Box progress = null!;
        private OsuSpriteText tip = null!;
        private Container corners = null!;

        private readonly Dictionary<ButtonSystemState, StableButton[]> levels = new Dictionary<ButtonSystemState, StableButton[]>();

        private int beatmapCount;
        private float fontScreenHeight = 1080;

        /// <summary>
        /// Where the corners and the tip are drawn, over the logo (which the game draws above the screens); without one, behind it.
        /// </summary>
        public Container? FrontTarget { get; init; }

        public LegacyMainMenu(ButtonSystem buttons)
        {
            this.buttons = buttons;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures, GameHost host, Configuration.OsuConfigManager config)
        {
            permanentSongInfo = config.GetBindable<bool>(Configuration.OsuSetting.StablePermanentSongInfo);

            beatmapCount = realm.Run(r => r.All<BeatmapSetInfo>().Count(s => !s.DeletePending && !s.Protected));

            if (host.Window != null)
                fontScreenHeight = host.Window.ClientSize.Height;

            levels[ButtonSystemState.TopLevel] = new[]
            {
                new StableButton(textures, "play", -125, () => buttons.State = ButtonSystemState.Play, flash: false),
                new StableButton(textures, "edit", -60, () => enterMode(() => buttons.OnEditBeatmap?.Invoke())),
                new StableButton(textures, "options", 5, () => buttons.OnSettings?.Invoke()),
                new StableButton(textures, "exit", 70, () => buttons.OnExit?.Invoke(null!)),
            };

            levels[ButtonSystemState.Play] = new[]
            {
                new StableButton(textures, "freeplay", -92.5f, () => enterMode(() => buttons.OnSolo?.Invoke())),
                new StableButton(textures, "multiplayer", -27.5f, () => buttons.State = ButtonSystemState.Multi, flash: false),
                new StableButton(textures, "back", 37.5f, () => buttons.State = ButtonSystemState.TopLevel, flash: false, sample: "menuback"),
            };

            // The game's online modes, as more of stable's buttons (made in their style: stable's Multiplayer went straight to its lobby).
            levels[ButtonSystemState.Multi] = new[]
            {
                new StableButton(textures, "lobby", -157.5f, () => enterMode(buttons.OpenMultiplayer)),
                new StableButton(textures, "rankedplay", -92.5f, () => enterMode(buttons.OpenRankedPlay)),
                new StableButton(textures, "playlists", -27.5f, () => enterMode(buttons.OpenPlaylists)),
                new StableButton(textures, "dailychallenge", 37.5f, () => enterMode(buttons.OpenDailyChallenge)),
                new StableButton(textures, "back", 102.5f, () => buttons.State = ButtonSystemState.Play, flash: false, sample: "menuback"),
            };

            InternalChild = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = new Vector2(1024, 768),
                Strategy = DrawSizePreservationStrategy.Minimum,
                Children = new Drawable[]
                {
                    // Behind stable's logo, which follows the game's (above the screens, unseen).
                    buttonLayer = new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        ChildrenEnumerable = levels.Values.SelectMany(l => l),
                    },
                    stableLogo = new StableLogo(textures, buttons),
                    corners = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                        Children = new Drawable[]
                        {
                            // Stable's: black at 40%, 54 high (in its field) along the top and the bottom.
                            new Box { RelativeSizeAxes = Axes.X, Height = 54 * 1.6f, Colour = Color4.Black, Alpha = 0.4f },
                            new Box
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                RelativeSizeAxes = Axes.X,
                                Height = 54 * 1.6f,
                                Colour = Color4.Black,
                                Alpha = 0.4f,
                            },
                            new LegacyFooterUser { Position = new Vector2(3 * 1.6f, 2 * 1.6f) },
                            // "You have ... beatmaps available! / osu! has been running for ... / It is currently ...", size 14 at 210 across.
                            info = new FillFlowContainer<OsuSpriteText>
                            {
                                X = 210 * 1.6f,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                ChildrenEnumerable = Enumerable.Range(0, 3).Select(_ => new OsuSpriteText
                                {
                                    Font = StableFont.Get(14, screenHeight: fontScreenHeight),
                                    Shadow = true,
                                }),
                            },
                            // A tip, size 16 across the middle 58 up from the bottom, gone after twelve seconds.
                            tip = new OsuSpriteText
                            {
                                Anchor = Anchor.BottomCentre,
                                Origin = Anchor.TopCentre,
                                Y = -58 * 1.6f,
                                Alpha = 0,
                                Font = StableFont.Get(16, screenHeight: fontScreenHeight),
                                Shadow = true,
                            },
                            createNowPlaying(textures),
                            createDirectTab(textures),
                            createCopyright(textures),
                        }
                    },
                }
            };

            FrontTarget?.Add(corners.CreateProxy());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            buttons.StateChanged += onStateChanged;
            preferOriginal = frameworkConfig.GetBindable<bool>(osu.Framework.Configuration.FrameworkSetting.ShowUnicode);
            beatmap.BindValueChanged(b => showTitle(titleOf(b.NewValue), b.OldValue != null), true);
            preferOriginal.BindValueChanged(_ => showTitle(titleOf(beatmap.Value), false));
            onStateChanged(buttons.State);

            tip.Text = tips[RNG.Next(tips.Length)];
            tip.Delay(500).FadeIn(1000).Delay(11000).FadeOut(500);
        }

        private ButtonSystemState? shownLevel;

        private void onStateChanged(ButtonSystemState state)
        {
            corners.FadeTo(state is ButtonSystemState.Exit or ButtonSystemState.EnteringMode ? 0 : 1, 300);
            stableLogo.FadeTo(state is ButtonSystemState.Exit or ButtonSystemState.EnteringMode ? 0 : 1, 200);

            if (state == ButtonSystemState.EnteringMode)
                return;

            ButtonSystemState? level = state switch
            {
                ButtonSystemState.TopLevel or ButtonSystemState.Edit => ButtonSystemState.TopLevel,
                ButtonSystemState.Play => ButtonSystemState.Play,
                ButtonSystemState.Multi => ButtonSystemState.Multi,
                _ => null,
            };

            if (level == shownLevel)
                return;

            // Stable's: forward from the left (30), back from the right (15), quickly.
            bool backwards = depth(level) < depth(shownLevel);
            shownLevel = level;

            // Six seconds from now before it's left alone.
            lastMovement = Time.Current;

            foreach (var (key, levelButtons) in levels)
            {
                foreach (var b in levelButtons)
                {
                    if (key == level)
                        b.Show(backwards ? 15 * 1.6f : -30 * 1.6f);
                    else
                        b.HideButton();
                }
            }

            setIdle(false);
        }

        private static int depth(ButtonSystemState? level) => level switch
        {
            ButtonSystemState.Play => 1,
            ButtonSystemState.Multi => 2,
            _ => 0,
        };

        // Stable's: the buttons not chosen slide away left.
        private void enterMode(Action? action)
        {
            foreach (var b in levels.Values.SelectMany(l => l))
                b.Leave();

            action?.Invoke();
        }

        #region Idle

        private Vector2 lastMousePosition;
        private double lastMovement;
        private bool idle;

        private void setIdle(bool value)
        {
            if (idle == value)
                return;

            idle = value;
            buttons.StableIdle = value;

            if (shownLevel is ButtonSystemState current)
            {
                foreach (var b in levels[current])
                {
                    if (value)
                        b.Idle();
                    else
                        b.Show(-100 * 1.6f, 350, 400);
                }
            }
        }

        #endregion

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat)
                return false;

            // Stable's shortcuts.
            switch (shownLevel, e.Key)
            {
                case (ButtonSystemState.TopLevel, Key.P):
                    buttons.State = ButtonSystemState.Play;
                    return true;

                case (ButtonSystemState.TopLevel, Key.E):
                    enterMode(() => buttons.OnEditBeatmap?.Invoke());
                    return true;

                case (ButtonSystemState.TopLevel, Key.O):
                    buttons.OnSettings?.Invoke();
                    return true;

                case (ButtonSystemState.TopLevel, Key.D):
                    buttons.OnBeatmapListing?.Invoke();
                    return true;

                case (ButtonSystemState.TopLevel, Key.Q):
                    buttons.OnExit?.Invoke(null!);
                    return true;

                case (ButtonSystemState.Play, Key.P):
                    enterMode(() => buttons.OnSolo?.Invoke());
                    return true;

                case (ButtonSystemState.Play, Key.M):
                    buttons.State = ButtonSystemState.Multi;
                    return true;

                case (ButtonSystemState.Play, Key.B):
                    buttons.State = ButtonSystemState.TopLevel;
                    return true;

                case (ButtonSystemState.Multi, Key.L):
                    enterMode(buttons.OpenMultiplayer);
                    return true;

                case (ButtonSystemState.Multi, Key.R):
                    enterMode(buttons.OpenRankedPlay);
                    return true;

                case (ButtonSystemState.Multi, Key.P):
                    enterMode(buttons.OpenPlaylists);
                    return true;

                case (ButtonSystemState.Multi, Key.D):
                    enterMode(buttons.OpenDailyChallenge);
                    return true;

                case (ButtonSystemState.Multi, Key.B):
                    buttons.State = ButtonSystemState.Play;
                    return true;
            }

            // Stable's music keys in the menu.
            switch (e.Key)
            {
                case Key.Z:
                case Key.Left:
                    music.PreviousTrack();
                    return true;

                case Key.X:
                    music.Play(requestedByUser: true);
                    return true;

                case Key.C:
                    if (music.IsPlaying)
                        music.TogglePause();
                    return true;

                case Key.V:
                case Key.Right:
                    music.NextTrack();
                    return true;

                case Key.J:
                    nowPlaying?.ToggleVisibility();
                    return true;
            }

            return base.OnKeyDown(e);
        }

        protected override void Update()
        {
            base.Update();

            // "menu-np" 100 (in stable's field) before the song, with it.
            nowPlayingBar.X = nowPlayingText.X - nowPlayingText.DrawWidth - 100 * 1.6f;
            nowPlayingBar.Alpha = nowPlayingText.Alpha;

            var lines = info.Children;
            lines[0].Text = $"You have {beatmapCount} beatmaps available!";
            lines[1].Text = $"osu! has been running for {runningFor(Time.Current)}.";
            lines[2].Text = $"It is currently {DateTime.Now.ToString("h:mm tt", CultureInfo.InvariantCulture)}.";

            var track = music.CurrentTrack;
            progress.Width = track.Length > 0 ? (float)Math.Clamp(track.CurrentTime / track.Length, 0, 1) : 0;

            // Stable's idle: six seconds without the mouse moving (with the buttons out).
            var mouse = GetContainingInputManager()?.CurrentState.Mouse.Position ?? Vector2.Zero;

            if (Vector2.DistanceSquared(mouse, lastMousePosition) > 25)
            {
                lastMousePosition = mouse;
                lastMovement = Time.Current;
                setIdle(false);
            }
            else if (shownLevel != null && Time.Current - lastMovement >= idle_time)
                setIdle(true);

            // Then the bars and the rest fade away slowly (back quickly); not while the menu's closing or leaving (they're going).
            if (buttons.State is not (ButtonSystemState.Exit or ButtonSystemState.EnteringMode) && !corners.Transforms.Any())
            {
                corners.Alpha = idle && shownLevel != null
                    ? Math.Max(0.01f, corners.Alpha - 0.00006f * (float)Time.Elapsed)
                    : Math.Min(1, corners.Alpha + 0.0048f * (float)Time.Elapsed);
            }
        }

        private static string runningFor(double milliseconds)
        {
            // Stable's: "12 seconds", then "01:23", then "01:02:03".
            var time = TimeSpan.FromMilliseconds(milliseconds);

            if (time.TotalHours >= 1)
                return time.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
            if (time.TotalMinutes >= 1)
                return time.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

            return $"{(int)time.TotalSeconds} seconds";
        }

        // Stable's music bar (from its 2015 source): seven controls 20 apart from the right edge, 11 down (31 while the song's shown),
        // the song's progress (134 across, 143 from the right, 20 down), and the song sliding in from the right with "menu-np" 100 before it.
        private const float control_y = 11 * 1.6f;
        private const float control_y_with_song = 31 * 1.6f;

        private Drawable createNowPlaying(TextureStore textures)
        {
            var controls = new (IconUsage Icon, float X, Action Action)[]
            {
                (FontAwesome.Solid.StepBackward, 136, () => music.PreviousTrack()),
                (FontAwesome.Solid.Play, 116, () => music.Play(requestedByUser: true)),
                (FontAwesome.Solid.Pause, 96, () =>
                {
                    if (music.IsPlaying)
                        music.TogglePause();
                }),
                (FontAwesome.Solid.Stop, 76, () => music.Stop(requestedByUser: true)),
                (FontAwesome.Solid.StepForward, 56, () => music.NextTrack()),
                (FontAwesome.Solid.Info, 36, togglePermanentSongInfo),
                (FontAwesome.Solid.Bars, 16, () => nowPlaying?.ToggleVisibility()),
            };

            musicControls = new Container
            {
                Anchor = Anchor.TopRight,
                Y = control_y,
                ChildrenEnumerable = controls.Select(c => new MusicControl(c.Icon, c.Action) { X = -c.X * 1.6f }),
            };

            var seekBar = new SeekBar(music)
            {
                Anchor = Anchor.TopRight,
                Position = new Vector2(-143 * 1.6f, 20 * 1.6f),
                Size = new Vector2(134 * 1.6f, 3 * 1.6f),
            };

            progress = seekBar.Fill;

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    nowPlayingBar = new Sprite
                    {
                        Anchor = Anchor.TopRight,
                        Texture = textures.Get(@"Stable/menu-np@2x"),
                        Size = new Vector2(682, 30),
                        Alpha = 0,
                    },
                    nowPlayingText = new TruncatingSpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Y = -2 * 1.6f,
                        Alpha = 0,
                        Font = StableFont.Get(14, screenHeight: fontScreenHeight),
                    },
                    seekBar,
                    musicControls,
                }
            };
        }

        private Container musicControls = null!;
        private Bindable<bool> permanentSongInfo = null!;

        private void togglePermanentSongInfo()
        {
            permanentSongInfo.Value = !permanentSongInfo.Value;
            showTitle(nowPlayingText.Text.ToString(), true);
        }

        // Stable's: the song slides in from the right over a second (the controls moving down under it), and unless it's to stay, slides
        // back out after six seconds (the controls back up).
        private void showTitle(string title, bool animated)
        {
            // Stable cuts it at 54 letters.
            nowPlayingText.Text = title.Length > 54 ? title[..54] + "..." : title;

            nowPlayingText.ClearTransforms();
            musicControls.ClearTransforms();

            nowPlayingText.MoveToX(80 * 1.6f).FadeOut()
                          .MoveToX(-10 * 1.6f, animated ? 1000 : 0, Easing.Out).FadeIn(animated ? 1000 : 0);
            musicControls.MoveToY(control_y_with_song, 400);

            if (permanentSongInfo.Value)
                return;

            nowPlayingText.Delay(6000).MoveToX(80 * 1.6f, 2000, Easing.Out).FadeOut(2000);
            musicControls.Delay(7600).MoveToY(control_y, 400);
        }

        // One of stable's music controls (FontAwesome at size 14), a little bigger under the mouse.
        private partial class MusicControl : ClickableContainer
        {
            private readonly SpriteIcon icon;
            private readonly SkinnableSound hoverSound;

            public MusicControl(IconUsage iconUsage, Action action)
            {
                Origin = Anchor.Centre;
                Size = new Vector2(14 * 4 / 3f);
                Action = action;

                Children = new Drawable[]
                {
                    icon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(0.75f),
                        Icon = iconUsage,
                        Shadow = true,
                    },
                    hoverSound = new SkinnableSound(new SampleInfo("click-short")),
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSound.Play();
                icon.ScaleTo(1.2f, 100, Easing.Out);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                icon.ScaleTo(1, 100, Easing.Out);
                base.OnHoverLost(e);
            }
        }

        // Stable's song progress: dark, lighter under the mouse; clicking seeks there.
        private partial class SeekBar : ClickableContainer
        {
            public readonly Box Fill;
            private readonly Box background;
            private readonly MusicController music;

            public SeekBar(MusicController music)
            {
                this.music = music;

                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(20, 20, 20, 128) },
                    Fill = new Box { RelativeSizeAxes = Axes.Both, Width = 0, Colour = new Color4(255, 255, 255, 128) },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeColour(new Color4(60, 60, 60, 128), 100);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeColour(new Color4(20, 20, 20, 128), 100);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                var track = music.CurrentTrack;

                if (track.Length > 0)
                    music.SeekTo(Math.Clamp(ToLocalSpace(e.ScreenSpaceMousePosition).X / DrawWidth, 0, 1) * track.Length);

                return true;
            }
        }

        // Stable's "menu-osudirect" in the middle of the right edge, lit under the mouse.
        private Drawable createDirectTab(TextureStore textures) => new HoverImageButton(textures.Get(@"Stable/menu-osudirect@2x"),
            textures.Get(@"Stable/menu-osudirect-over@2x"), new Vector2(50, 284))
        {
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            Action = () => buttons.OnBeatmapListing?.Invoke(),
        };

        // Stable's "menu-copyright" at the bottom left, to the website: orange and bouncing bigger under the mouse.
        private Drawable createCopyright(TextureStore textures) => new HoverImageButton(textures.Get(@"Stable/menu-copyright@2x"), null, new Vector2(244, 33))
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
            Position = new Vector2(2 * 1.6f, -2 * 1.6f),
            Bounce = true,
            Action = () => game?.OpenUrlExternally(api.Endpoints.WebsiteUrl),
        };

        private partial class HoverImageButton : ClickableContainer
        {
            private readonly Sprite image;
            private readonly Sprite? over;

            public bool Bounce { get; init; }

            public HoverImageButton(Texture? texture, Texture? overTexture, Vector2 size)
            {
                Size = size;

                Add(image = new Sprite { Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft, RelativeSizeAxes = Axes.Both, Texture = texture });

                if (overTexture != null)
                    Add(over = new Sprite { RelativeSizeAxes = Axes.Both, Texture = overTexture, Alpha = 0 });
            }

            protected override bool OnHover(HoverEvent e)
            {
                over?.FadeIn(100);

                if (Bounce)
                {
                    image.FadeColour(new Color4(255, 179, 59, 255), 100);
                    image.ScaleTo(1.1f, 540, Easing.OutElastic);
                }

                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                over?.FadeOut(300);
                image.FadeColour(Color4.White, 400);
                image.ScaleTo(1, 600, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            buttons.StateChanged -= onStateChanged;
            buttons.StableIdle = false;
        }

        /// <summary>
        /// One of stable's menu buttons ("menu-button-*" with its "-over" image), from the middle (100 left, <paramref name="y"/> down).
        /// It slides right under the mouse; choosing it flashes it blue (Play and Back don't).
        /// </summary>
        private partial class StableButton : ClickableContainer
        {
            private readonly Vector2 basePosition;
            private readonly Sprite over;
            private readonly Sprite image;
            private readonly bool flash;
            private readonly SkinnableSound hoverSound;
            private readonly SkinnableSound clickSound;

            private bool shown;

            public StableButton(TextureStore textures, string name, float y, Action action, bool flash = true, string sample = "menuhit")
            {
                this.flash = flash;
                Action = action;
                Name = name;

                basePosition = new Vector2(-100 * 1.6f, y * 1.6f);
                Position = basePosition;
                Size = new Vector2(583, 89);
                Alpha = 0;

                Children = new Drawable[]
                {
                    image = new Sprite { RelativeSizeAxes = Axes.Both, Texture = textures.Get($@"Stable/menu-button-{name}@2x") },
                    over = new Sprite { RelativeSizeAxes = Axes.Both, Texture = textures.Get($@"Stable/menu-button-{name}-over@2x"), Alpha = 0 },
                    hoverSound = new SkinnableSound(new SampleInfo("menuclick")),
                    clickSound = new SkinnableSound(new SampleInfo(sample)),
                };
            }

            public override bool HandlePositionalInput => shown;

            public void Show(float fromX, double fadeDuration = 80, double moveDuration = 200)
            {
                shown = true;
                ClearTransforms();
                this.MoveTo(basePosition + new Vector2(fromX, 0)).FadeInFromZero(fadeDuration)
                    .MoveTo(basePosition, moveDuration, Easing.Out);
            }

            public void HideButton()
            {
                shown = false;
                ClearTransforms(targetMember: nameof(Position));
                this.FadeOut(160);
                over.FadeOut(160);
            }

            public void Idle()
            {
                if (!shown)
                    return;

                shown = false;
                this.FadeOut(500).MoveTo(basePosition - new Vector2(100 * 1.6f, 0), 2000, Easing.Out);
                over.FadeOut(500);
            }

            public void Leave()
            {
                if (!shown)
                    return;

                shown = false;
                this.MoveTo(Position - new Vector2(50 * 1.6f, 0), 100).FadeOut(100);
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSound.Play();
                over.FadeIn(30);
                this.MoveTo(basePosition + new Vector2(20 * 1.6f, 0), 580, Easing.OutElastic);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                over.FadeOut(500);

                if (shown)
                    this.MoveTo(basePosition, 400, Easing.Out);

                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                clickSound.Play();

                if (flash)
                {
                    // Stable's: a blue copy over it, fading.
                    var copy = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = image.Texture,
                        Colour = new Color4(0, 78, 165, 255),
                        Blending = BlendingParameters.Additive,
                        Alpha = 0.6f,
                    };

                    Add(copy);
                    copy.FadeOut(500).Expire();
                }

                return base.OnClick(e);
            }
        }

        /// <summary>
        /// Stable's logo ("menu-osu") over the game's (unseen) one: throbbing with the beat, a little bigger under the mouse, with a
        /// shockwave each beat.
        /// </summary>
        private partial class StableLogo : BeatSyncedContainer
        {
            private readonly ButtonSystem buttons;
            private readonly Container logoContainer;
            private readonly Sprite logoSprite;
            private readonly Texture? shockwave;

            public Sprite Image => logoSprite;

            private double lastBeatTime;
            private double beatLength = 1000;
            private float hoverBoost;
            private float pulse;
            private float averageAmplitude;

            public StableLogo(TextureStore textures, ButtonSystem buttons)
            {
                this.buttons = buttons;
                RelativeSizeAxes = Axes.Both;

                shockwave = textures.Get(@"Stable/menu-osu-shockwave@2x");

                Child = logoContainer = new Container
                {
                    Origin = Anchor.Centre,
                    Size = new Vector2(logo_image_size),
                    Child = logoSprite = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Texture = textures.Get(@"Stable/menu-osu@2x"),
                    },
                };
            }

            protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
            {
                base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);

                lastBeatTime = Time.Current;
                beatLength = Math.Max(timingPoint.BeatLength, 100);

                if (!IsPresent || shockwave == null)
                    return;

                // Stable's: a faint ring growing out of the logo over a second.
                var ring = new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = logoContainer.Position,
                    Size = logoContainer.Size * logoSprite.Scale,
                    Texture = shockwave,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0.1f * (0.6f + 0.4f * Math.Clamp(averageAmplitude, 0, 1)),
                };

                AddInternal(ring);
                ring.ScaleTo(1.4f, 1000, Easing.Out).FadeOut(1000).Expire();
            }

            protected override void Update()
            {
                base.Update();

                var logo = buttons.Logo;

                if (logo?.Parent == null)
                    return;

                // Where the game's logo is; stable's size (540 in the 768-high layout), following the game's logo's scale from there
                // (its own size changes with its bounce and hover).
                logoContainer.Position = ToLocalSpace(logo.ScreenSpaceDrawQuad.Centre);
                logoContainer.Size = new Vector2(logo_image_size * logo.Scale.X / Math.Max(0.01f, buttons.StableLogoScale));

                // Stable's throb: from 1.05 down towards 1 with each beat (and the music's loudness), a little bigger under the mouse.
                float amplitude = BeatSyncSource.CurrentAmplitudes.Average;
                averageAmplitude = averageAmplitude * 0.9f + 0.1f * amplitude;

                float beatProgress = (float)Math.Clamp((Time.Current - lastBeatTime) / beatLength, 0, 1);
                float loudness = 1 - Math.Clamp((amplitude - averageAmplitude) / 0.5f, 0, 1);
                float target = Math.Clamp(1 - (beatProgress * 0.5f + loudness * 0.5f), 0, 1);
                float smoothing = (float)Math.Pow(0.5, Time.Elapsed / 16.666);
                pulse = pulse * smoothing + target * (1 - smoothing);

                hoverBoost = logo.IsHovered
                    ? Math.Min(0.1f, hoverBoost + 0.0007f * (float)Time.Elapsed)
                    : Math.Max(0, hoverBoost - 0.0007f * (float)Time.Elapsed);

                logoSprite.Scale = new Vector2(1.05f + hoverBoost - 0.05f * pulse);
            }
        }
    }
}

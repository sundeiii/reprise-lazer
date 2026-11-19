// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers.Markdown;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Updater
{
    /// <summary>
    /// Reprise: what's new in this version, after an update: its release notes from GitHub (reprise.pw has no changelog of lazer builds),
    /// shown as the game's own changelog is (a full overlay with the changelog's header, the build at the top, then its notes); for an
    /// update that's ready, a bar at the bottom restarts to it.
    /// </summary>
    public partial class WhatsNewOverlay : FullscreenOverlay<WhatsNewOverlay.WhatsNewHeader>
    {
        /// <summary>The releases (by tag: the game's version is its tag, "v2026.1021.1-lazer").</summary>
        public const string RELEASES_API = @"https://api.github.com/repos/sundeiii/reprise-lazer/releases/tags/";

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        private OsuSpriteText buildVersion = null!;
        private OsuMarkdownContainer notes = null!;
        private Container restartFooter = null!;
        private RoundedButton restartButton = null!;
        private OverlayScrollContainer scroll = null!;

        /// <summary>Whether the bar restarting to a ready update is shown.</summary>
        public bool OffersRestart => restartFooter.Alpha > 0;

        public WhatsNewOverlay()
            : base(OverlayColourScheme.Purple)
        {
            // Updated while hidden: the notes come in (ShowFor) while it is, and what it schedules then (showing them, or restarting
            // without them) never ran on a hidden overlay, so clicking "update ready" or "see what's new" did nothing.
            AlwaysPresent = true;
        }

        protected override WhatsNewHeader CreateHeader() => new WhatsNewHeader();

        protected override osuTK.Graphics.Color4 BackgroundColour => ColourProvider.Background4;

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.AutoSize) },
                Content = new[]
                {
                    new Drawable[]
                    {
                        scroll = new OverlayScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            ScrollbarVisible = false,
                            Child = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    Header,
                                    // The build, as the changelog heads one: its stream, then its version in the stream's colour.
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Margin = new MarginPadding { Top = 30, Bottom = 10 },
                                        Children = new Drawable[]
                                        {
                                            new OsuSpriteText
                                            {
                                                Text = @"lazer ",
                                                Font = OsuFont.GetFont(weight: FontWeight.Medium, size: 19),
                                            },
                                            buildVersion = new OsuSpriteText
                                            {
                                                Font = OsuFont.GetFont(weight: FontWeight.Light, size: 19),
                                                Colour = ColourProvider.Highlight1,
                                            },
                                        }
                                    },
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Bottom = 30 },
                                        Child = notes = new OsuMarkdownContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                        },
                                    },
                                }
                            }
                        },
                    },
                    // An update that's ready (its notes shown before restarting): restarting to it from here.
                    new Drawable[]
                    {
                        restartFooter = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Alpha = 0,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = ColourProvider.Background5,
                                },
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(15),
                                    Padding = new MarginPadding { Vertical = 15 },
                                    Children = new Drawable[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Text = @"This update is downloaded and ready.",
                                            Font = OsuFont.GetFont(size: 14),
                                            Colour = ColourProvider.Content2,
                                        },
                                        restartButton = new RoundedButton
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Width = 220,
                                            Height = 40,
                                            Text = "Restart to update",
                                        },
                                    }
                                },
                            }
                        }
                    },
                }
            };
        }

        /// <summary>
        /// Shows what's new in <paramref name="version"/> once its notes come (not at all when there are none or they can't be had).
        /// With <paramref name="restart"/> (an update that's ready), a button restarts to it; without notes to show, it restarts straight away.
        /// </summary>
        public void ShowFor(string version, Action? restart = null)
        {
            // The notes come in on another thread; what's done with them is scheduled here (which runs while hidden: AlwaysPresent).
            FetchNotes(version,
                body => Schedule(() =>
                {
                    if (!ShowNotes(version, body, restart))
                        restart?.Invoke();
                }),
                e =>
                {
                    Logger.Log($"No release notes for {version}: {e.Message}");

                    if (restart != null)
                        Schedule(restart);
                });
        }

        /// <summary>Gets a release's notes (its tag's, from GitHub), calling back from another thread. Tests give their own.</summary>
        protected virtual void FetchNotes(string version, Action<string?> onNotes, Action<Exception> onFailed)
        {
            var request = new OsuJsonWebRequest<GitHubRelease>(RELEASES_API + version);

            request.Finished += () => onNotes(request.ResponseObject?.Body);
            request.Failed += onFailed;
            // Its failure is reported through Failed; observed here so it isn't left as an unobserved exception.
            request.PerformAsync().ContinueWith(t => _ = t.Exception, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// Shows <paramref name="body"/> (a release's notes, markdown) as what's new in <paramref name="version"/>, unless there's nothing in it;
        /// with <paramref name="restart"/>, a button restarts to it.
        /// </summary>
        /// <returns>Whether there was something to show.</returns>
        public bool ShowNotes(string version, string? body, Action? restart = null)
        {
            body = Clean(body);

            if (string.IsNullOrWhiteSpace(body))
                return false;

            buildVersion.Text = DisplayVersion(version);
            Header.Version = DisplayVersion(version);
            notes.Text = body;
            scroll.ScrollToStart(false);

            restartFooter.Alpha = restart != null ? 1 : 0;
            restartButton.Action = () =>
            {
                Hide();
                restart?.Invoke();
            };

            // The notifications closed (opened from one, they stayed open, and an open panel takes the next click anywhere to close
            // itself: "Restart to update" took two clicks).
            notificationOverlay?.Hide();

            Show();
            return true;
        }

        /// <summary>A release's tag as the changelog shows a build: "v2026.1119.3-lazer" is 2026.1119.3.</summary>
        public static string DisplayVersion(string version)
        {
            string v = version.StartsWith('v') ? version[1..] : version;
            return v.EndsWith(@"-lazer", StringComparison.Ordinal) ? v[..^@"-lazer".Length] : v;
        }

        /// <summary>The notes without GitHub's "Full Changelog" link (to a compare page, nothing to read in the game).</summary>
        public static string? Clean(string? body) =>
            body == null
                ? null
                : string.Join('\n', body.Replace("\r\n", "\n").Split('\n').Where(l => !l.StartsWith(@"**Full Changelog**", StringComparison.Ordinal))).Trim();

        /// <summary>The changelog's header (its background and icon), titled "what's new", with the build as its breadcrumb.</summary>
        public partial class WhatsNewHeader : BreadcrumbControlOverlayHeader
        {
            private const string listing = @"release notes";

            private string? version;

            public WhatsNewHeader()
            {
                TabControl.AddItem(listing);
                Current.Value = listing;
            }

            /// <summary>The build shown, as the breadcrumb after "release notes".</summary>
            public string? Version
            {
                get => version;
                set
                {
                    if (version == value)
                        return;

                    if (version != null)
                        TabControl.RemoveItem(version);

                    version = value;

                    if (version != null)
                    {
                        TabControl.AddItem(version);
                        Current.Value = version;
                    }
                }
            }

            protected override Drawable CreateBackground() => new OverlayHeaderBackground(@"Headers/changelog");

            protected override OverlayTitle CreateTitle() => new WhatsNewTitle();

            private partial class WhatsNewTitle : OverlayTitle
            {
                public WhatsNewTitle()
                {
                    Title = @"what's new";
                    Description = @"what's changed in this version of reprise lazer";
                    Icon = OsuIcon.ChangelogB;
                }
            }
        }
    }
}

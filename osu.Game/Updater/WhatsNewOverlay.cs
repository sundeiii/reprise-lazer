// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Containers.Markdown;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osuTK;

namespace osu.Game.Updater
{
    /// <summary>
    /// Reprise: what's new in this version, after an update: its release notes from GitHub (reprise.pw has no changelog of lazer builds).
    /// </summary>
    public partial class WhatsNewOverlay : OsuFocusedOverlayContainer
    {
        /// <summary>The releases (by tag: the game's version is its tag, "v2026.1021.1-lazer").</summary>
        public const string RELEASES_API = @"https://api.github.com/repos/sundeiii/reprise-lazer/releases/tags/";

        private const double enter_duration = 500;
        private const double exit_duration = 200;

        protected override string PopInSampleName => @"UI/overlay-big-pop-in";
        protected override string PopOutSampleName => @"UI/overlay-big-pop-out";

        private OsuSpriteText title = null!;
        private OsuMarkdownContainer notes = null!;

        public WhatsNewOverlay()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(0.5f, 0.7f);

            Masking = true;
            CornerRadius = 10;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    Colour = colours.GreySeaFoamDark,
                    RelativeSizeAxes = Axes.Both,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    title = new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Font = OsuFont.GetFont(size: 26, weight: FontWeight.SemiBold),
                                        Padding = new MarginPadding { Vertical = 12 },
                                    },
                                    new IconButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Icon = FontAwesome.Solid.Times,
                                        Colour = colours.GreySeaFoamDarker,
                                        Scale = new Vector2(0.8f),
                                        X = -10,
                                        Action = Hide,
                                    }
                                }
                            }
                        },
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = colours.GreySeaFoamDarker,
                                    },
                                    new OsuScrollContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Child = new Container
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Padding = new MarginPadding(20),
                                            Child = notes = new OsuMarkdownContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                            },
                                        },
                                    },
                                }
                            }
                        },
                    }
                }
            };
        }

        /// <summary>Shows what's new in <paramref name="version"/> once its notes come (not at all when there are none or they can't be had).</summary>
        public void ShowFor(string version)
        {
            var request = new OsuJsonWebRequest<GitHubRelease>(RELEASES_API + version);

            request.Finished += () => Schedule(() => ShowNotes(version, request.ResponseObject?.Body));

            request.Failed += e => Logger.Log($"No release notes for {version}: {e.Message}");
            request.PerformAsync();
        }

        /// <summary>Shows <paramref name="body"/> (a release's notes, markdown) as what's new in <paramref name="version"/>, unless there's nothing in it.</summary>
        public void ShowNotes(string version, string? body)
        {
            body = Clean(body);

            if (string.IsNullOrWhiteSpace(body))
                return;

            title.Text = $"What's new in {version}";
            notes.Text = body;
            Show();
        }

        /// <summary>The notes without GitHub's "Full Changelog" link (to a compare page, nothing to read in the game).</summary>
        public static string? Clean(string? body) =>
            body == null
                ? null
                : string.Join('\n', body.Replace("\r\n", "\n").Split('\n').Where(l => !l.StartsWith(@"**Full Changelog**", System.StringComparison.Ordinal))).Trim();

        protected override void PopIn()
        {
            this.FadeIn(enter_duration, Easing.OutQuint);
            this.ScaleTo(0.9f).Then().ScaleTo(1f, enter_duration, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            base.PopOut();

            this.FadeOut(exit_duration, Easing.OutQuint);
            this.ScaleTo(0.9f, exit_duration);
        }
    }
}

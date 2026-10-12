// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Scoring;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Statistics.Session
{
    /// <summary>
    /// Browse the stats of every recorded session (or of all plays combined), or the progress made on each map.
    /// </summary>
    public partial class SessionBrowserScreen : OsuScreen
    {
        /// <summary>
        /// What the list on the left is made of.
        /// </summary>
        public enum ListMode
        {
            Sessions,
            Maps,
        }

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [Resolved]
        private SessionStatsStore store { get; set; } = null!;

        [Resolved]
        private ScoreManager? scoreManager { get; set; }

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private readonly Bindable<ListMode> listMode = new Bindable<ListMode>();

        private FillFlowContainer<SessionListItem> list = null!;
        private OsuSpriteText heading = null!;
        private Container detailHolder = null!;
        private CancellationTokenSource? loadCancellation;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    // leave room for the toolbar at the top and the back button at the bottom.
                    Padding = new MarginPadding { Top = Overlays.Toolbar.Toolbar.HEIGHT + 10, Bottom = 80, Left = 20, Right = 20 },
                    Child = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new[] { createHeader() },
                            new[]
                            {
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(GridSizeMode.Absolute, 340),
                                        new Dimension(GridSizeMode.Absolute, 20),
                                        new Dimension(),
                                    },
                                    Content = new[]
                                    {
                                        new[] { createListPanel(), new Container(), createDetailPanel() }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            listMode.BindValueChanged(_ => populateList(), true);
        }

        private static Drawable createHeader() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Padding = new MarginPadding { Bottom = 16 },
            Spacing = new Vector2(0, 2),
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = "Session stats",
                    Font = OsuFont.GetFont(size: 30, weight: FontWeight.Bold),
                },
                new OsuSpriteText
                {
                    Text = "How your plays are going, by session or by map",
                    Font = OsuFont.GetFont(size: 14),
                    Colour = Color4.White.Opacity(0.6f),
                },
            }
        };

        private Drawable createListPanel() => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 10,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 34,
                                Padding = new MarginPadding { Horizontal = 8, Top = 6 },
                                Child = new OsuTabControl<ListMode>
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Current = listMode,
                                }
                            }
                        },
                        new Drawable[]
                        {
                            new OsuScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = list = new FillFlowContainer<SessionListItem>
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Padding = new MarginPadding(8),
                                    Spacing = new Vector2(0, 4),
                                }
                            }
                        },
                    }
                }
            }
        };

        private Drawable createDetailPanel() => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 10,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4,
                },
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(20),
                        Spacing = new Vector2(0, 16),
                        Children = new Drawable[]
                        {
                            heading = new OsuSpriteText
                            {
                                Font = OsuFont.GetFont(size: 24, weight: FontWeight.Bold),
                            },
                            detailHolder = new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                            },
                        }
                    }
                }
            }
        };

        private void populateList()
        {
            list.Clear();
            detailHolder.Clear();

            var entries = listMode.Value == ListMode.Sessions ? createSessionEntries() : createMapEntries();

            if (entries.Count == 0)
            {
                heading.Text = listMode.Value == ListMode.Sessions ? "No sessions yet" : "No maps yet";

                detailHolder.Child = new OsuSpriteText
                {
                    Text = "Finish a map to the results screen and your stats will show up here.",
                    Font = OsuFont.GetFont(size: 16),
                    Colour = Color4.White.Opacity(0.6f),
                };

                return;
            }

            foreach (var entry in entries)
            {
                SessionListItem? item = null;

                item = new SessionListItem(entry)
                {
                    Action = () => select(item!),
                };

                list.Add(item);
            }

            // prefer the current session, otherwise the first (most recent) entry after "all time".
            int initial = entries.FindIndex(e => e.IsCurrent);

            if (initial < 0)
                initial = listMode.Value == ListMode.Sessions && entries.Count > 1 ? 1 : 0;

            select(list[initial]);
        }

        private List<SessionEntry> createSessionEntries()
        {
            var sessions = store.GetSessions();
            var entries = new List<SessionEntry>();

            if (sessions.Count == 0)
                return entries;

            var allPlays = sessions.SelectMany(s => s.Plays).OrderBy(p => p.PlayedAt).ToList();
            entries.Add(new SessionEntry("All time", describeSession(allPlays), allPlays));

            foreach (var session in sessions)
            {
                bool isCurrent = session.SessionId == store.SessionId;

                entries.Add(new SessionEntry(
                    $"{session.Start.LocalDateTime:ddd d MMM · HH:mm}{(isCurrent ? " (current)" : string.Empty)}",
                    describeSession(session.Plays),
                    session.Plays,
                    isCurrent));
            }

            return entries;
        }

        private List<SessionEntry> createMapEntries()
            => MapGroup.Create(store.AllPlays).Select(map => new SessionEntry(map.Beatmap, describeMap(map.Plays), map.Plays)).ToList();

        private static string describeSession(IReadOnlyList<SessionPlayRecord> plays)
        {
            var summary = SessionSummary.Create(plays);
            string unstableRate = summary.AverageUnstableRate?.ToString("N1") ?? "-";

            return $"{plays.Count} play{(plays.Count == 1 ? string.Empty : "s")} · {summary.AverageAccuracy.FormatAccuracy()} · UR {unstableRate}";
        }

        private static string describeMap(IReadOnlyList<SessionPlayRecord> plays)
        {
            string bestAccuracy = plays.Max(p => p.Accuracy).FormatAccuracy().ToString();
            double? bestUnstableRate = plays.Where(p => p.UnstableRate != null).Select(p => p.UnstableRate).Min();

            return $"{plays.Count} attempt{(plays.Count == 1 ? string.Empty : "s")} · best {bestAccuracy} · best UR {bestUnstableRate?.ToString("N1") ?? "-"}";
        }

        private void select(SessionListItem item)
        {
            foreach (var other in list)
                other.Selected = other == item;

            heading.Text = item.Entry.Title;

            loadCancellation?.Cancel();
            loadCancellation = new CancellationTokenSource();

            LoadComponentAsync(new SessionStatsDisplay(item.Entry.Plays, detailed: true, playClicked: openPlay), display => detailHolder.Child = display, loadCancellation.Token);
        }

        /// <summary>
        /// Opens the results of a recorded play, if the score is still around.
        /// </summary>
        private void openPlay(SessionPlayRecord record)
        {
            var score = scoreManager?.Query(s => s.ID == record.ScoreID);

            if (score == null)
            {
                notifications?.Post(new SimpleNotification { Text = "That score is no longer available." });
                return;
            }

            game?.PresentScore(score);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            loadCancellation?.Cancel();
        }

        /// <param name="Title">The heading of the entry.</param>
        /// <param name="Subtitle">A line summarising the entry.</param>
        /// <param name="Plays">The plays the entry stands for, oldest first.</param>
        /// <param name="IsCurrent">Whether this is the current session.</param>
        public record SessionEntry(string Title, string Subtitle, IReadOnlyList<SessionPlayRecord> Plays, bool IsCurrent = false);

        public partial class SessionListItem : OsuClickableContainer
        {
            public SessionEntry Entry { get; }

            private Box background = null!;
            private bool selected;

            public bool Selected
            {
                get => selected;
                set
                {
                    selected = value;
                    updateState();
                }
            }

            public SessionListItem(SessionEntry entry)
            {
                Entry = entry;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Masking = true;
                CornerRadius = 6;

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Highlight1,
                        Alpha = 0,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(10),
                        Spacing = new Vector2(0, 2),
                        Children = new Drawable[]
                        {
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = Entry.Title,
                                Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                            },
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = Entry.Subtitle,
                                Font = OsuFont.GetFont(size: 12),
                                Colour = Color4.White.Opacity(0.7f),
                            },
                        }
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                updateState();
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                base.OnHoverLost(e);
                updateState();
            }

            private void updateState() => background?.FadeTo(selected ? 0.3f : IsHovered ? 0.12f : 0f, 100);
        }
    }
}

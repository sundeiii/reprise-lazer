// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Notifications;
using osuTK;

namespace osu.Game.Overlays.LazerImport
{
    /// <summary>
    /// Reprise: things from osu!lazer in a list to tick (searchable; the ones here already ticked off in green), imported by the wizard's
    /// button. Long lists show their first matches (the search finds the rest); "Select all" ticks every match.
    /// </summary>
    public abstract partial class LazerImportScreen : WizardScreen
    {
        /// <summary>Rows shown at once (the search narrows the rest down).</summary>
        private const int max_rows = 100;

        /// <summary>Something there, as its row shows it.</summary>
        protected class Entry
        {
            public readonly object Item;
            public readonly string Title;
            public readonly string Subtitle;
            public readonly bool AlreadyHere;
            public readonly BindableBool Selected = new BindableBool();

            public Entry(object item, string title, string subtitle, bool alreadyHere)
            {
                Item = item;
                Title = title;
                Subtitle = subtitle;
                AlreadyHere = alreadyHere;
            }

            public bool Matches(string[] terms) => terms.All(t => Title.Contains(t, StringComparison.OrdinalIgnoreCase) || Subtitle.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>What it brings over, one and several ("skin", "skins").</summary>
        protected abstract string Noun { get; }

        protected abstract string NounPlural { get; }

        private string count(int n) => $"{n} {(n == 1 ? Noun : NounPlural)}";

        /// <summary>The wizard's button: importing what's ticked, or just closing with nothing ticked.</summary>
        public override LocalisableString? NextStepText
        {
            get
            {
                int selected = SelectedCount;
                return selected == 0 ? "Close" : $"Import {count(selected)}";
            }
        }

        public int SelectedCount => entries.Count(e => e.Selected.Value);

        [Resolved]
        private LazerImportOverlay? overlay { get; set; }

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private OsuTextFlowContainer description = null!;
        private SearchTextBox search = null!;
        private LoadingSpinner loading = null!;
        private FillFlowContainer<LazerImportRow> list = null!;
        private OsuTextFlowContainer moreText = null!;
        private RoundedButton selectAllButton = null!;

        private LazerDataSource? source;
        private CancellationTokenSource? readCancellation;
        private ScheduledDelegate? filterDelegate;

        private IReadOnlyList<Entry> entries = Array.Empty<Entry>();

        /// <summary>Where osu!lazer is (for tests); otherwise looked for on this computer.</summary>
        public Func<LazerDataSource?>? FindSource { get; init; }

        [BackgroundDependencyLoader]
        private void load()
        {
            Content.Children = new Drawable[]
            {
                description = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Colour = OverlayColourProvider.Content1,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 10), new Dimension(GridSizeMode.Absolute, 200) },
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new Drawable?[]
                        {
                            search = new BasicSearchTextBox
                            {
                                RelativeSizeAxes = Axes.X,
                            },
                            null,
                            selectAllButton = new RoundedButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 40,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = "Select all",
                                Action = selectAllMatching,
                            },
                        }
                    }
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        loading = new LoadingSpinner
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Margin = new MarginPadding { Top = 40 },
                        },
                        list = new FillFlowContainer<LazerImportRow>
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(5),
                        },
                    }
                },
                moreText = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Colour = OverlayColourProvider.Content2,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    TextAnchor = Anchor.TopCentre,
                },
            };

            search.Current.BindValueChanged(_ =>
            {
                filterDelegate?.Cancel();
                filterDelegate = Scheduler.AddDelayed(filter, 150);
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Refresh();
        }

        /// <summary>
        /// What osu!lazer has, each with whether it's here already. Runs in the background (reading a copy of osu!lazer's database, and ours).
        /// </summary>
        protected abstract IReadOnlyList<Entry> ReadEntries(LazerDataSource source);

        /// <summary>
        /// Brings the given ones over, in the background, saying how it goes on the notification (which it completes or cancels).
        /// </summary>
        protected abstract Task Import(LazerDataSource source, IReadOnlyList<object> items, ProgressNotification notification);

        /// <summary>osu!lazer's read again (and what's here already).</summary>
        public void Refresh()
        {
            if (!IsLoaded)
                return;

            readCancellation?.Cancel();
            var cancellation = readCancellation = new CancellationTokenSource();

            entries = Array.Empty<Entry>();
            list.Clear();
            moreText.Text = string.Empty;
            search.Alpha = selectAllButton.Alpha = 0;
            updateWizardButton();
            loading.Show();
            description.Text = $"Reading osu!lazer's {NounPlural}...";

            source = FindSource != null ? FindSource() : LazerDataSource.Find(host);

            if (source == null)
            {
                loading.Hide();
                description.Text = "osu!lazer isn't on this computer (or hasn't been started yet), so there's nothing to bring over.";
                return;
            }

            var lazerSource = source;

            Task.Run(() => ReadEntries(lazerSource), cancellation.Token).ContinueWith(t => Schedule(() =>
            {
                if (cancellation.IsCancellationRequested)
                    return;

                loading.Hide();

                if (!t.IsCompletedSuccessfully)
                {
                    Logger.Error(t.Exception?.InnerException ?? t.Exception, $"Couldn't read osu!lazer's {NounPlural}");
                    description.Text = $"Couldn't read osu!lazer's {NounPlural}. Close osu!lazer and try again.";
                    return;
                }

                entries = t.GetResultSafely();

                if (entries.Count == 0)
                {
                    description.Text = $"osu!lazer has no {NounPlural} of its own to bring over.";
                    return;
                }

                foreach (var entry in entries)
                    entry.Selected.BindValueChanged(_ => updateWizardButton());

                int alreadyHere = entries.Count(e => e.AlreadyHere);
                description.Text = $"osu!lazer on this computer has {count(entries.Count)}"
                                   + (alreadyHere > 0 ? $", {alreadyHere} of them here already" : string.Empty)
                                   + ". Tick the ones to bring over and import them with the button below. osu!lazer's own stay as they are.";

                search.Alpha = selectAllButton.Alpha = 1;
                filter();
                updateWizardButton();

                Schedule(() => GetContainingFocusManager()?.ChangeFocus(search));
            }));
        }

        private string[] searchTerms => search.Current.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        private void filter()
        {
            string[] terms = searchTerms;
            var matching = entries.Where(e => e.Matches(terms)).ToList();

            list.ChildrenEnumerable = matching.Take(max_rows).Select(e => new LazerImportRow(e.Title, e.Subtitle, e.AlreadyHere, e.Selected));

            moreText.Text = matching.Count > max_rows
                ? $"and {matching.Count - max_rows} more (search to find them; \"Select all\" ticks them too)."
                : matching.Count == 0 && entries.Count > 0
                    ? "Nothing matches."
                    : string.Empty;
        }

        private void selectAllMatching()
        {
            string[] terms = searchTerms;
            var matching = entries.Where(e => !e.AlreadyHere && e.Matches(terms)).ToList();
            bool select = matching.Any(e => !e.Selected.Value);

            foreach (var entry in matching)
                entry.Selected.Value = select;
        }

        // The wizard's button says how many are ticked.
        private void updateWizardButton()
        {
            if (overlay?.NextButton is ShearedButton button && NextStepText is LocalisableString text)
                button.Text = text;
        }

        /// <summary>
        /// Imports what's ticked in the background (the notification shows how it goes), so the wizard can close meanwhile.
        /// </summary>
        public void ImportSelected()
        {
            if (source == null || !IsLoaded)
                return;

            var lazerSource = source;
            var selected = entries.Where(e => e.Selected.Value).Select(e => e.Item).ToList();

            if (selected.Count == 0)
                return;

            var notification = new ProgressNotification
            {
                Text = $"Bringing {count(selected.Count)} over from osu!lazer...",
                State = ProgressNotificationState.Active,
            };
            notifications?.Post(notification);

            Task.Run(async () =>
            {
                try
                {
                    await Import(lazerSource, selected, notification).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Couldn't import {NounPlural} from osu!lazer");
                    notification.State = ProgressNotificationState.Cancelled;
                }
            });
        }

        protected string CountOf(int n) => count(n);

        protected static void DeleteTemporaryFolder(string folder)
        {
            try
            {
                if (System.IO.Directory.Exists(folder))
                    System.IO.Directory.Delete(folder, true);
            }
            catch
            {
                // A leftover in the temporary folder.
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Notifications;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Overlays.LazerSkinImport
{
    /// <summary>
    /// Reprise: osu!lazer's skins in a list to tick (searchable; the ones here already ticked off), imported by the wizard's button as
    /// osu!lazer exports them.
    /// </summary>
    [LocalisableDescription(typeof(ScreenImportLazerSkins), nameof(Title))]
    public partial class ScreenImportLazerSkins : WizardScreen
    {
        public static LocalisableString Title => "Pick the skins to bring over";

        /// <summary>The wizard's button: importing the ticked skins, or just closing with none ticked.</summary>
        public override LocalisableString? NextStepText
        {
            get
            {
                int count = SelectedCount;
                return count == 0 ? "Close" : $"Import {count} skin{(count == 1 ? string.Empty : "s")}";
            }
        }

        public int SelectedCount => IsLoaded ? list.Count(r => r.Selected.Value) : 0;

        [Resolved]
        private LazerSkinImportOverlay? overlay { get; set; }

        // The wizard's button says how many are ticked.
        private void updateWizardButton()
        {
            if (overlay?.NextButton is ShearedButton button && NextStepText is LocalisableString text)
                button.Text = text;
        }

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private OsuTextFlowContainer description = null!;
        private SearchTextBox search = null!;
        private LoadingSpinner loading = null!;
        private FillFlowContainer<SkinRow> list = null!;
        private RoundedButton selectAllButton = null!;

        private LazerSkinSource? source;
        private CancellationTokenSource? readCancellation;

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
                                Text = "Select all shown",
                                Action = selectAllShown,
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
                        list = new FillFlowContainer<SkinRow>
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(5),
                        },
                    }
                },
            };

            search.Current.BindValueChanged(_ => filter());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Refresh();
        }

        /// <summary>osu!lazer's skins read again (and which are here already).</summary>
        public void Refresh()
        {
            if (!IsLoaded)
                return;

            readCancellation?.Cancel();
            var cancellation = readCancellation = new CancellationTokenSource();

            list.Clear();
            search.Alpha = selectAllButton.Alpha = 0;
            updateWizardButton();
            loading.Show();
            description.Text = "Reading osu!lazer's skins...";

            source = LazerSkinSource.Find(host);

            if (source == null)
            {
                loading.Hide();
                description.Text = "osu!lazer isn't on this computer (or hasn't been started yet), so there's nothing to bring over.";
                return;
            }

            // Ours already, by name and creator.
            var ours = realm.Run(r => r.All<SkinInfo>().Where(s => !s.DeletePending).AsEnumerable().Select(s => (s.Name, s.Creator)).ToHashSet());
            var lazerSource = source;

            Task.Run(() => lazerSource.ReadSkins(), cancellation.Token).ContinueWith(t => Schedule(() =>
            {
                if (cancellation.IsCancellationRequested)
                    return;

                loading.Hide();

                if (!t.IsCompletedSuccessfully)
                {
                    Logger.Error(t.Exception?.InnerException ?? t.Exception, "Couldn't read osu!lazer's skins");
                    description.Text = "Couldn't read osu!lazer's skins. Close osu!lazer and try again.";
                    return;
                }

                var found = t.GetResultSafely();

                if (found.Count == 0)
                {
                    description.Text = "osu!lazer has no skins of its own to bring over.";
                    return;
                }

                list.ChildrenEnumerable = found.Select(s => new SkinRow(s, ours.Contains((s.Name, s.Creator))));

                foreach (var row in list)
                    row.Selected.BindValueChanged(_ => updateWizardButton());

                int alreadyHere = list.Count(r => r.AlreadyHere);
                description.Text = $"osu!lazer on this computer has {list.Count} skin{(list.Count == 1 ? string.Empty : "s")}"
                                   + (alreadyHere > 0 ? $", {alreadyHere} of them here already" : string.Empty)
                                   + ". Tick the ones to bring over and import them with the button below. osu!lazer's own stay as they are.";

                search.Alpha = selectAllButton.Alpha = 1;
                filter();
                updateWizardButton();

                Schedule(() => GetContainingFocusManager()?.ChangeFocus(search));
            }));
        }

        private void filter()
        {
            string[] terms = search.Current.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var row in list)
            {
                string text = row.Skin.ToString();
                row.Alpha = terms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
            }
        }

        private void selectAllShown()
        {
            var shown = list.Where(r => r.Alpha > 0 && !r.AlreadyHere).ToList();
            bool select = shown.Any(r => !r.Selected.Value);

            foreach (var row in shown)
                row.Selected.Value = select;
        }

        /// <summary>
        /// Imports the ticked skins in the background (the notifications show how it goes), so the wizard can close meanwhile.
        /// </summary>
        public void ImportSelected()
        {
            if (source == null || !IsLoaded)
                return;

            var lazerSource = source;
            var selected = list.Where(r => r.Selected.Value).Select(r => r.Skin).ToList();

            if (selected.Count == 0)
                return;

            var notification = new ProgressNotification
            {
                Text = $"Copying {selected.Count} skin{(selected.Count == 1 ? string.Empty : "s")} from osu!lazer...",
                State = ProgressNotificationState.Active,
            };
            notifications?.Post(notification);

            var importer = skins;

            Task.Run(async () =>
            {
                string folder = Path.Combine(Path.GetTempPath(), $@"reprise-lazer-skins-import-{Guid.NewGuid():N}");

                try
                {
                    var paths = new List<string>();

                    for (int i = 0; i < selected.Count; i++)
                    {
                        if (notification.CancellationToken.IsCancellationRequested)
                            return;

                        try
                        {
                            paths.Add(lazerSource.WriteOsk(selected[i], folder));
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, $"Couldn't copy {selected[i]} from osu!lazer");
                        }

                        notification.Text = $"Copying skins from osu!lazer ({i + 1} of {selected.Count})...";
                        notification.Progress = (float)(i + 1) / selected.Count;
                    }

                    // The import's own notification takes over from here.
                    notification.CompletionText = $"Copied {paths.Count} skin{(paths.Count == 1 ? string.Empty : "s")} from osu!lazer.";
                    notification.State = ProgressNotificationState.Completed;

                    await importer.Import(paths.ToArray()).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Couldn't import skins from osu!lazer");
                    notification.State = ProgressNotificationState.Cancelled;
                }
                finally
                {
                    try
                    {
                        if (Directory.Exists(folder))
                            Directory.Delete(folder, true);
                    }
                    catch
                    {
                        // A leftover in the temporary folder.
                    }
                }
            });
        }

        /// <summary>A skin: its name and who made it, ticked to import; one that's here already ticked off in green.</summary>
        private partial class SkinRow : OsuClickableContainer
        {
            public readonly LazerSkinSource.LazerSkin Skin;
            public readonly bool AlreadyHere;
            public readonly BindableBool Selected = new BindableBool();

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            private Box background = null!;
            private Container tickBox = null!;
            private Box tickFill = null!;
            private SpriteIcon tick = null!;

            public SkinRow(LazerSkinSource.LazerSkin skin, bool alreadyHere)
            {
                Skin = skin;
                AlreadyHere = alreadyHere;

                RelativeSizeAxes = Axes.X;
                Height = 50;
                Masking = true;
                CornerRadius = 8;
                Action = () => Selected.Toggle();
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 15 },
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.Absolute, 15),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new Drawable?[]
                            {
                                // A filled box (an outline alone isn't drawn), its border the outline.
                                tickBox = new Container
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(22),
                                    Masking = true,
                                    CornerRadius = 6,
                                    BorderThickness = 2,
                                    Children = new Drawable[]
                                    {
                                        tickFill = new Box { RelativeSizeAxes = Axes.Both },
                                        tick = new SpriteIcon
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Size = new Vector2(12),
                                            Icon = FontAwesome.Solid.Check,
                                        },
                                    }
                                },
                                null,
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Text = Skin.Name.Trim(),
                                            Font = OsuFont.Default.With(size: 16, weight: FontWeight.SemiBold),
                                            ShowTooltip = true,
                                        },
                                        new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Text = string.IsNullOrWhiteSpace(Skin.Creator) ? "Unknown" : Skin.Creator.Trim(),
                                            Font = OsuFont.Default.With(size: 13),
                                            Colour = colourProvider.Content2,
                                        },
                                    }
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Margin = new MarginPadding { Left = 10 },
                                    Text = "here already",
                                    Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                                    Colour = colours.Green1,
                                    Alpha = AlreadyHere ? 1 : 0,
                                },
                            }
                        }
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Selected.BindValueChanged(_ => updateState(), true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateState();
                base.OnHoverLost(e);
            }

            private void updateState()
            {
                bool selected = Selected.Value;

                background.FadeColour(selected ? colourProvider.Background3 : IsHovered ? colourProvider.Background4 : colourProvider.Background5, 150, Easing.OutQuint);

                // Ticked to import: filled in; here already (and not ticked): ticked off in green.
                tickBox.BorderColour = selected ? colourProvider.Highlight1 : AlreadyHere ? colours.Green1 : colourProvider.Light4;
                tickFill.FadeColour(selected ? colourProvider.Highlight1 : colourProvider.Background6, 150, Easing.OutQuint);
                tick.Colour = selected ? colourProvider.Background6 : colours.Green1;
                tick.FadeTo(selected || AlreadyHere ? 1 : 0, 150, Easing.OutQuint);
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
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

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>
    /// Reprise: skins from osu!'s own lazer on this computer, picked from a list, imported as osu!lazer would export them.
    /// </summary>
    public partial class ImportLazerSkinsButton : SettingsButtonV2, IHasPopover
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            Text = "Import skins from osu!lazer...";
            Action = this.ShowPopover;
        }

        public Popover GetPopover() => new ImportLazerSkinsPopover();
    }

    public partial class ImportLazerSkinsPopover : OsuPopover
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private readonly OsuTextFlowContainer status;
        private readonly SearchTextBox search;
        private readonly FillFlowContainer<SkinRow> list;
        private readonly RoundedButton importButton;
        private readonly RoundedButton selectAllButton;

        private LazerSkinSource? source;

        public ImportLazerSkinsPopover()
        {
            AutoSizeAxes = Axes.Both;
            Origin = Anchor.TopCentre;

            Child = new FillFlowContainer
            {
                Direction = FillDirection.Vertical,
                AutoSizeAxes = Axes.Y,
                Width = 380,
                Spacing = new Vector2(10),
                Children = new Drawable[]
                {
                    status = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 14))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Text = "Reading osu!lazer's skins...",
                    },
                    search = new BasicSearchTextBox
                    {
                        RelativeSizeAxes = Axes.X,
                        Alpha = 0,
                    },
                    new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 360,
                        Child = list = new FillFlowContainer<SkinRow>
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(4),
                            Padding = new MarginPadding { Right = 10 },
                        },
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 10), new Dimension() },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable?[]
                            {
                                selectAllButton = new RoundedButton
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 40,
                                    Text = "Select all shown",
                                    Action = selectAllShown,
                                    Enabled = { Value = false },
                                },
                                null,
                                importButton = new RoundedButton
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 40,
                                    Text = "Import",
                                    Action = import,
                                    Enabled = { Value = false },
                                },
                            }
                        }
                    },
                }
            };

            search.Current.BindValueChanged(_ => filter());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            source = LazerSkinSource.Find(host);

            if (source == null)
            {
                status.Text = "osu!lazer isn't on this computer (or has no data yet).";
                return;
            }

            // Ours already, by name and creator: shown, but not ticked by "select all".
            var ours = realm.Run(r => r.All<SkinInfo>().Where(s => !s.DeletePending).AsEnumerable().Select(s => (s.Name, s.Creator)).ToHashSet());

            var lazerSource = source;

            Task.Run(() => lazerSource.ReadSkins()).ContinueWith(t => Schedule(() =>
            {
                if (!t.IsCompletedSuccessfully)
                {
                    Logger.Error(t.Exception?.InnerException ?? t.Exception, "Couldn't read osu!lazer's skins");
                    status.Text = "Couldn't read osu!lazer's skins. Close osu!lazer and try again.";
                    return;
                }

                var found = t.GetResultSafely();

                if (found.Count == 0)
                {
                    status.Text = "osu!lazer has no skins of its own to import.";
                    return;
                }

                list.ChildrenEnumerable = found.Select(s => new SkinRow(s, ours.Contains((s.Name, s.Creator))));

                foreach (var row in list)
                    row.Selected.BindValueChanged(_ => updateState());

                search.Alpha = 1;
                selectAllButton.Enabled.Value = true;
                updateState();
            }));
        }

        protected override void PopIn()
        {
            base.PopIn();
            Schedule(() => GetContainingFocusManager()?.ChangeFocus(search));
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

        private void updateState()
        {
            int count = list.Count(r => r.Selected.Value);
            int alreadyHere = list.Count(r => r.AlreadyHere);

            importButton.Text = count == 0 ? "Import" : $"Import {count}";
            importButton.Enabled.Value = count > 0;
            status.Text = $"{list.Count} skins in osu!lazer" + (alreadyHere > 0 ? $" ({alreadyHere} here already)" : string.Empty)
                          + ". Tick the ones to import.";
        }

        private void import()
        {
            if (source == null)
                return;

            var lazerSource = source;
            var selected = list.Where(r => r.Selected.Value).Select(r => r.Skin).ToList();

            if (selected.Count == 0)
                return;

            this.HidePopover();

            var notification = new ProgressNotification
            {
                Text = $"Copying {selected.Count} skin{(selected.Count == 1 ? string.Empty : "s")} from osu!lazer...",
                State = ProgressNotificationState.Active,
            };
            notifications?.Post(notification);

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

                        notification.Progress = (float)(i + 1) / selected.Count;
                    }

                    // The import's own notification takes over from here.
                    notification.CompletionText = $"Copied {paths.Count} skin{(paths.Count == 1 ? string.Empty : "s")} from osu!lazer, importing.";
                    notification.State = ProgressNotificationState.Completed;

                    await skins.Import(paths.ToArray()).ConfigureAwait(false);
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

        private partial class SkinRow : CompositeDrawable
        {
            public readonly LazerSkinSource.LazerSkin Skin;
            public readonly bool AlreadyHere;
            public readonly BindableBool Selected = new BindableBool();

            public SkinRow(LazerSkinSource.LazerSkin skin, bool alreadyHere)
            {
                Skin = skin;
                AlreadyHere = alreadyHere;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                InternalChild = new OsuCheckbox(nubOnRight: false)
                {
                    LabelText = alreadyHere ? $"{skin} (here already)" : skin.ToString(),
                    Current = Selected,
                    Alpha = alreadyHere ? 0.6f : 1,
                };
            }
        }
    }
}

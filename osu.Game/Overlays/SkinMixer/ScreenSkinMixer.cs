// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.IO.Stores;
using osu.Framework.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Notifications;
using osu.Game.Skinning;
using osu.Game.Skinning.Mixer;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Overlays.SkinMixer
{
    /// <summary>
    /// Reprise: the skin mixer's one step: a base skin, a name, and for each part of a skin (grouped as OsuSkinMixer groups them, a group
    /// setting all its parts) the base's own, the game's default, blank, or another skin's. The wizard's button makes the skin and uses it.
    /// </summary>
    [LocalisableDescription(typeof(ScreenSkinMixer), nameof(Title))]
    public partial class ScreenSkinMixer : WizardScreen, ISkinMakingScreen
    {
        public static LocalisableString Title => "Mix a skin";

        public override LocalisableString? NextStepText => "Create skin";

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        /// <summary>The game's classic skin as a base: no files of its own.</summary>
        private static readonly SkinMixerSource classic = new SkinMixerSource(@"osu! (classic)", Array.Empty<KeyValuePair<string, Func<byte[]>>>());

        private readonly Bindable<SkinMixerSource> baseSkin = new Bindable<SkinMixerSource>(classic);
        private readonly Bindable<string> name = new Bindable<string>(string.Empty);

        private SkinMixerOption[] parts = SkinMixerParts.Create();
        private List<SkinMixerSource> sources = new List<SkinMixerSource>();

        private FormDropdown<SkinMixerSource> baseDropdown = null!;
        private FillFlowContainer partsFlow = null!;

        private readonly List<PartRow> rows = new List<PartRow>();

        [BackgroundDependencyLoader]
        private void load()
        {
            sources = ReadSkins(realm, storage);

            Content.Children = new Drawable[]
            {
                new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Colour = OverlayColourProvider.Content1,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = "Start from a skin, then pick what to change: each part can come from another of your skins, go back to the game's "
                           + "default, or be left blank to hide it. A group changes all of its parts. The new skin is made as a copy; the "
                           + "skins it's made from stay as they are.",
                },
                baseDropdown = new FormDropdown<SkinMixerSource>
                {
                    Caption = "Base skin",
                    AlwaysShowSearchBar = true,
                    HintText = "What's kept wherever nothing else is picked.",
                    Items = new[] { classic }.Concat(sources).ToArray(),
                    Current = baseSkin,
                },
                new FormTextBox
                {
                    Caption = "New skin's name",
                    Current = name,
                },
                partsFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(10),
                    // Room under the last parts for their lists to open below them.
                    Margin = new MarginPadding { Bottom = 300 },
                },
            };

            // The skin in use, when it's one of these.
            string current = skins.CurrentSkinInfo.Value.PerformRead(s => s.Name);
            baseSkin.Value = sources.FirstOrDefault(s => s.Name == current) ?? classic;

            baseSkin.BindValueChanged(b => name.Value = suggestName(b.NewValue), true);

            foreach (var part in parts)
                partsFlow.Add(new PartRow(this, part));
        }

        private string suggestName(SkinMixerSource from)
        {
            string[] existing = realm.Run(r => r.All<SkinInfo>().Where(s => !s.DeletePending).AsEnumerable().Select(s => s.Name).ToArray());
            return NamingUtils.GetNextBestName(existing, $@"{from.Name} (mixed)");
        }

        /// <summary>The installed skins (not the game's own), as the mixer reads them: their files, read when used.</summary>
        public static List<SkinMixerSource> ReadSkins(RealmAccess realm, Storage storage)
        {
            var files = storage.GetStorageForDirectory(@"files");

            return realm.Run(r => r.All<SkinInfo>()
                                   .Where(s => !s.DeletePending && !s.Protected)
                                   .AsEnumerable()
                                   .Where(s => s.Files.Any())
                                   .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                                   .Select(s => new SkinMixerSource(s.Name, s.Files.Select(f =>
                                   {
                                       string path = f.File.GetStoragePath();
                                       return new KeyValuePair<string, Func<byte[]>>(f.Filename, () => read(files, path));
                                   }).ToList()))
                                   .ToList());
        }

        private static byte[] read(Storage files, string path)
        {
            using var stream = files.GetStream(path);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        /// <summary>The choices a part offers: keep, default, blank (images and sounds), then each skin.</summary>
        private IEnumerable<SkinMixerChoice> choicesFor(SkinMixerOption part)
        {
            yield return SkinMixerChoice.KEEP;
            yield return SkinMixerChoice.DEFAULT;

            if (part.CanBeBlank)
                yield return SkinMixerChoice.BLANK;

            foreach (var s in sources)
                yield return SkinMixerChoice.From(s);
        }

        /// <summary>A part's choice as shown: its own, or a group's when all its parts agree ("Mixed" when they don't).</summary>
        private static SkinMixerChoice shownChoice(SkinMixerOption part)
        {
            var choices = part.Leaves.Select(l => l.Choice).Distinct().ToArray();
            return choices.Length == 1 ? choices[0] : SkinMixerChoice.MIXED;
        }

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private ISampleStore gameSamples { get; set; } = null!;

        private readonly Dictionary<SkinMixerSource, ISampleStore> previewStores = new Dictionary<SkinMixerSource, ISampleStore>();
        private readonly List<ScheduledDelegate> pendingPreview = new List<ScheduledDelegate>();

        /// <summary>
        /// Plays a set of sounds one after another as picked (another skin's, the base's to keep it, the game's for its default; nothing
        /// for blank); a sound the skin hasn't got is the game's, as in play.
        /// </summary>
        private void preview(string[] names, SkinMixerChoice choice)
        {
            if (choice.Type == SkinMixerChoice.Kind.Blank)
                return;

            var from = choice.Type switch
            {
                SkinMixerChoice.Kind.FromSkin => choice.Skin,
                SkinMixerChoice.Kind.Default => null,
                _ => baseSkin.Value,
            };

            foreach (var p in pendingPreview)
                p.Cancel();
            pendingPreview.Clear();

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                pendingPreview.Add(Scheduler.AddDelayed(() => playPreview(from, name), i * 350));
            }
        }

        private void playPreview(SkinMixerSource? from, string name)
        {
            string? file = from?.Files.Keys.FirstOrDefault(f => !f.Contains('/') && Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase)
                                                               && new[] { ".wav", ".ogg", ".mp3" }.Contains(Path.GetExtension(f).ToLowerInvariant()));

            if (from != null && file != null)
            {
                if (!previewStores.TryGetValue(from, out var store))
                    previewStores[from] = store = audio.GetSampleStore(new SkinMixerSourceStore(from));

                store.Get(file)?.Play();
                return;
            }

            gameSamples.Get($@"Gameplay/{name}")?.Play();
        }

        protected override void Dispose(bool isDisposing)
        {
            foreach (var store in previewStores.Values)
                store.Dispose();
            previewStores.Clear();

            base.Dispose(isDisposing);
        }

        /// <summary>Scrolls so an opened list (under its field, <see cref="list_height"/> at most) is all in view.</summary>
        private void reveal(Drawable field)
        {
            var scroll = this.ChildrenOfType<OsuScrollContainer>().First();

            double bottom = scroll.GetChildPosInContent(field) + field.DrawHeight + list_height;
            double needed = bottom - scroll.DisplayableContent;

            if (needed > scroll.Target)
                scroll.ScrollTo(needed);
        }

        private const float list_height = 260 + 50;

        private void refreshRows()
        {
            foreach (var row in rows)
                row.Refresh();
        }

        /// <summary>Makes the skin (in the background) and imports it; <paramref name="onCreated"/> gets it (from another thread; null if it failed).</summary>
        public void Create(Action<Live<SkinInfo>?> onCreated)
        {
            var from = baseSkin.Value;
            var chosen = parts;
            string skinName = string.IsNullOrWhiteSpace(name.Value) ? suggestName(from) : name.Value.Trim();

            // A fresh set of parts for the next time the mixer's opened.
            parts = SkinMixerParts.Create();

            Task.Run(async () =>
            {
                try
                {
                    var files = SkinMixing.Mix(from, chosen, skinName);

                    var stream = new MemoryStream();

                    using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                    {
                        foreach (var file in files)
                        {
                            using var entry = zip.CreateEntry(file.Key).Open();
                            entry.Write(file.Value);
                        }
                    }

                    stream.Position = 0;

                    var imported = await skins.Import(new ImportTask(stream, $@"{skinName}.osk")).ConfigureAwait(false);

                    if (imported == null)
                        throw new InvalidOperationException("The skin couldn't be imported.");

                    onCreated(imported);

                    notifications?.Post(new SimpleNotification
                    {
                        Text = $"Made \"{skinName}\" and put it on.",
                        Icon = FontAwesome.Solid.PaintBrush,
                    });
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Couldn't make the skin \"{skinName}\"");
                    onCreated(null);
                }
            });
        }

        /// <summary>
        /// A part (or group) with its choice, in the same form field as the base skin's; a group opens (its arrow) to its parts, indented
        /// under it.
        /// </summary>
        private partial class PartRow : CompositeDrawable
        {
            private const float gutter = 36;

            private readonly ScreenSkinMixer screen;
            private readonly SkinMixerOption part;

            private ChoiceDropdown dropdown = null!;
            private SpriteIcon? chevron;
            private GroupToggle? groupToggle;
            private Container? previewHolder;
            private FillFlowContainer? children;
            private bool updating;

            public PartRow(ScreenSkinMixer screen, SkinMixerOption part)
            {
                this.screen = screen;
                this.part = part;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                var group = part as SkinMixerGroup;

                dropdown = new ChoiceDropdown
                {
                    Caption = part.Name,
                    HintText = group != null ? $"{group.Leaves.Count()} parts: picking here sets them all." : default(LocalisableString),
                    // A list of every skin: searchable, and no taller than a few of them.
                    AlwaysShowSearchBar = true,
                    Items = screen.choicesFor(part).ToArray(),
                    Opened = () =>
                    {
                        bringToFront();
                        screen.reveal(dropdown);
                    },
                };

                var header = new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, gutter),
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, group?.PreviewFileNames != null ? gutter + 8 : 0),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            group == null
                                ? Empty()
                                : groupToggle = new GroupToggle(toggle)
                                {
                                    // As tall as the field (not its open list, which the cell grows by).
                                    RelativeSizeAxes = Axes.X,
                                    Child = chevron = new SpriteIcon
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Size = new Vector2(14),
                                        Icon = FontAwesome.Solid.ChevronRight,
                                        Colour = colourProvider.Content1,
                                    },
                                },
                            dropdown,
                            group?.PreviewFileNames == null
                                ? Empty()
                                : previewHolder = new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Child = new IconButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Icon = FontAwesome.Solid.Play,
                                        TooltipText = "Hear these (as picked here)",
                                        Action = () => screen.preview(group.PreviewFileNames, shownChoice(part)),
                                    },
                                },
                        }
                    }
                };

                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(10),
                    Children = group == null
                        ? new Drawable[] { header }
                        : new Drawable[]
                        {
                            header,
                            children = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(10),
                                Padding = new MarginPadding { Left = gutter },
                                Alpha = 0,
                            }
                        },
                };

                screen.rows.Add(this);
                Refresh();

                dropdown.Current.BindValueChanged(c =>
                {
                    // "Mixed" is only shown (it isn't in the list), never picked.
                    if (updating || c.NewValue.Type == SkinMixerChoice.Kind.Mixed)
                        return;

                    foreach (var leaf in part.Leaves)
                        leaf.Choice = leaf.CanBeBlank || c.NewValue.Type != SkinMixerChoice.Kind.Blank ? c.NewValue : SkinMixerChoice.DEFAULT;

                    screen.refreshRows();
                });
            }

            protected override void Update()
            {
                base.Update();

                if (groupToggle != null)
                    groupToggle.Height = dropdown.MainDrawHeight;

                if (previewHolder != null)
                    previewHolder.Height = dropdown.MainDrawHeight;
            }

            /// <summary>Shows the part's choice as it is now (after a group above or below it changed).</summary>
            public void Refresh()
            {
                updating = true;
                dropdown.Current.Value = shownChoice(part);
                updating = false;
            }

            // A group's parts are made the first time it's opened (there are hundreds of them).
            private void toggle()
            {
                if (children == null || part is not SkinMixerGroup group)
                    return;

                if (children.Count == 0)
                {
                    foreach (var child in group.Children)
                        children.Add(new PartRow(screen, child));
                }

                bool open = children.Alpha == 0;
                children.Alpha = open ? 1 : 0;
                chevron?.RotateTo(open ? 90 : 0, 200, Easing.OutQuint);
            }

            protected override void Dispose(bool isDisposing)
            {
                screen.rows.Remove(this);
                base.Dispose(isDisposing);
            }

            /// <summary>Draws this row (and the groups it's in) over the rows after it, so its open list isn't covered by them.</summary>
            private void bringToFront()
            {
                Drawable row = this;

                while (row.Parent is Container<Drawable> container)
                {
                    if (row is PartRow)
                        container.ChangeChildDepth(row, --front_depth);

                    if (container == screen.partsFlow)
                        break;

                    row = container;
                }
            }

            private static float front_depth;

            /// <summary>The form's dropdown, a few skins tall, saying when its list opens.</summary>
            private partial class ChoiceDropdown : FormDropdown<SkinMixerChoice>
            {
                public Action? Opened;

                public ChoiceDropdown()
                {
                    MaxHeight = list_height - 50;
                }

                protected override DropdownMenu CreateMenu()
                {
                    var menu = base.CreateMenu();
                    menu.StateChanged += state =>
                    {
                        if (state == MenuState.Open)
                            Opened?.Invoke();
                    };
                    return menu;
                }
            }

            private partial class GroupToggle : OsuClickableContainer
            {
                public GroupToggle(Action action)
                {
                    Action = action;
                }
            }
        }
    }

    /// <summary>Reprise: a mixer skin's files, read as resources (for playing its sounds).</summary>
    internal class SkinMixerSourceStore : IResourceStore<byte[]>
    {
        private readonly SkinMixerSource source;

        public SkinMixerSourceStore(SkinMixerSource source)
        {
            this.source = source;
        }

        public byte[] Get(string name) => source.Files.TryGetValue(name, out var read) ? read() : null!;

        public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.Run(() => Get(name), cancellationToken);

        public Stream? GetStream(string name) => source.Files.TryGetValue(name, out var read) ? new MemoryStream(read()) : null;

        public IEnumerable<string> GetAvailableResources() => source.Files.Keys;

        public void Dispose()
        {
        }
    }
}

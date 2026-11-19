// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
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
    public partial class ScreenSkinMixer : WizardScreen
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
                    Spacing = new Vector2(2),
                    Margin = new MarginPadding { Bottom = 20 },
                },
            };

            // The skin in use, when it's one of these.
            string current = skins.CurrentSkinInfo.Value.PerformRead(s => s.Name);
            baseSkin.Value = sources.FirstOrDefault(s => s.Name == current) ?? classic;

            baseSkin.BindValueChanged(b => name.Value = suggestName(b.NewValue), true);

            foreach (var part in parts)
                partsFlow.Add(new PartRow(this, part, 0));
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

        /// <summary>A part (or group) with its choice; a group opens to its parts.</summary>
        private partial class PartRow : CompositeDrawable
        {
            private const float row_height = 40;

            private readonly ScreenSkinMixer screen;
            private readonly SkinMixerOption part;
            private readonly int depth;

            private OsuDropdown<SkinMixerChoice> dropdown = null!;
            private SpriteIcon? chevron;
            private FillFlowContainer? children;
            private bool updating;

            public PartRow(ScreenSkinMixer screen, SkinMixerOption part, int depth)
            {
                this.screen = screen;
                this.part = part;
                this.depth = depth;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                bool isGroup = part is SkinMixerGroup;

                var header = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = row_height,
                    Padding = new MarginPadding { Left = depth * 20 },
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 5,
                            Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = isGroup ? colourProvider.Background3 : colourProvider.Background4,
                            },
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8),
                            Padding = new MarginPadding { Left = 12 },
                            Children = new Drawable[]
                            {
                                chevron = new SpriteIcon
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(10),
                                    Icon = FontAwesome.Solid.ChevronRight,
                                    Alpha = isGroup ? 1 : 0,
                                },
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = part.Name,
                                    Font = OsuFont.Default.With(size: 14, weight: isGroup ? FontWeight.Bold : FontWeight.Regular),
                                },
                            }
                        },
                        dropdown = new OsuDropdown<SkinMixerChoice>
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Width = 280,
                            Margin = new MarginPadding { Right = 6 },
                            Items = (isGroup ? new[] { SkinMixerChoice.MIXED } : Array.Empty<SkinMixerChoice>()).Concat(screen.choicesFor(part)).ToArray(),
                        },
                    }
                };

                if (isGroup)
                    header.Add(new GroupToggle(toggle) { RelativeSizeAxes = Axes.Both, Width = 0.5f, Depth = -1 });

                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(2),
                    Children = isGroup
                        ? new Drawable[]
                        {
                            header,
                            children = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(2),
                                Alpha = 0,
                            }
                        }
                        : new Drawable[] { header },
                };

                screen.rows.Add(this);
                Refresh();

                dropdown.Current.BindValueChanged(c =>
                {
                    // "Mixed" is only shown, never picked.
                    if (updating || c.NewValue.Type == SkinMixerChoice.Kind.Mixed)
                        return;

                    foreach (var leaf in part.Leaves)
                        leaf.Choice = leaf.CanBeBlank || c.NewValue.Type != SkinMixerChoice.Kind.Blank ? c.NewValue : SkinMixerChoice.DEFAULT;

                    screen.refreshRows();
                });
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
                        children.Add(new PartRow(screen, child, depth + 1));
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

            private partial class GroupToggle : OsuClickableContainer
            {
                public GroupToggle(Action action)
                {
                    Action = action;
                }

                protected override bool OnHover(HoverEvent e) => true;
            }
        }
    }
}

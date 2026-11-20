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
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Notifications;
using osu.Game.Skinning;
using osu.Game.Skinning.Mixer;
using osu.Game.Utils;
using SixLabors.ImageSharp;

namespace osu.Game.Overlays.SkinMixer
{
    /// <summary>
    /// Reprise: the skin modifier's one step (OsuSkinMixer's modifier, in the game): a skin, and what to change in it: instafade circles,
    /// a smooth cursor trail, a still interface, no 300s, its combo colours. Made as a modified copy; the skin itself stays as it is.
    /// </summary>
    [LocalisableDescription(typeof(ScreenSkinModifier), nameof(Title))]
    public partial class ScreenSkinModifier : WizardScreen, ISkinMakingScreen
    {
        public static LocalisableString Title => "Modify a skin";

        public override LocalisableString? NextStepText => "Create skin";

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private static readonly SkinMixerSource classic = new SkinMixerSource(@"osu! (classic)", Array.Empty<KeyValuePair<string, Func<byte[]>>>());

        private readonly Bindable<SkinMixerSource> skin = new Bindable<SkinMixerSource>(classic);
        private readonly Bindable<string> name = new Bindable<string>(string.Empty);

        private readonly BindableBool instafade = new BindableBool();
        private readonly BindableBool smoothTrail = new BindableBool();
        private readonly BindableBool stillInterface = new BindableBool();
        private readonly BindableBool hideGreats = new BindableBool();
        private readonly BindableBool ownColours = new BindableBool();

        private FormColourPalette palette = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            var sources = ScreenSkinMixer.ReadSkins(realm, storage);

            Content.Children = new Drawable[]
            {
                new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Colour = OverlayColourProvider.Content1,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = "Pick a skin and what to change in it. It's made as a modified copy, so the skin itself stays as it is.",
                },
                new FormDropdown<SkinMixerSource>
                {
                    Caption = "Skin",
                    AlwaysShowSearchBar = true,
                    Items = new[] { classic }.Concat(sources).ToArray(),
                    Current = skin,
                },
                new FormTextBox
                {
                    Caption = "New skin's name",
                    Current = name,
                },
                new FormCheckBox
                {
                    Caption = "Instafade hitcircles",
                    HintText = "Circles vanish the moment they're hit: the circle and its overlay are put into the numbers' images. All combos take one colour.",
                    Current = instafade,
                },
                new FormCheckBox
                {
                    Caption = "Smooth cursor trail",
                    HintText = "The trail drawn as one smooth line (the cursor becomes its middle, the trail cropped to what's in it).",
                    Current = smoothTrail,
                },
                new FormCheckBox
                {
                    Caption = "Still interface",
                    HintText = "Animated interface elements (buttons, the pause screen...) hold their middle frame.",
                    Current = stillInterface,
                },
                new FormCheckBox
                {
                    Caption = "Hide 300s",
                    HintText = "No 300s (or gekis and katus) shown when hitting.",
                    Current = hideGreats,
                },
                new FormCheckBox
                {
                    Caption = "Own combo colours",
                    HintText = "The colours below instead of the skin's.",
                    Current = ownColours,
                },
                palette = new FormColourPalette
                {
                    Caption = "Combo colours",
                    PaletteHeaderText = "Combo colours",
                },
            };

            string current = skins.CurrentSkinInfo.Value.PerformRead(s => s.Name);
            skin.Value = sources.FirstOrDefault(s => s.Name == current) ?? classic;

            skin.BindValueChanged(s =>
            {
                name.Value = suggestName(s.NewValue);

                // The skin's own colours to start from.
                palette.Colours.Clear();
                palette.Colours.AddRange(SkinModifying.ComboColoursOf(s.NewValue.Ini).Select(c =>
                {
                    var p = c.ToPixel<SixLabors.ImageSharp.PixelFormats.Rgba32>();
                    return new Colour4(p.R, p.G, p.B, 255);
                }));
            }, true);

            ownColours.BindValueChanged(o => palette.FadeTo(o.NewValue ? 1 : 0.5f, 200), true);
        }

        private string suggestName(SkinMixerSource from)
        {
            string[] existing = realm.Run(r => r.All<SkinInfo>().Where(s => !s.DeletePending).AsEnumerable().Select(s => s.Name).ToArray());
            return NamingUtils.GetNextBestName(existing, $@"{from.Name} (modified)");
        }

        /// <summary>Makes the modified copy (in the background) and imports it; <paramref name="onCreated"/> gets it (null if it failed).</summary>
        public void Create(Action<Live<SkinInfo>?> onCreated)
        {
            var from = skin.Value;
            string skinName = string.IsNullOrWhiteSpace(name.Value) ? suggestName(from) : name.Value.Trim();

            var modifications = new SkinModifications
            {
                Instafade = instafade.Value,
                SmoothTrail = smoothTrail.Value,
                StillInterface = stillInterface.Value,
                HideGreats = hideGreats.Value,
                ComboColours = ownColours.Value ? palette.Colours.Select(c => Color.FromRgb((byte)(c.R * 255), (byte)(c.G * 255), (byte)(c.B * 255))).ToArray() : null,
            };

            // The game's classic skin's images, for those the skin hasn't got.
            var classicImages = new NamespacedResourceStore<byte[]>(game.Resources, @"Skins/Legacy");

            Task.Run(async () =>
            {
                try
                {
                    // The skin as it is (renamed), then changed.
                    var files = SkinMixing.Mix(from, Array.Empty<SkinMixerOption>(), skinName);
                    SkinModifying.Modify(files, modifications, n => classicImages.Get(n));

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
    }
}

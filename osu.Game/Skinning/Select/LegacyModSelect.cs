// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Skinning.Select
{
    /// <summary>
    /// The mods of osu!stable's mod selection, for its dialog: the score multiplier, then "Difficulty Reduction", "Difficulty Increase"
    /// and "Special" with their mods' icons (the skin's "selection-mod-*"). A slot with more than one mod (DT and NC) goes through them
    /// on each click (backwards with the right button). Only the current mode's mods are there.
    /// </summary>
    /// <remarks>
    /// Stable's positions (its 640x480, x1.6 here): the rows 60 apart, the icons 85 apart from 240 across, the rows' names at 20.
    /// </remarks>
    public partial class LegacyModSelect : CompositeDrawable
    {
        /// <summary>
        /// The first row's height in the layout (its icons' middle).
        /// </summary>
        public const float FIRST_ROW = 137.5f * 1.6f;

        /// <summary>
        /// Where the dialog's buttons start, under the rows.
        /// </summary>
        public const float BUTTONS_TOP = (137.5f + 60 * 3) * 1.6f;

        private const float row_spacing = 60 * 1.6f;
        private const float icon_x = 240 * 1.6f;
        private const float icon_spacing = 85 * 1.6f;

        // Stable's slots, by acronym (a slot's mods in the order clicks go through them).
        private static readonly (string Name, Color4 Colour, string[][] Slots)[] rows =
        {
            ("Difficulty Reduction", new Color4(50, 205, 50, 255), new[] { new[] { "EZ" }, new[] { "NF" }, new[] { "HT" } }),
            ("Difficulty Increase", new Color4(255, 69, 0, 255), new[] { new[] { "HR" }, new[] { "SD", "PF" }, new[] { "DT", "NC" }, new[] { "FI", "HD" }, new[] { "FL" } }),
            ("Special", Color4.White, new[]
            {
                new[] { "4K", "5K", "6K", "7K", "8K", "9K", "1K", "2K", "3K" }, new[] { "DS" }, new[] { "RD" },
                new[] { "RX" }, new[] { "AP" }, new[] { "SO" }, new[] { "AT", "CN" },
            }),
        };

        // Stable's names for the icons ("selection-mod-<name>").
        private static readonly Dictionary<string, string> texture_names = new Dictionary<string, string>
        {
            ["EZ"] = "easy", ["NF"] = "nofail", ["HT"] = "halftime", ["HR"] = "hardrock", ["SD"] = "suddendeath", ["PF"] = "perfect",
            ["DT"] = "doubletime", ["NC"] = "nightcore", ["HD"] = "hidden", ["FI"] = "fadein", ["FL"] = "flashlight", ["RX"] = "relax",
            ["AP"] = "relax2", ["SO"] = "spunout", ["AT"] = "autoplay", ["CN"] = "cinema", ["DS"] = "keycoop", ["RD"] = "random",
            ["1K"] = "key1", ["2K"] = "key2", ["3K"] = "key3", ["4K"] = "key4", ["5K"] = "key5", ["6K"] = "key6", ["7K"] = "key7", ["8K"] = "key8",
            ["9K"] = "key9",
        };

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        private OsuSpriteText multiplier = null!;

        public LegacyModSelect()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            // The mode's mods, one per acronym (some come grouped).
            var available = ModUtils.FlattenMods(ruleset.Value.CreateInstance().CreateAllMods())
                                    .Where(m => !string.IsNullOrEmpty(m.Acronym))
                                    .GroupBy(m => m.Acronym)
                                    .ToDictionary(g => g.Key, g => g.First());

            AddInternal(multiplier = new OsuSpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                // Measured against stable at 1920x1080.
                Y = 130,
                Font = OsuFont.GetFont(size: 48, weight: FontWeight.Bold),
            });

            for (int row = 0; row < rows.Length; row++)
            {
                float y = FIRST_ROW + row_spacing * row;

                AddInternal(new OsuSpriteText
                {
                    Position = new Vector2(20 * 1.6f, y - 13 * 1.6f),
                    Text = rows[row].Name,
                    Font = OsuFont.GetFont(size: 38, weight: FontWeight.Bold),
                    Colour = rows[row].Colour,
                });

                int slotIndex = 0;

                foreach (string[] slot in rows[row].Slots)
                {
                    var mods = slot.Where(available.ContainsKey).Select(a => available[a]).ToArray();

                    if (mods.Length == 0)
                        continue;

                    AddInternal(new Slot(mods, skin, select)
                    {
                        Position = new Vector2(icon_x + icon_spacing * slotIndex, y),
                    });

                    slotIndex++;
                }
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatmap.BindValueChanged(_ => showMultiplier());
            ruleset.BindValueChanged(_ => showMultiplier());
            selectedMods.BindValueChanged(mods =>
            {
                showMultiplier();

                foreach (var slot in InternalChildren.OfType<Slot>())
                    slot.Current = slot.Mods.FirstOrDefault(m => mods.NewValue.Any(s => s.Acronym == m.Acronym));
            }, true);
        }

        /// <summary>
        /// Turns a slot's mods off and <paramref name="mod"/> on (none: just off), dropping the selected mods it can't go with.
        /// </summary>
        private void select(IReadOnlyList<Mod> slotMods, Mod? mod)
        {
            var mods = selectedMods.Value.Where(m => slotMods.All(s => s.Acronym != m.Acronym)).ToList();

            if (mod != null)
            {
                var added = mod.DeepClone();
                mods.RemoveAll(m => m.IncompatibleMods.Any(t => t.IsInstanceOfType(added)) || added.IncompatibleMods.Any(t => t.IsInstanceOfType(m)));
                mods.Add(added);
            }

            selectedMods.Value = mods;
        }

        private void showMultiplier() =>
            multiplier.Text = ScoreMultiplierText(ruleset.Value, beatmap.Value.BeatmapInfo, selectedMods.Value);

        /// <summary>
        /// The mods' score multiplier on <paramref name="beatmapInfo"/>, as the game works it out (its mod selection's): mods' own
        /// <c>ScoreMultiplier</c> is no longer used and always 1.
        /// </summary>
        public static double ScoreMultiplierFor(RulesetInfo? rulesetInfo, BeatmapInfo beatmapInfo, IReadOnlyList<Mod> mods)
        {
            var calculator = (rulesetInfo ?? beatmapInfo.Ruleset).CreateInstance().CreateScoreMultiplierCalculator(new ScoreMultiplierContext(beatmapInfo.Difficulty));
            return calculator.CalculateFor(mods);
        }

        /// <summary>
        /// "Score Multiplier: 1.23x", the number as the game's mod selection shows it (rounded away from 1.00x).
        /// </summary>
        public static string ScoreMultiplierText(RulesetInfo? rulesetInfo, BeatmapInfo beatmapInfo, IReadOnlyList<Mod> mods)
        {
            double value = ScoreMultiplierFor(rulesetInfo, beatmapInfo, mods);
            value = value > 1 ? Math.Ceiling(Math.Round(value * 100, 4)) / 100 : Math.Floor(Math.Round(value * 100, 4)) / 100;
            return string.Create(CultureInfo.InvariantCulture, $"Score Multiplier: {value:0.00}x");
        }

        public static void Reset(Bindable<IReadOnlyList<Mod>> mods) => mods.Value = Array.Empty<Mod>();

        /// <summary>
        /// One of stable's mod slots: its mod's icon, bigger, tilted and glowing while on.
        /// </summary>
        public partial class Slot : CompositeDrawable
        {
            public readonly IReadOnlyList<Mod> Mods;

            private readonly Action<IReadOnlyList<Mod>, Mod?> select;
            private readonly Drawable[] icons;
            private readonly Sprite glow;
            private readonly SkinnableSound hoverSound;
            private readonly SkinnableSound onSound;
            private readonly SkinnableSound offSound;

            private Mod? current;

            public Slot(IReadOnlyList<Mod> mods, ISkinSource skin, Action<IReadOnlyList<Mod>, Mod?> select)
            {
                Mods = mods;
                this.select = select;

                Origin = Anchor.Centre;
                Size = new Vector2(64);

                icons = mods.Select(m => icon(m, skin)).ToArray();

                glow = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Texture = skin.GetTexture(@"lighting"),
                    Colour = new Color4(135, 206, 235, 255),
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                };

                hoverSound = new SkinnableSound(new SampleInfo("click-short"));
                onSound = new SkinnableSound(new SampleInfo("check-on"));
                offSound = new SkinnableSound(new SampleInfo("check-off"));

                // The glow behind the icons.
                InternalChildren = new Drawable[] { glow }.Concat(icons).Concat(new Drawable[] { hoverSound, onSound, offSound }).ToArray();

                updateIcons(false);
            }

            private static Drawable icon(Mod mod, ISkinSource skin)
            {
                Texture? texture = texture_names.TryGetValue(mod.Acronym, out string? name) ? skin.GetTexture($"selection-mod-{name}") : null;

                Drawable drawable = texture != null
                    ? new Sprite { Texture = texture }
                    // Without the skin's: the game's own icon.
                    : new ModIcon(mod, showTooltip: false) { Scale = new Vector2(0.7f) };

                drawable.Anchor = Anchor.Centre;
                drawable.Origin = Anchor.Centre;
                return drawable;
            }

            public Mod? Current
            {
                get => current;
                set
                {
                    if (current?.Acronym == value?.Acronym)
                        return;

                    current = value;
                    updateIcons(IsLoaded);
                }
            }

            // Stable's: the current mod's icon (the first when off), at 1.2 and turned a little while on.
            private void updateIcons(bool animated)
            {
                int shown = current == null ? 0 : Math.Max(0, Mods.ToList().FindIndex(m => m.Acronym == current.Acronym));
                double duration = animated ? 400 : 0;

                for (int i = 0; i < icons.Length; i++)
                {
                    icons[i].FadeTo(i == shown ? 1 : 0, animated ? 100 : 0);
                    icons[i].ScaleTo(current != null && i == shown ? 1.2f : 1, duration, Easing.OutElastic);
                    icons[i].RotateTo(current != null && i == shown ? 0.1f * 180 / MathF.PI : 0, duration, Easing.OutElastic);
                }

                glow.FadeTo(current != null ? 0.2f : 0, animated ? 100 : 0);
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSound.Play();
                return true;
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                if (e.Button == MouseButton.Right)
                {
                    cycle(backwards: true);
                    return true;
                }

                return base.OnMouseDown(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                cycle(backwards: false);
                return true;
            }

            // Stable's: off -> the first (the last, backwards) -> ... -> off.
            private void cycle(bool backwards)
            {
                int index = current == null ? -1 : Mods.ToList().FindIndex(m => m.Acronym == current.Acronym);
                int next = backwards
                    ? (index == -1 ? Mods.Count - 1 : index - 1)
                    : (index == -1 ? 0 : index + 1);

                Mod? mod = next >= 0 && next < Mods.Count ? Mods[next] : null;

                (mod != null ? onSound : offSound).Play();
                select(Mods, mod);
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;

namespace osu.Game.Overlays.Toolbar
{
    /// <summary>
    /// Reprise: the mode's variant next to the rulesets: vanilla, Relax or Autopilot (reprise.pw's osu!rx, osu!ap,
    /// taiko!rx and catch!rx). Picking Relax or Autopilot turns that mod on, which is playing in that mode: its
    /// leaderboards and the player's rank and pp in it. It follows the mod select too.
    /// </summary>
    public partial class ToolbarModeVariantSelector : FillFlowContainer
    {
        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

        private readonly VariantButton vanilla;
        private readonly VariantButton relax;
        private readonly VariantButton autopilot;

        private readonly Dictionary<string, bool> hasAutopilot = new Dictionary<string, bool>();

        public ToolbarModeVariantSelector()
        {
            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;
            Direction = FillDirection.Horizontal;

            Children = new[]
            {
                vanilla = new VariantButton("VN", "Vanilla", "Play without Relax or Autopilot", () => setVariant(null)),
                relax = new VariantButton("RX", "Relax", "Play on the Relax leaderboards", () => setVariant("RX")),
                autopilot = new VariantButton("AP", "Autopilot", "Play on the Autopilot leaderboards", () => setVariant("AP")),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => requestUpdate());
            selectedMods.BindValueChanged(_ => requestUpdate());
            selectedMods.BindDisabledChanged(_ => requestUpdate());
            updateState();
        }

        /// <summary>
        /// At once on the update thread. Off it (a screen leases the mods, disabling them, while it loads: multiplayer, playlists), fading
        /// would crash the game, so it's scheduled there. Not always scheduled: while hidden (a ruleset without Relax/Autopilot) this
        /// isn't updated, so its scheduler doesn't run, and switching back to such a ruleset never showed it again.
        /// </summary>
        private void requestUpdate()
        {
            if (ThreadSafety.IsUpdateThread)
                updateState();
            else
                Scheduler.AddOnce(updateState);
        }

        private void updateState()
        {
            var current = ruleset.Value;
            bool available = current != null && current.HasSpecialRuleset();

            Alpha = available ? 1 : 0;
            if (!available)
                return;

            string? variant = currentVariant();
            // Asked once per ruleset (it makes all of the ruleset's mods), not on every mod change.
            if (!hasAutopilot.TryGetValue(current!.ShortName, out bool autopilotAvailable))
                hasAutopilot[current.ShortName] = autopilotAvailable = createMod(current, "AP") != null;

            vanilla.Active = variant == null;
            relax.Active = variant == "RX";
            autopilot.Active = variant == "AP";
            autopilot.Alpha = autopilotAvailable ? 1 : 0;

            this.FadeColour(selectedMods.Disabled ? Colour4.Gray : Colour4.White, 300);
        }

        private string? currentVariant()
        {
            var acronyms = selectedMods.Value.Select(m => m.Acronym).ToArray();
            return acronyms.Contains("RX") ? "RX" : acronyms.Contains("AP") ? "AP" : null;
        }

        private void setVariant(string? acronym)
        {
            if (selectedMods.Disabled || ruleset.Value == null || acronym == currentVariant())
                return;

            var mods = selectedMods.Value.Where(m => m.Acronym != "RX" && m.Acronym != "AP").ToList();

            if (acronym != null && createMod(ruleset.Value, acronym) is Mod variant)
            {
                // What can't be played with it goes (Autoplay with Relax, Spun Out with Autopilot, ...).
                mods.RemoveAll(m => !ModUtils.CheckCompatibleSet(new[] { m, variant }));
                mods.Add(variant);
            }

            selectedMods.Value = mods;

            // As the mod select's toggles sound: on for Relax/Autopilot, off back to vanilla.
            (acronym != null ? sampleOn : sampleOff)?.Play();
        }

        private Sample? sampleOn;
        private Sample? sampleOff;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            sampleOn = audio.Samples.Get(@"UI/check-on");
            sampleOff = audio.Samples.Get(@"UI/check-off");
        }

        private static Mod? createMod(RulesetInfo ruleset, string acronym) =>
            ruleset.CreateInstance().CreateAllMods().FirstOrDefault(m => m.Acronym == acronym);

        private partial class VariantButton : ToolbarButton
        {
            private readonly Action select;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            public VariantButton(string label, string name, string description, Action select)
            {
                this.select = select;

                RelativeSizeAxes = Axes.Y;
                AutoSizeAxes = Axes.X;
                ButtonContent.Padding = new MarginPadding(PADDING) { Bottom = 5 };

                SetIcon(new OsuSpriteText { Text = label, Font = OsuFont.Torus.With(size: 15, weight: FontWeight.Bold) });
                TooltipMain = name;
                TooltipSub = description;
            }

            protected override HoverSounds CreateHoverSounds(HoverSampleSet sampleSet) => new HoverSounds();

            public bool Active
            {
                set => Scheduler.AddOnce(() =>
                {
                    IconContainer.Colour = value ? Color4Extensions.FromHex("#00FFAA") : colours.GrayF;
                });
            }

            protected override bool OnClick(ClickEvent e)
            {
                select();
                return base.OnClick(e);
            }
        }
    }
}

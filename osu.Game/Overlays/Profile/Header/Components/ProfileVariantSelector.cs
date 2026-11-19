// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.Overlays.Profile.Header.Components
{
    /// <summary>
    /// Reprise: Vanilla, Relax and Autopilot as the website's pills beside the profile's ruleset icons: the shown ruleset's own modes
    /// (Relax for osu!, taiko and catch, Autopilot for osu! only; none for mania), the one shown lit.
    /// </summary>
    public partial class ProfileVariantSelector : CompositeDrawable
    {
        public enum Variant
        {
            Vanilla,
            Relax,
            Autopilot,
        }

        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

        [Resolved]
        private UserProfileOverlay? profileOverlay { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        private Pill vanilla = null!;
        private Pill relax = null!;
        private Pill autopilot = null!;

        public ProfileVariantSelector()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(4, 0),
                Children = new Drawable[]
                {
                    vanilla = new Pill("Vanilla") { Action = () => select(Variant.Vanilla) },
                    relax = new Pill("Relax") { Action = () => select(Variant.Relax) },
                    autopilot = new Pill("Autopilot") { Action = () => select(Variant.Autopilot) },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            User.BindValueChanged(_ => updateState(), true);
        }

        /// <summary>A shown ruleset's base ruleset ("osu" for "osurx") and its mode.</summary>
        public static (string BaseName, Variant Variant) Split(string shortName)
        {
            if (shortName.EndsWith(@"rx", System.StringComparison.Ordinal) && shortName.Length > 2)
                return (shortName[..^2], Variant.Relax);
            if (shortName.EndsWith(@"ap", System.StringComparison.Ordinal) && shortName.Length > 2)
                return (shortName[..^2], Variant.Autopilot);

            return (shortName, Variant.Vanilla);
        }

        /// <summary>The ruleset to show for a base ruleset in a mode, or null where it has none (Autopilot outside osu!, mania's Relax).</summary>
        public static RulesetInfo? RulesetFor(RulesetStore rulesets, string baseName, Variant variant)
        {
            var baseRuleset = rulesets.GetRuleset(baseName);

            if (baseRuleset == null)
                return null;

            return (variant, baseName) switch
            {
                (Variant.Vanilla, _) => baseRuleset,
                (Variant.Relax, RulesetInfo.OSU_MODE_SHORTNAME) => baseRuleset.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID),
                (Variant.Relax, RulesetInfo.TAIKO_MODE_SHORTNAME) => baseRuleset.CreateSpecialRuleset(RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME, RulesetInfo.TAIKO_RELAX_ONLINE_ID),
                (Variant.Relax, RulesetInfo.CATCH_MODE_SHORTNAME) => baseRuleset.CreateSpecialRuleset(RulesetInfo.CATCH_RELAX_MODE_SHORTNAME, RulesetInfo.CATCH_RELAX_ONLINE_ID),
                (Variant.Autopilot, RulesetInfo.OSU_MODE_SHORTNAME) => baseRuleset.CreateSpecialRuleset(RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME, RulesetInfo.OSU_AUTOPILOT_ONLINE_ID),
                _ => null,
            };
        }

        private (string BaseName, Variant Variant)? shown => User.Value?.Ruleset is RulesetInfo ruleset ? Split(ruleset.ShortName) : null;

        private void updateState()
        {
            if (shown is not var (baseName, variant))
            {
                Alpha = 0;
                return;
            }

            bool hasRelax = RulesetFor(rulesets, baseName, Variant.Relax) != null;
            bool hasAutopilot = RulesetFor(rulesets, baseName, Variant.Autopilot) != null;

            // Mania has neither: no pills at all.
            Alpha = hasRelax || hasAutopilot ? 1 : 0;
            relax.Alpha = hasRelax ? 1 : 0;
            autopilot.Alpha = hasAutopilot ? 1 : 0;

            vanilla.Active = variant == Variant.Vanilla;
            relax.Active = variant == Variant.Relax;
            autopilot.Active = variant == Variant.Autopilot;
        }

        private void select(Variant variant)
        {
            if (User.Value == null || shown is not var (baseName, current) || current == variant)
                return;

            if (RulesetFor(rulesets, baseName, variant) is RulesetInfo target)
                profileOverlay?.ShowUser(User.Value.User, target);
        }

        private partial class Pill : OsuClickableContainer
        {
            private readonly string text;

            private Box background = null!;
            private OsuSpriteText label = null!;
            private bool active;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public bool Active
            {
                set
                {
                    active = value;

                    if (IsLoaded)
                        updateState();
                }
            }

            public Pill(string text)
            {
                this.text = text;

                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 11;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    label = new OsuSpriteText
                    {
                        Text = text,
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                        Margin = new MarginPadding { Horizontal = 12, Vertical = 4 },
                    },
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
                updateState();
                base.OnHoverLost(e);
            }

            private void updateState()
            {
                background.FadeColour(active ? colourProvider.Highlight1 : colourProvider.Background6, 150, Easing.OutQuint);
                background.FadeTo(active || IsHovered ? 1 : 0, 150, Easing.OutQuint);
                label.Colour = active ? colourProvider.Background6 : colourProvider.Content1;
            }
        }
    }
}

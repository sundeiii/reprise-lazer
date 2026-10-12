// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Select
{
    /// <summary>
    /// osu!stable's mode list, opened by song select's mode button: the modes stacked up from the button, each the skin's
    /// "mode-*-med" (or the ruleset's icon) and its name. Clicking one changes to it; clicking elsewhere (or back) closes it.
    /// </summary>
    /// <remarks>
    /// In stable's 1024x768 layout: rows 80 high (its 640x480) from 140,344 upwards.
    /// </remarks>
    public partial class LegacyModeMenu : VisibilityContainer, IKeyBindingHandler<GlobalAction>
    {
        private const float row_size = 80 * 1.6f;
        private static readonly Vector2 first_row = new Vector2(140, 344) * 1.6f;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        private SkinnableSound expandSound = null!;

        protected override bool StartHidden => true;

        // Open: it takes the clicks (outside a row, to close).
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => State.Value == Visibility.Visible;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            // Like stable's: all the icons, then all the names over them (skins draw the whole menu in one icon: XooMoon's
            // "mode-fruits-med" is a panel with all four, the others blank), then the rows to click.
            var icons = new Container { RelativeSizeAxes = Axes.Both };
            var names = new Container { RelativeSizeAxes = Axes.Both };
            var rows = new Container { RelativeSizeAxes = Axes.Both };

            InternalChildren = new Drawable[]
            {
                icons,
                names,
                rows,
                expandSound = new SkinnableSound(new SampleInfo("select-expand")),
            };

            int index = 0;

            // Stable's four modes (other installed rulesets, like add-ons, aren't modes to pick here).
            foreach (var r in rulesets.AvailableRulesets.Where(r => r.IsLegacyRuleset()).OrderBy(r => r.OnlineID))
            {
                var position = first_row - new Vector2(0, row_size * index);
                var texture = skin.GetTexture($"mode-{r.ShortName}-med");

                Drawable icon = texture != null
                    ? new Sprite { Texture = texture }
                    // (Fitted into a box: a ruleset's icon may size itself.)
                    : new ConstrainedIconContainer { Size = new Vector2(64), Icon = r.CreateInstance().CreateIcon() };

                icon.Origin = Anchor.Centre;
                icon.Position = position + new Vector2(row_size / 2);
                icons.Add(icon);

                var name = new OsuSpriteText
                {
                    Origin = Anchor.CentreLeft,
                    Position = position + new Vector2(row_size + 16, row_size / 2),
                    Text = r.Name,
                    Font = OsuFont.GetFont(size: 38.4f),
                };
                names.Add(name);

                rows.Add(new Row(icon, name)
                {
                    Position = position,
                    Action = () => select(r),
                });

                index++;
            }
        }

        private void select(RulesetInfo r)
        {
            if (!ruleset.Disabled)
                ruleset.Value = r;

            Hide();
        }

        protected override void PopIn()
        {
            expandSound.Play();
            this.FadeIn(200, Easing.OutQuint);
        }

        protected override void PopOut() => this.FadeOut(200, Easing.OutQuint);

        protected override bool OnClick(ClickEvent e)
        {
            Hide();
            return true;
        }

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action != GlobalAction.Back || State.Value != Visibility.Visible)
                return false;

            Hide();
            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        /// <summary>
        /// One mode's row to click: its icon (in the icons' layer) grows on hover and its name (in the names') moves right.
        /// </summary>
        private partial class Row : ClickableContainer
        {
            private readonly Drawable icon;
            private readonly Drawable name;
            private readonly float nameX;
            private readonly SkinnableSound hoverSound;
            private readonly SkinnableSound clickSound;

            public Row(Drawable icon, Drawable name)
            {
                this.icon = icon;
                this.name = name;
                nameX = name.X;

                Size = new Vector2(row_size * 3.5f, row_size);

                Children = new Drawable[]
                {
                    hoverSound = new SkinnableSound(new SampleInfo("click-short")),
                    clickSound = new SkinnableSound(new SampleInfo("click-short-confirm")),
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverSound.Play();
                icon.ScaleTo(1.1f, 1000, Easing.OutElastic);
                name.MoveToX(nameX + 16, 300, Easing.OutQuint);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                icon.ScaleTo(1, 1000, Easing.OutElastic);
                name.MoveToX(nameX, 300, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                clickSound.Play();
                return base.OnClick(e);
            }
        }
    }
}

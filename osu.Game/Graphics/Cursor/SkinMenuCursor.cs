// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// The skin's cursor ("cursor" and "cursormiddle") for menus, as osu!stable shows it everywhere: centred unless the skin says
    /// CursorCentre: 0, spinning with CursorRotate, larger while pressed with CursorExpand.
    /// </summary>
    public partial class SkinMenuCursor : CompositeDrawable
    {
        /// <summary>
        /// The skin's settings for its cursor (its skin.ini's [General]).
        /// </summary>
        public enum Setting
        {
            CursorCentre,
            CursorRotate,
            CursorExpand,
        }

        private const double revolution_duration = 10000;
        private const float pressed_scale = 1.3f;

        /// <summary>
        /// Whether it's shown: the setting is on and the skin has a cursor.
        /// </summary>
        public readonly BindableBool Active = new BindableBool();

        /// <summary>
        /// The skin's trail ("cursortrail"), for <see cref="SkinMenuCursorTrail"/>.
        /// </summary>
        public Texture? TrailTexture { get; private set; }

        /// <summary>
        /// The trail's size as the skin gives it. Its texture is the one gameplay uses too, which makes it 1.6x smaller for the playfield's
        /// scale; drawn at this size, the menus' trail isn't.
        /// </summary>
        public Vector2 TrailSize { get; private set; }

        /// <summary>
        /// Whether the skin's trail is a line (with a "cursormiddle", stable's continuous trail) rather than separate images.
        /// </summary>
        public bool ContinuousTrail { get; private set; }

        public bool Centred { get; private set; } = true;

        private bool expand = true;

        // Not everywhere a menu cursor is shown has skins (test scenes, ...): then it's the game's own cursor.
        [Resolved(CanBeNull = true)]
        private ISkinSource? skin { get; set; }

        private Bindable<bool> enabled = null!;
        private Container expandTarget = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            enabled = config.GetBindable<bool>(OsuSetting.SkinCursorInMenus);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            enabled.BindValueChanged(_ => reload(), true);
            if (skin != null)
                skin.SourceChanged += onSkinChanged;
        }

        private void onSkinChanged() => Schedule(reload);

        // Straight from the old cursor to the new one (switching off in between showed the game's own cursor for a frame).
        private void reload()
        {
            var cursor = enabled.Value ? skin?.GetTexture(@"cursor") : null;

            ClearInternal();
            TrailTexture = null;

            if (skin == null || cursor == null)
            {
                Active.Value = false;
                return;
            }

            // Stable takes the cursor's settings and middle from the skin the cursor itself is from.
            var provider = skin.FindProvider(s => s.GetTexture(@"cursor") != null) ?? skin;

            Centred = provider.GetConfig<Setting, bool>(Setting.CursorCentre)?.Value ?? true;
            bool spin = provider.GetConfig<Setting, bool>(Setting.CursorRotate)?.Value ?? true;
            expand = provider.GetConfig<Setting, bool>(Setting.CursorExpand)?.Value ?? true;

            var middle = provider.GetTexture(@"cursormiddle");
            ContinuousTrail = middle != null;

            TrailTexture = skin.GetTexture(@"cursortrail");
            TrailSize = TrailTexture?.DisplaySize ?? Vector2.Zero;

            var origin = Centred ? Anchor.Centre : Anchor.TopLeft;

            // Sizeless at the mouse, so the image's own origin puts it there (sized around it, it sat half its size up and left)
            // and it grows and spins about the mouse.
            AddInternal(expandTarget = new Container
            {
                // At the sizes the skin gives (gameplay changes these textures' scale for its playfield).
                Child = new Sprite
                {
                    Texture = cursor,
                    Size = cursor.DisplaySize,
                    Origin = origin,
                },
            });

            if (middle != null)
                AddInternal(new Sprite { Texture = middle, Size = middle.DisplaySize, Origin = origin });

            if (spin)
                expandTarget.Spin(revolution_duration, RotationDirection.Clockwise);

            Active.Value = true;
        }

        /// <summary>
        /// Pressed: larger (CursorExpand).
        /// </summary>
        public void Expand()
        {
            if (expand && expandTarget != null)
                expandTarget.ScaleTo(1).ScaleTo(pressed_scale, 100, Easing.Out);
        }

        public void Contract() => expandTarget?.ScaleTo(1, 100, Easing.Out);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;
        }
    }

    /// <summary>
    /// The skin's cursor trail behind <see cref="SkinMenuCursor"/>: separate images every frame fading in 150ms, or (with a cursormiddle) a line
    /// of them along the path, added and fading in 500ms, as osu!stable's.
    /// </summary>
    public partial class SkinMenuCursorTrail : Container
    {
        private const double disjoint_separation = 1000 / 60.0;

        private SkinMenuCursor? cursor;
        private Vector2? last;
        private double lastTime;

        public void Follow(SkinMenuCursor? skinCursor) => cursor = skinCursor;

        /// <summary>
        /// The menu cursor was hidden (gameplay's cursor took over): no trail until it's back.
        /// </summary>
        public void Stop()
        {
            if (last == null && Children.Count == 0)
                return;

            last = null;
            Clear();
        }

        /// <summary>
        /// The cursor moved to <paramref name="position"/> (in this trail's space).
        /// </summary>
        public void MoveTo(Vector2 position)
        {
            if (cursor?.Active.Value != true || cursor.TrailTexture is not Texture texture)
            {
                last = null;
                return;
            }

            if (!cursor.ContinuousTrail)
            {
                if (Time.Current - lastTime >= disjoint_separation && last != position)
                {
                    lastTime = Time.Current;
                    add(texture, cursor.TrailSize, position, cursor.Centred ? Anchor.Centre : Anchor.TopLeft, 150, false);
                }

                last = position;
                return;
            }

            // A line: images every texture width / 2.5 from where it was.
            last ??= position;
            float interval = cursor.TrailSize.X / 2.5f;
            var from = last.Value;
            float distance = Vector2.Distance(from, position);

            if (interval <= 0)
                return;

            int count = 0;

            while (distance >= interval && count++ < 200)
            {
                from += (position - from) / distance * interval;
                distance -= interval;
                add(texture, cursor.TrailSize, from, Anchor.Centre, 500, true);
            }

            last = from;
        }

        private void add(Texture texture, Vector2 size, Vector2 position, Anchor origin, double fade, bool additive)
        {
            var sprite = new Sprite
            {
                Texture = texture,
                Size = size,
                Origin = origin,
                Position = position,
                Blending = additive ? BlendingParameters.Additive : BlendingParameters.Inherit,
            };

            Add(sprite);
            sprite.FadeOut(fade).Expire();
        }
    }
}

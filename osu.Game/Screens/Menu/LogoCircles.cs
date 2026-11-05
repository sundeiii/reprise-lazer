// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// Reprise: the main menu logo's rings, in place of osu!'s triangles: outlined circles drifting up through it, as the triangles
    /// did (and as fast: <see cref="Velocity"/> is driven by the beat the same way).
    /// </summary>
    public partial class LogoCircles : CompositeDrawable
    {
        private const float base_velocity = 50;
        private const int ring_count = 22;

        /// <summary>
        /// How fast the rings rise (1 is resting speed).
        /// </summary>
        public float Velocity = 1;

        /// <summary>
        /// The rings' outline, relative to this drawable's width.
        /// </summary>
        public float Thickness { get; set; } = 0.009f;

        private readonly Random random = new Random();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            for (int i = 0; i < ring_count; i++)
            {
                var ring = new Ring();
                respawn(ring);
                // Spread over the whole logo at first, not all waiting below it.
                ring.Y = (float)random.NextDouble() * (1 + ring.Height) - ring.Height / 2;
                AddInternal(ring);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (DrawHeight <= 0)
                return;

            float moved = (float)(-Time.Elapsed / 1000 * Velocity * base_velocity / DrawHeight);
            float thickness = Math.Max(1, DrawWidth * Thickness);

            foreach (var ring in InternalChildren.OfType<Ring>())
            {
                ring.BorderThickness = thickness;
                ring.Y += moved * ring.Speed;

                if (ring.Y < -ring.Height / 2)
                    respawn(ring);
            }
        }

        private void respawn(Ring ring)
        {
            // Bigger rings rise slower, as if further away.
            float size = 0.06f + (float)Math.Pow(random.NextDouble(), 2) * 0.3f;

            ring.Size = new Vector2(size);
            ring.Speed = 1.4f - size * 2;
            ring.X = (float)random.NextDouble();
            ring.Y = 1 + size / 2;
            ring.Alpha = 0.5f + (float)random.NextDouble() * 0.5f;
        }

        private partial class Ring : CircularContainer
        {
            public float Speed;

            public Ring()
            {
                Anchor = Anchor.TopLeft;
                Origin = Anchor.Centre;
                RelativePositionAxes = Axes.Both;
                RelativeSizeAxes = Axes.Both;
                Masking = true;
                BorderColour = Color4.White;
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    AlwaysPresent = true,
                };
            }
        }
    }
}

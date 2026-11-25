// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Play.HUD.Counters
{
    /// <summary>
    /// Reprise: a filled line chart as BTMC's overlay draws its difficulty graph with Chart.js, upside down (hanging from its top edge):
    /// a monotone curve through the values, scaled to Chart.js's rounded maximum, filled from its base with the overlay's gradient
    /// (a tenth of the fill's opacity at the base, all of it two fifths of the way out), and a line along it if <see cref="LineColour"/> is set.
    /// </summary>
    public partial class StrainChart : Drawable
    {
        private double[] values = Array.Empty<double>();

        /// <summary>The chart's values, evenly over its width.</summary>
        public double[] Values
        {
            get => values;
            set
            {
                values = value;
                columnsValid = false;
            }
        }

        /// <summary>The fill's colour (its opacity is the most it reaches).</summary>
        public Color4 FillColour { get; set; } = new Color4(42, 130, 112, 128);

        /// <summary>The line along the curve, if any.</summary>
        public Color4? LineColour { get; set; }

        public float LineWidth { get; set; } = 1.5f;

        private float[] columns = Array.Empty<float>();
        private bool columnsValid;
        private float lastWidth;

        private Texture texture = null!;
        private IShader shader = null!;

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer, ShaderManager shaders)
        {
            texture = renderer.WhitePixel;
            shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
        }

        protected override void Update()
        {
            base.Update();

            if (columnsValid && lastWidth == DrawWidth)
                return;

            columns = computeColumns(values, (int)Math.Round(DrawWidth), DrawHeight);
            lastWidth = DrawWidth;
            columnsValid = true;
            Invalidate(Invalidation.DrawNode);
        }

        /// <summary>The curve's depth at every pixel column (its middle).</summary>
        private static float[] computeColumns(double[] data, int width, float height)
        {
            if (data.Length < 2 || width <= 0)
                return Array.Empty<float>();

            double max = NiceMaximum(data.Max());
            if (max <= 0)
                return Array.Empty<float>();

            int n = data.Length;
            double spacing = (double)width / (n - 1);
            double[] tangents = monotoneTangents(data, spacing);
            float[] result = new float[width];

            for (int c = 0; c < width; c++)
            {
                double x = c + 0.5;
                int i = Math.Clamp((int)(x / spacing), 0, n - 2);
                double t = (x - i * spacing) / spacing;

                // Cubic Hermite, as Chart.js's monotone curve.
                double t2 = t * t, t3 = t2 * t;
                double v = (2 * t3 - 3 * t2 + 1) * data[i] + (t3 - 2 * t2 + t) * spacing * tangents[i]
                                                          + (-2 * t3 + 3 * t2) * data[i + 1] + (t3 - t2) * spacing * tangents[i + 1];

                result[c] = (float)Math.Clamp(v / max * height, 0, height);
            }

            return result;
        }

        /// <summary>
        /// Chart.js's linear scale top: at most five ticks over a 64px chart, the step a "nice" number (1, 2, 5 or 10 times a power of ten),
        /// and the top the next step at or over the highest value.
        /// </summary>
        public static double NiceMaximum(double highest, int maxTicks = 5)
        {
            if (highest <= 0)
                return 0;

            double step = niceNumber(highest / (maxTicks - 1));
            return Math.Ceiling(highest / step - 1e-9) * step;
        }

        private static double niceNumber(double range)
        {
            double rounded = Math.Round(range);
            if (Math.Abs(range - rounded) <= range / 1000)
                range = rounded;

            double power = Math.Pow(10, Math.Floor(Math.Log10(range)));
            double fraction = range / power;
            double nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
            return nice * power;
        }

        // Chart.js's splineCurveMonotone (Fritsch–Carlson).
        private static double[] monotoneTangents(double[] y, double spacing)
        {
            int n = y.Length;
            double[] delta = new double[n - 1];
            double[] m = new double[n];

            for (int i = 0; i < n - 1; i++)
                delta[i] = (y[i + 1] - y[i]) / spacing;

            m[0] = delta[0];
            m[n - 1] = delta[n - 2];

            for (int i = 1; i < n - 1; i++)
                m[i] = Math.Sign(delta[i - 1]) != Math.Sign(delta[i]) ? 0 : (delta[i - 1] + delta[i]) / 2;

            for (int i = 0; i < n - 1; i++)
            {
                if (Math.Abs(delta[i]) < 1e-12)
                {
                    m[i] = m[i + 1] = 0;
                    continue;
                }

                double alpha = m[i] / delta[i];
                double beta = m[i + 1] / delta[i];
                double magnitude = alpha * alpha + beta * beta;

                if (magnitude <= 9)
                    continue;

                double tau = 3 / Math.Sqrt(magnitude);
                m[i] = alpha * tau * delta[i];
                m[i + 1] = beta * tau * delta[i];
            }

            return m;
        }

        protected override DrawNode CreateDrawNode() => new StrainChartDrawNode(this);

        private class StrainChartDrawNode : DrawNode
        {
            public new StrainChart Source => (StrainChart)base.Source;

            private Texture texture = null!;
            private IShader shader = null!;
            private float[] columns = Array.Empty<float>();
            private float height;
            private Color4 fill;
            private Color4? line;
            private float lineWidth;
            private Color4 parent;

            public StrainChartDrawNode(StrainChart source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                texture = Source.texture;
                shader = Source.shader;
                columns = Source.columns;
                height = Source.DrawHeight;
                fill = Source.FillColour;
                line = Source.LineColour;
                lineWidth = Source.LineWidth;
                parent = DrawColourInfo.Colour.TopLeft.SRGB;
            }

            private Color4 tint(Color4 c) => new Color4(c.R * parent.R, c.G * parent.G, c.B * parent.B, c.A * parent.A);

            // The fill's gradient, by depth from the base: a tenth of its opacity there, all of it from two fifths of the way out.
            private Color4 fillAt(float depth)
            {
                float knee = 0.4f * height;
                float alpha = depth >= knee ? 1 : 0.2f + 0.8f * depth / knee;
                return tint(new Color4(fill.R, fill.G, fill.B, fill.A * alpha));
            }

            private Vector2 toScreen(float x, float y) => Vector2Extensions.Transform(new Vector2(x, y), DrawInfo.Matrix);

            private void quad(Vector2 topLeft, Vector2 topRight, Vector2 bottomLeft, Vector2 bottomRight, ColourInfo colour, IRenderer renderer) =>
                renderer.DrawQuad(texture, new Quad(toScreen(topLeft.X, topLeft.Y), toScreen(topRight.X, topRight.Y),
                    toScreen(bottomLeft.X, bottomLeft.Y), toScreen(bottomRight.X, bottomRight.Y)), colour);

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                if (columns.Length == 0)
                    return;

                shader.Bind();

                float knee = 0.4f * height;

                for (int c = 0; c < columns.Length; c++)
                {
                    float depth = columns[c];
                    if (depth <= 0)
                        continue;

                    float upper = Math.Min(depth, knee);
                    quad(new Vector2(c, 0), new Vector2(c + 1, 0), new Vector2(c, upper), new Vector2(c + 1, upper),
                        ColourInfo.GradientVertical(fillAt(0), fillAt(upper)), renderer);

                    if (depth > knee)
                    {
                        quad(new Vector2(c, knee), new Vector2(c + 1, knee), new Vector2(c, depth), new Vector2(c + 1, depth),
                            fillAt(knee), renderer);
                    }
                }

                if (line is Color4 lineColour)
                {
                    var colour = tint(lineColour);

                    for (int c = 0; c < columns.Length - 1; c++)
                    {
                        var a = new Vector2(c + 0.5f, columns[c]);
                        var b = new Vector2(c + 1.5f, columns[c + 1]);
                        var direction = b - a;
                        if (direction.LengthSquared < 1e-6f)
                            continue;

                        var normal = new Vector2(-direction.Y, direction.X).Normalized() * (lineWidth / 2);
                        quad(a - normal, b - normal, a + normal, b + normal, colour, renderer);
                    }
                }

                shader.Unbind();
            }
        }

        /// <summary>For tests: the curve's depth at each pixel column.</summary>
        internal IReadOnlyList<float> Columns => columns;
    }
}

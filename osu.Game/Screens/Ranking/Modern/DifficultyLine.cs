// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Modern
{
    /// <summary>
    /// "[difficulty] mapped by [mapper]" in the difficulty's colour, within a width: a long difficulty name is cut short ("…", the whole
    /// of it on hover) so the mapper always shows.
    /// </summary>
    public partial class DifficultyLine : FillFlowContainer
    {
        private readonly float maxWidth;
        private readonly TruncatingSpriteText name;
        private readonly OsuSpriteText mappedBy;
        private readonly TruncatingSpriteText mapper;

        public DifficultyLine(string difficultyName, string mapperName, Color4 colour, float maxWidth)
        {
            this.maxWidth = maxWidth;

            AutoSizeAxes = Axes.Both;
            Direction = FillDirection.Horizontal;
            Colour = colour;

            Children = new Drawable[]
            {
                name = new TruncatingSpriteText
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Text = difficultyName,
                    Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold),
                },
                mappedBy = new OsuSpriteText
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Text = " mapped by ",
                    Font = OsuFont.Torus.With(size: 14),
                },
                mapper = new TruncatingSpriteText
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Text = mapperName,
                    Font = OsuFont.Torus.With(size: 14, weight: FontWeight.SemiBold),
                    // (A mapper's name is short; this keeps an odd long one from taking all the room.)
                    MaxWidth = maxWidth / 2,
                },
            };

            Spacing = Vector2.Zero;
        }

        protected override void Update()
        {
            base.Update();

            // The name gets what's left after "mapped by" and the mapper.
            name.MaxWidth = Math.Max(40, maxWidth - mappedBy.DrawWidth - mapper.DrawWidth);
        }
    }
}

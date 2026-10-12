// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;

namespace osu.Game.Skinning.Select
{
    /// <summary>
    /// A beatmap list panel's thumbnail, like stable's: the whole background, filling a 4:3 box (cropped at the sides).
    /// Loaded in the background.
    /// </summary>
    public partial class LegacyCarouselThumbnail : Container
    {
        private CancellationTokenSource? loadCancellation;
        private WorkingBeatmap? working;

        public LegacyCarouselThumbnail()
        {
            Masking = true;
        }

        public WorkingBeatmap? Beatmap
        {
            get => working;
            set
            {
                // The same background (several difficulties of a set): nothing to load.
                if (working != null && value != null && working.BeatmapSetInfo.Equals(value.BeatmapSetInfo)
                    && working.Metadata.BackgroundFile == value.Metadata.BackgroundFile)
                    return;

                working = value;

                loadCancellation?.Cancel();
                loadCancellation = null;
                Clear();

                if (value == null)
                    return;

                loadCancellation = new CancellationTokenSource();

                LoadComponentAsync(new BackgroundSprite(value)
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fill,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                }, sprite =>
                {
                    Add(sprite);
                    sprite.FadeInFromZero(200);
                }, loadCancellation.Token);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            loadCancellation?.Cancel();
        }

        private partial class BackgroundSprite : Sprite
        {
            private readonly WorkingBeatmap working;

            public BackgroundSprite(WorkingBeatmap working)
            {
                this.working = working ?? throw new ArgumentNullException(nameof(working));
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Texture = working.GetBackground();
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking.Statistics.Session;
using osu.Game.Utils;

namespace osu.Game.Skinning.Components
{
    /// <summary>
    /// Shows which attempt at the current map this is in the current session, and optionally the best accuracy reached on it so far.
    /// </summary>
    /// <remarks>
    /// This only displays the count. The attempts themselves are counted while playing whether or not this is part of the skin.
    /// </remarks>
    [UsedImplicitly]
    public partial class AttemptCounter : FontAdjustableSkinComponent
    {
        [SettingSource("Show best accuracy", "Also show the best accuracy reached on this map so far in the current session.")]
        public BindableBool ShowBestAccuracy { get; } = new BindableBool(true);

        private readonly OsuSpriteText text;

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        [Resolved]
        private SessionStatsStore? store { get; set; }

        private string? beatmapKey;

        public AttemptCounter()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = text = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            beatmapKey = gameplayState?.Beatmap.BeatmapInfo.ToString();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (store != null)
                store.AttemptRegistered += onAttemptRegistered;

            ShowBestAccuracy.BindValueChanged(_ => updateText(), true);
        }

        private void onAttemptRegistered(string registeredBeatmapKey)
        {
            if (registeredBeatmapKey == beatmapKey)
                Schedule(updateText);
        }

        private void updateText()
        {
            // where nothing has been counted (e.g. while editing the skin), show what it would look like for a first attempt.
            int attempts = Math.Max(1, beatmapKey == null ? 0 : store?.GetAttempts(beatmapKey) ?? 0);

            string display = $"Attempt #{attempts}";

            if (ShowBestAccuracy.Value && store != null && beatmapKey != null)
            {
                double? bestAccuracy = store.CurrentSessionPlays.Where(p => p.Beatmap == beatmapKey).Select(p => (double?)p.Accuracy).Max();

                if (bestAccuracy != null)
                    display += $" · best {bestAccuracy.Value.FormatAccuracy()}";
            }

            text.Text = display;
        }

        protected override void SetFont(FontUsage font) => text.Font = font.With(size: 24);

        protected override void SetTextColour(Colour4 textColour) => text.Colour = textColour;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (store != null)
                store.AttemptRegistered -= onAttemptRegistered;
        }
    }
}

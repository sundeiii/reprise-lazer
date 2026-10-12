// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;

namespace osu.Game.Screens.Ranking.Statistics.Session
{
    /// <summary>
    /// An invisible hoverable strip which shows a tooltip and reports when it is hovered,
    /// used to make chart points inspectable. The owning chart decides how to highlight the hovered point.
    /// </summary>
    public partial class ChartHoverColumn : Container, IHasTooltip
    {
        public LocalisableString TooltipText { get; }

        /// <summary>
        /// Invoked with <see langword="true"/> when the mouse enters this column, and <see langword="false"/> when it leaves.
        /// </summary>
        public event Action<bool>? HoverChanged;

        public ChartHoverColumn(LocalisableString tooltipText)
        {
            TooltipText = tooltipText;
        }

        protected override bool OnHover(HoverEvent e)
        {
            HoverChanged?.Invoke(true);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            HoverChanged?.Invoke(false);
        }
    }
}

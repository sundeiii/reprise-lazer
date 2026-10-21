// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Users
{
    /// <summary>
    /// Reprise: the local player's rank and pp in the mode they're playing (with Relax or Autopilot on, that mode's), on a line: for the
    /// 2023 interface's user card, which has none.
    /// </summary>
    public partial class ModeRankLine : CompositeDrawable
    {
        [Resolved]
        private LocalUserStatisticsProvider? statisticsProvider { get; set; }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IBindable<IReadOnlyList<Mod>>? selectedMods { get; set; }

        private RulesetInfo shownRuleset => ruleset.Value.ForMods(selectedMods?.Value ?? Array.Empty<Mod>());

        private OsuSpriteText rank = null!;
        private OsuSpriteText countryRank = null!;
        private OsuSpriteText pp = null!;
        private OsuSpriteText mode = null!;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new osuTK.Vector2(10, 0),
                Padding = new MarginPadding { Horizontal = 10, Vertical = 5 },
                Children = new Drawable[]
                {
                    rank = new OsuSpriteText { Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold) },
                    countryRank = new OsuSpriteText { Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold), Colour = colours.Gray9, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft },
                    pp = new OsuSpriteText { Font = OsuFont.GetFont(size: 16, weight: FontWeight.SemiBold), Colour = colours.BlueLighter },
                    mode = new OsuSpriteText { Font = OsuFont.GetFont(size: 14), Colour = colours.Gray9, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (statisticsProvider != null)
                statisticsProvider.StatisticsUpdated += onStatisticsUpdated;

            ruleset.BindValueChanged(_ => updateDisplay());
            selectedMods?.BindValueChanged(_ => updateDisplay());
            updateDisplay();
        }

        private void onStatisticsUpdated(UserStatisticsUpdate update)
        {
            if (update.Ruleset.ShortName == shownRuleset.ShortName)
                Schedule(updateDisplay);
        }

        private void updateDisplay()
        {
            var shown = shownRuleset;
            var statistics = statisticsProvider?.GetStatisticsFor(shown);

            rank.Text = statistics?.GlobalRank?.ToLocalisableString("\\##,##0") ?? "#-";
            countryRank.Text = statistics?.CountryRank is int country ? LocalisableString.Interpolate($"(#{country.ToLocalisableString(@"#,##0")} in country)") : default;
            pp.Text = statistics?.PP is decimal performance ? LocalisableString.Interpolate($"{performance.ToLocalisableString(@"#,##0")}pp") : default;
            mode.Text = shown.Name;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (statisticsProvider != null)
                statisticsProvider.StatisticsUpdated -= onStatisticsUpdated;
        }
    }
}

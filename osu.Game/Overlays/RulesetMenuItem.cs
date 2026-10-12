// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
// This file is originally created by GooGuTeam.

using System;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;

namespace osu.Game.Overlays
{
    public class RulesetMenuItem : ToggleMenuItem
    {
        public RulesetMenuItem(OverlayRulesetSelector overlayRulesetSelector, RulesetInfo rulesetInfo, Action action)
            : base(rulesetInfo.Name, MenuItemType.Standard, state =>
            {
                overlayRulesetSelector.Current.Value = state ? rulesetInfo : rulesetInfo.CreateNormalRuleset();
                action?.Invoke();
            })
        {
            State.Value = overlayRulesetSelector.Current.Value.Equals(rulesetInfo);
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Scoring.Legacy;

namespace osu.Game.Rulesets
{
    public interface ILegacyRuleset
    {
        /// <summary>
        /// Reprise: reprise.pw's modes, osu!'s four and the Relax/Autopilot ones (<see cref="RulesetInfo.CATCH_RELAX_ONLINE_ID"/>); osu!'s is 3.
        /// Others (custom rulesets) aren't the server's, so they don't submit scores.
        /// </summary>
        const int MAX_LEGACY_RULESET_ID = RulesetInfo.CATCH_RELAX_ONLINE_ID;

        /// <summary>
        /// Identifies the server-side ID of a legacy ruleset.
        /// </summary>
        int LegacyID { get; }

        ILegacyScoreSimulator CreateLegacyScoreSimulator();
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Difficulty
{
    /// <summary>
    /// Reprise: Relax and Autopilot plays are worth what the same play without them is (reprise.pw works their
    /// pp out that way), so the star rating leaves those two mods out (and osu!'s pp formula has no Relax or Autopilot parts).
    /// </summary>
    public static class VanillaDifficulty
    {
        /// <summary>Whether <paramref name="mod"/> is Relax or Autopilot.</summary>
        public static bool IsAssist(Mod mod) => mod is ModRelax || mod.Acronym == "AP";

        /// <summary><paramref name="mods"/> without Relax and Autopilot.</summary>
        public static IEnumerable<Mod> WithoutAssist(IEnumerable<Mod> mods) => mods.Where(m => !IsAssist(m));
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;

namespace osu.Game.Rulesets.Mods
{
    public abstract class ModRelax : Mod
    {
        public override string Name => "Relax";
        public override string Acronym => "RX";
        public override IconUsage? Icon => OsuIcon.ModRelax;
        public override ModType Type => ModType.Automation;

        // Reprise: ranked (reprise.pw has Relax leaderboards and pp).
        public override bool Ranked => true;

        public override Type[] IncompatibleMods => new[] { typeof(ModAutoplay) };
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.Ranking.Statistics.Session
{
    /// <summary>
    /// How a play compares to earlier plays of the same beatmap with the same kind of setup.
    /// </summary>
    /// <param name="PreviousAttempts">How many earlier plays it was compared against.</param>
    /// <param name="NewBestUnstableRate">Whether it has a lower unstable rate than all of them.</param>
    /// <param name="NewBestAccuracy">Whether it has a higher accuracy than all of them.</param>
    public record PersonalBests(int PreviousAttempts, bool NewBestUnstableRate, bool NewBestAccuracy)
    {
        public bool HasAnyNewBest => PreviousAttempts > 0 && (NewBestUnstableRate || NewBestAccuracy);
    }
}

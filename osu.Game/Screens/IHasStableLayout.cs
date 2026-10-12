// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens
{
    /// <summary>
    /// A screen which can show osu!stable's layout (with a legacy skin and "Use osu!stable layouts"). Stable's layouts are made for
    /// 4:3 to 16:9: while one shows, windows wider than 16:9 show the screen 16:9 wide (margins at the sides), so it's never stretched.
    /// </summary>
    public interface IHasStableLayout
    {
        bool StableLayoutShown { get; }
    }
}

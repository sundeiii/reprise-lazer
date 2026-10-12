// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Skinning.Ranking
{
    /// <summary>
    /// What the second page of a legacy ranking screen shows beyond the score itself, and what its options do.
    /// Each part is optional: what isn't known shows as "-" or not at all.
    /// </summary>
    public class LegacyRankingDetails
    {
        /// <summary>
        /// The score's place among the local scores on its beatmap (1 for the best), or null when it isn't a local score.
        /// </summary>
        public int? LocalRank { get; init; }

        /// <summary>
        /// The player's statistics before and after the score (rank, accuracy, ranked and total score), once the server has them.
        /// </summary>
        public IBindable<ScoreBasedUserStatisticsUpdate?> StatisticsUpdate { get; init; } = new Bindable<ScoreBasedUserStatisticsUpdate?>();

        /// <summary>
        /// The player's statistics as they are now (for the logged-in player's own scores): shown when the server sends no
        /// before-and-after for the score.
        /// </summary>
        public IBindable<Users.UserStatistics?> CurrentStatistics { get; init; } = new Bindable<Users.UserStatistics?>();

        /// <summary>
        /// The player's best place on the beatmap's online ranking, once fetched (stable's "your best: #1" when this score isn't it).
        /// </summary>
        public IBindable<int?> UserBestPosition { get; init; } = new Bindable<int?>();

        /// <summary>
        /// The performance the player needs to pass the next player up the rankings, once fetched (null at #1 or when unknown).
        /// </summary>
        public IBindable<double?> ToNextRank { get; init; } = new Bindable<double?>();

        /// <summary>
        /// The player's best score on the beatmap before this one, once fetched (null when it's their first, or unknown).
        /// </summary>
        public IBindable<SoloScoreInfo?> PreviousBest { get; init; } = new Bindable<SoloScoreInfo?>();

        /// <summary>
        /// The score's performance (pp), once known.
        /// </summary>
        public IBindable<double?> Performance { get; init; } = new Bindable<double?>();

        /// <summary>
        /// The beatmap as the server knows it (its play and pass counts), once fetched.
        /// </summary>
        public IBindable<APIBeatmap?> OnlineBeatmap { get; init; } = new Bindable<APIBeatmap?>();

        /// <summary>
        /// Whether there's a second page (the rankings after a play): stable shows it, and its button, only for a score just played.
        /// </summary>
        public bool HasSecondPage { get; init; } = true;

        /// <summary>
        /// Leaves the screen (the skin's back button, at the bottom left on both pages), or null for no back button.
        /// </summary>
        public Action? Back { get; init; }

        /// <summary>
        /// Exports the score's replay as an .osr, or null when there's no replay to export.
        /// </summary>
        public Action? ExportReplay { get; init; }

        /// <summary>
        /// Favourites or unfavourites the beatmap set online, or null when it can't be favourited.
        /// </summary>
        public Action? ToggleFavourite { get; init; }

        /// <summary>
        /// Whether the beatmap set is favourited.
        /// </summary>
        public IBindable<bool> Favourited { get; init; } = new Bindable<bool>();
    }
}

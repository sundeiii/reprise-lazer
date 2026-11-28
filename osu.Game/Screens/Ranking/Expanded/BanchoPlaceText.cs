// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Scoring;

namespace osu.Game.Screens.Ranking.Expanded
{
    /// <summary>
    /// Reprise: "#1,234 on osu!", where the play would place on osu!'s own leaderboard for the map (for a player with osu!'s
    /// scores on; nothing otherwise, for a fail, or when osu! has no board for it).
    /// </summary>
    public partial class BanchoPlaceText : OsuSpriteText, IHasTooltip
    {
        private readonly ScoreInfo score;

        [Resolved]
        private IAPIProvider? api { get; set; }

        private GetBanchoPlaceRequest? request;

        public LocalisableString TooltipText { get; private set; }

        public BanchoPlaceText(ScoreInfo score)
        {
            this.score = score;
            Alpha = 0;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (api == null || !api.IsLoggedIn || score.BeatmapInfo?.OnlineID is not > 0 || score.Rank == ScoreRank.F || !score.Passed || score.TotalScore <= 0)
                return;

            request = new GetBanchoPlaceRequest(score.BeatmapInfo.OnlineID, score.Ruleset.ShortName, score.TotalScore, score.LegacyTotalScore,
                score.Mods.Select(m => m.Acronym));
            request.Success += result => Schedule(() => Show(result));
            api.Queue(request);
        }

        /// <summary>Shows the server's answer (nothing when the player doesn't have osu!'s scores on, or there's no place).</summary>
        public void Show(BanchoPlace result)
        {
            if (!result.Enabled || result.Place is not int place)
                return;

            Text = $"#{place:N0} on osu!";
            TooltipText = result.Count > 0 ? $"Where this play would place among osu!'s {result.Count:N0} plays on this map" : "Where this play would place on osu!'s leaderboard";
            this.FadeIn(300);
        }

        protected override void Dispose(bool isDisposing)
        {
            request?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}

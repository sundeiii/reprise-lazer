// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Scoring
{
    public class ScoreModelDownloader : ModelDownloader<ScoreInfo, IScoreInfo>
    {
        public ScoreModelDownloader(IModelImporter<ScoreInfo> scoreManager, IAPIProvider api)
            : base(scoreManager, api)
        {
        }

        protected override ArchiveDownloadRequest<IScoreInfo> CreateDownloadRequest(IScoreInfo score, bool minimiseDownload) => new DownloadReplayRequest(score);

        protected override void PostProcessImportedModels(ArchiveDownloadRequest<IScoreInfo> request, IEnumerable<Live<ScoreInfo>> imported)
        {
            var onlineScore = request.Model;

            foreach (var importedScore in imported)
            {
                importedScore.PerformWrite(score =>
                {
                    // A downloaded online replay may contain stale legacy username metadata inside the .osr.
                    // The clicked API score is authoritative for online identity and replay matching.
                    if (onlineScore.OnlineID > 0)
                        score.OnlineID = onlineScore.OnlineID;

                    if (onlineScore.LegacyOnlineID > 0)
                        score.LegacyOnlineID = onlineScore.LegacyOnlineID;

                    if (onlineScore.User != null)
                    {
                        score.User = new APIUser
                        {
                            Id = onlineScore.User.OnlineID,
                            Username = onlineScore.User.Username,
                            CountryCode = onlineScore.User.CountryCode,
                        };
                    }
                });
            }
        }

        public override ArchiveDownloadRequest<IScoreInfo>? GetExistingDownload(IScoreInfo model)
            => CurrentDownloads.Find(r => r.Model.MatchesOnlineID(model));
    }
}
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Tests.Online
{
    /// <summary>Reprise: osu!'s own (bancho's) scores on our leaderboards come marked, and the mark reaches the score and the in-game board.</summary>
    [TestFixture]
    public class TestBanchoScores
    {
        [Test]
        public void TestMarkedFromTheServer()
        {
            var bancho = JsonConvert.DeserializeObject<SoloScoreInfo>(@"{""id"": 5747987361, ""user_id"": 7341183, ""total_score"": 1115470, ""tomyum_bancho"": true}")!;
            var ours = JsonConvert.DeserializeObject<SoloScoreInfo>(@"{""id"": 12, ""user_id"": 4, ""total_score"": 523844}")!;

            var banchoScore = bancho.ToScoreInfo(Array.Empty<Mod>());
            Assert.That(banchoScore.IsBancho, Is.True);
            Assert.That(ours.ToScoreInfo(Array.Empty<Mod>()).IsBancho, Is.False);

            Assert.That(new Screens.Play.Leaderboards.GameplayLeaderboardScore(banchoScore, false, Screens.Play.Leaderboards.GameplayLeaderboardScore.ComboDisplayMode.Highest).IsBancho, Is.True);
        }
    }
}

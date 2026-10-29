// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Scoring;

namespace osu.Game.Tests.Online
{
    /// <summary>
    /// Reprise: <see cref="APIUser.HasStatistics"/> says whether a user response came with statistics.
    /// </summary>
    [TestFixture]
    public class TestAPIUserJsonDeserialisation
    {
        [Test]
        public void TestResponseWithStatisticsHasThem()
        {
            var user = JsonConvert.DeserializeObject<APIUser>(@"{""id"": 4, ""username"": ""Lilily"",
                ""statistics"": {""pp"": 157.993, ""global_rank"": 4, ""country_rank"": 1, ""grade_counts"": {""ss"": 0, ""ssh"": 0, ""s"": 1, ""sh"": 0, ""a"": 1}},
                ""rank_history"": {""mode"": ""osu"", ""data"": [5, 4]}}")!;

            Assert.That(user.HasStatistics, Is.True);
            Assert.That(user.Statistics.PP, Is.EqualTo(157.993m));
            Assert.That(user.Statistics.CountryRank, Is.EqualTo(1));
            Assert.That(user.Statistics.GradesCount[ScoreRank.S], Is.EqualTo(1));
            Assert.That(user.Statistics.RankHistory?.Data, Is.EqualTo(new[] { 5, 4 }));
        }

        [Test]
        public void TestResponseWithoutStatisticsHasNone()
        {
            var user = JsonConvert.DeserializeObject<APIUser>(@"{""id"": 4, ""username"": ""Lilily""}")!;

            Assert.That(user.HasStatistics, Is.False);
            Assert.That(user.Statistics, Is.Not.Null);
        }
    }
}

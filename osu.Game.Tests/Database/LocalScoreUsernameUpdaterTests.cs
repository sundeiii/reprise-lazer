// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Scoring;
using osu.Game.Tests.Resources;
using osu.Game.Users;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class LocalScoreUsernameUpdaterTests : RealmTest
    {
        [Test]
        public void TestOwnScoresTakeTheCurrentName()
        {
            RunTestWithRealm((realm, _) =>
            {
                realm.Write(r =>
                {
                    var set = TestResources.CreateTestBeatmapSetInfo();
                    r.Add(set);
                    var beatmap = set.Beatmaps.First();
                    var ruleset = beatmap.Ruleset;

                    ScoreInfo score(int id, string name, CountryCode country) => new ScoreInfo(beatmap, ruleset)
                    {
                        BeatmapHash = beatmap.Hash,
                        User = new APIUser { Id = id, Username = name, CountryCode = country },
                    };

                    r.Add(score(4, "Lilily", CountryCode.EE));
                    r.Add(score(4, "Lilily", CountryCode.EE));
                    r.Add(score(4, "FlyingTuna", CountryCode.EE));
                    r.Add(score(7, "Lilily", CountryCode.FR)); // someone else with that name once
                    r.Add(score(1, "Guest", CountryCode.Unknown));
                });

                Assert.That(realm.Write(r => LocalScoreUsernameUpdater.Update(r, 4, "FlyingTuna", CountryCode.EE)), Is.EqualTo(2));
                Assert.That(realm.Write(r => LocalScoreUsernameUpdater.Update(r, 4, "FlyingTuna", CountryCode.EE)), Is.EqualTo(0)); // nothing left to change

                realm.Run(r =>
                {
                    r.Refresh();
                    Assert.That(r.All<ScoreInfo>().AsEnumerable().Where(s => s.UserID == 4).Select(s => s.User.Username), Is.All.EqualTo("FlyingTuna"));
                    Assert.That(r.All<ScoreInfo>().AsEnumerable().Single(s => s.UserID == 7).User.Username, Is.EqualTo("Lilily"));
                    Assert.That(r.All<ScoreInfo>().AsEnumerable().Single(s => s.UserID == 1).User.Username, Is.EqualTo("Guest"));
                });

                // A country change alone counts too; an unknown country leaves the stored one.
                Assert.That(realm.Write(r => LocalScoreUsernameUpdater.Update(r, 4, "FlyingTuna", CountryCode.FI)), Is.EqualTo(3));
                Assert.That(realm.Write(r => LocalScoreUsernameUpdater.Update(r, 4, "FlyingTuna", CountryCode.Unknown)), Is.EqualTo(0));
                realm.Run(r => Assert.That(r.All<ScoreInfo>().AsEnumerable().Where(s => s.UserID == 4).Select(s => s.User.CountryCode), Is.All.EqualTo(CountryCode.FI)));
            });
        }
    }
}

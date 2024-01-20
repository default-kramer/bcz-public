using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core;
using BCZ.Core.ReplayModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BCZ.CoreTests
{
    [TestClass]
    public class ReplayTests : ReplayTestBase
    {
        private static CompletedGameInfo Get(string path)
        {
            path = Path.Combine(ReplayDirectory.FullName, path);
            using var reader = new StreamReader(path);
            if (ReplayReader.ValidateReplay(reader, out var gameInfo))
            {
                return gameInfo;
            }
            throw new Exception("Failed to validate replay");
        }

        public record struct TestItem(string Filename, int TotalScore, int EnemyScore, int ComboScore);

        private static IEnumerable<TestItem> RawTestData()
        {
            yield return new TestItem("010.ffr", 1450, 1000, 450); // tall, went AFK after first combo
            yield return new TestItem("011.ffr", 32350, 19200, 13150); // tall
            yield return new TestItem("012.ffr", 28050, 14600, 13450); // wide
            yield return new TestItem("013.ffr", 10702, 3200, 7502); // wide, only combo resolved after time expired
        }

        private static IEnumerable<object[]> TestData => RawTestData().Select(x => new object[] { x });

        [TestMethod, DynamicData(nameof(TestData))]
        public void final_score_tests(TestItem expected)
        {
            var score = Get(expected.Filename).FinalState.Score;
            Assert.AreEqual(expected.TotalScore, score.TotalScore);
            Assert.AreEqual(expected.EnemyScore, score.EnemyScore);
            Assert.AreEqual(expected.ComboScore, score.ComboScore);
        }
    }
}

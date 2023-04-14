using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FF2.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FF2.CoreTests
{
    [TestClass]
    public class TimingTests
    {
        private Grid grid = null!;
        private InfiniteSpawnDeck deck = null!;
        private State state = null!;
        private Ticker ticker = null!;
        private Moment now;

        [TestInitialize]
        public void setup()
        {
            grid = Grid.Create(8, 20);
            // This deck returns <y b>, <b r>, <b y>, <o y>, <y y>, <o r>
            deck = new InfiniteSpawnDeck(Lists.MainDeck, new PRNG(new PRNG.State(1, 1, 1, 2, 2, 2)));
            now = new Moment(0);
            grid.Set(new Loc(0, 0), Occupant.MakeEnemy(Color.Red));
            state = State.CreateWithInfiniteHealth(grid, deck);
            ticker = new Ticker(state, NullReplayCollector.Instance);
        }

        private void Advance(int millis)
        {
            now = now.AddMillis(millis);
            ticker.Advance(now);
        }

        private string Info => ticker.AnimationString;

        private void DoCommands(params Command[] commands)
        {
            const int millis = 10; // 10ms per command

            foreach (var command in commands)
            {
                now = now.AddMillis(millis);
                if (!ticker.HandleCommand(command, now))
                {
                    throw new Exception("Failed to do command: " + command);
                }
            }
        }

        private string GridDump => grid.PrintGrid;

        [TestMethod]
        public void asdf()
        {
            Assert.Inconclusive("TODO rewrite this test");

            Assert.AreEqual("Spawning 100", Info);
            Advance(0);
            Assert.AreEqual("Spawning 100", Info);
            Advance(99);
            Assert.AreEqual("Spawning 1", Info);
            Advance(1);
            Assert.AreEqual("Pre-Waiting", Info);
            Advance(1000);
            Assert.AreEqual("Pre-Waiting", Info);

            // Build a tower at the midpoint (x=3 not x=4) 
            const int towerCount = 3;
            for (int i = 0; i < towerCount; i++)
            {
                DoCommands(Command.RotateCW, Command.BurstBegin, Command.BurstCancel);
                // After a plummet, the next tick will *attempt* to fall (but it won't succeed):
                Assert.AreEqual("Pre-Falling", Info);
                // We need to Advance() before the state will change:
                Advance(1);
                Assert.AreEqual("Spawning 99", Info);
                Advance(99);
                Assert.AreEqual("Pre-Waiting", Info);
            }

            // We built a tower at x=3. Now if we just drop a blank straight down the blank half
            // will land on the tower. Once bursting is complete, the other half will fall alongside the tower.
            var mover = state.PreviewPlummet() ?? throw new Exception("WTF");
            Assert.AreEqual(Color.Blank, mover.OccA.Color);
            const int towerHeight = towerCount * 2;
            Assert.AreEqual(new Loc(3, towerHeight), mover.LocA);

            DoCommands(Command.BurstBegin);
            Assert.AreEqual("Bursting 500", Info);
            Advance(499);
            Assert.AreEqual("Bursting 1", Info);
            Advance(1);
            // Falling takes 150ms per cell
            const int fallRate = 150;
            Assert.AreEqual($"Falling {towerHeight * fallRate}", Info);
            //Assert.AreEqual(0f, ticker.AnimationProgress(now));
            Advance(towerHeight * fallRate / 2);
            //Assert.AreEqual(0.5f, ticker.AnimationProgress(now), 0.01f);
            Advance(towerHeight * fallRate / 2 - 1);
            Assert.AreEqual("Falling 1", Info);
        }
    }
}

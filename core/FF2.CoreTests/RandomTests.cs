using FF2.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace FF2.CoreTests
{
    [TestClass]
    public class RandomTests
    {
        private static State MakeState(PRNG prng)
        {
            var settings = new SinglePlayerSettings();
            var seed = PRNG.State.Deserialize(prng.Serialize());
            return State.Create(new SeededSettings(seed, settings));
        }

        [TestMethod]
        public void queue_is_deterministic()
        {
            var prng1 = PRNG.Create();
            var prng2 = prng1.Clone();
            var state1 = MakeState(prng1);
            var state2 = MakeState(prng2);
            var queue1 = state1.MakeQueueModel();
            var queue2 = state2.MakeQueueModel();

            for (int i = 0; i < 99; i++)
            {
                Assert.AreEqual(queue1[i], queue2[i]);
            }
        }

        [TestMethod]
        public void ValidateRandomDoubles()
        {
            // For these tests, the output files were generated by the reference Java implementation.
            var state = GetState(3044737622, 155609380, 3488820501, 1922517136, 40728809, 3640400285);
            int lineCount = ValidateDoubles(state);
            Assert.AreEqual(104444, lineCount);

            state = GetState(1928380848, 1542618196, 3045600165, 1717332222, 3142207023, 1149002106);
            lineCount = ValidateDoubles(state);
            Assert.AreEqual(104444, lineCount);

            state = GetState(753845826, 4014832777, 2150615510, 1513918462, 256437212, 4072870820);
            lineCount = ValidateDoubles(state);
            Assert.AreEqual(104444, lineCount);
        }

        private static PRNG.State GetState(long s10, long s11, long s12, long s20, long s21, long s22)
        {
            return new PRNG.State(s10, s11, s12, s20, s21, s22);
        }

        private static string GetFilename(PRNG.State state, string pattern)
        {
            var seedStr = string.Format("{0}-{1}-{2}-{3}-{4}-{5}",
                state.s10, state.s11, state.s12, state.s20, state.s21, state.s22);

            return pattern.Replace("{seed}", seedStr);
        }

        private static int ValidateDoubles(PRNG.State seed)
        {
            var filename = GetFilename(seed, "rand-doubles-{seed}.txt");
            var rand = new PRNG(seed);

            // Throw away one value to align output with L'Ecuyer original version
            rand.NextDouble();
            // Make sure that cloning works correctly
            var clone = rand.Clone();

            // Run tests
            int count1 = Validate(filename, double.Parse, () => rand.NextDouble());

            // Run tests again with the clone
            rand = clone;
            int count2 = Validate(filename, double.Parse, () => rand.NextDouble());

            Assert.AreEqual(count1, count2);
            return count1;
        }

        /// <summary>
        /// Finds the embedded resource with the given filename.
        /// For each line, we assert that the two values produced by the given Funcs are equal.
        /// Finally, we return the number of assertions that we performed
        /// (which is also the number of lines in the file).
        /// </summary>
        private static int Validate<T>(string filename, Func<string, T> expectedFunc, Func<T> actualFunc)
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = string.Format("{0}.RandomExpectations.{1}", asm.GetName().Name, filename);

            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new Exception("Missing embedded resource: " + resourceName);
            }

            int lineCount = 0;

            using var reader = new System.IO.StreamReader(stream);
            while (!reader.EndOfStream)
            {
                lineCount++;
                var line = reader.ReadLine() ?? "";
                T expected = expectedFunc(line);
                T actual = actualFunc();
                Assert.AreEqual(expected, actual, string.Format("On line {0} of {1}", lineCount, filename));
            }

            return lineCount;
        }
    }
}

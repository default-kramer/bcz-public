using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BCZ.CoreTests
{
    [TestClass]
    public class LayoutTests
    {
        [TestMethod]
        public void will_not_generate_groups_of_3()
        {
            var settingsCollection = SinglePlayerSettings.LevelsModeWithBlanks;
            var settings = settingsCollection.GetSettings(settingsCollection.MaxLevel);

            var prng = PRNG.Create(new Random());

            // Make sure we can find groups of 2 to make sure our test actually works
            bool foundGroupsOfTwo = false;

            for (int i = 0; i < 1000; i++)
            {
                prng.NextDouble();
                var name = prng.Serialize();

                var grid = Grid.Create(settings, prng.Clone());
                var result = grid.Test_FindGroups(3);
                if (result.TotalNumGroupsPermissive > 0)
                {
                    Assert.Fail($"Generated group(s) of 3 with seed: {name}");
                }

                if (!foundGroupsOfTwo)
                {
                    result = grid.Test_FindGroups(2);
                    if (result.TotalNumGroupsPermissive > 0)
                    {
                        foundGroupsOfTwo = true;
                    }
                }
            }

            Assert.IsTrue(foundGroupsOfTwo, "Failed to find groups of 2, this test must be broken.");
        }
    }
}

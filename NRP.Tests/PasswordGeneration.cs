using NRP;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace NRP.Tests
{
    [TestClass]
    public class PasswordGenerationTest
    {
        private int Count = 0;

        [TestMethod]
        public void CreateTestParams1()
        {
            IPasswordGenerator pg = PasswordGenerator.Create();
            Assert.IsNotNull(pg);
            Assert.IsTrue(pg.CreateLength >= 4);
            Assert.AreEqual(0, pg.FirstCharacter);
            Assert.IsFalse(pg.HasFirstCharacter);
            Assert.IsNull(pg.MaxLength);
            Assert.IsNull(pg.MinLength);
            Assert.AreEqual(1, pg.NumberToCreate);
        }

        [TestMethod]
        public void CreateTestParams2()
        {
            IPasswordGenerator pg = PasswordGenerator.Create(20);
            Assert.IsNotNull(pg);
            Assert.AreEqual(20, pg.CreateLength);
            Assert.AreEqual(0, pg.FirstCharacter);
            Assert.IsFalse(pg.HasFirstCharacter);
            Assert.IsNull(pg.MaxLength);
            Assert.IsNull(pg.MinLength);
            Assert.AreEqual(1, pg.NumberToCreate);
        }

        [TestMethod]
        public void TestGenerate1()
        {
            char[][] charGroups = Characters.GetDefaultCharGroups();
            IPasswordGenerator pg = PasswordGenerator.Create(20);

            string[] passes = pg.Generate();
            Assert.IsNotNull(passes);
            Assert.AreEqual(1, passes.Length);
            string pass = passes[0];
            Assert.AreEqual(20, pass.Length);

            // Make sure at least one letter from each group in present in the string.
            Assert.IsTrue(charGroups.All(grp => pass.Any(c => grp.Contains(c))));
        }

        [TestMethod]
        public void TestGenerate2()
        {
            IPasswordGenerator pg = PasswordGenerator.Create(12);
            pg.NumberToCreate = 12;

            string[] passes = pg.Generate();
            Assert.IsNotNull(passes);
            Assert.AreEqual(12, passes.Length);

            Assert.IsTrue(passes.All(x => x.Length == 12));
        }

        [TestMethod]
        public void TestGenerate3()
        {
            char[][] charGroups = Characters.GetDefaultCharGroups();
            IPasswordGenerator pg = PasswordGenerator.Create(20);
            pg.FirstCharacter = char.Parse("#");
            pg.NumberToCreate = 5;

            string[] passes = pg.Generate();
            Assert.IsNotNull(passes);
            Assert.AreEqual(5, passes.Length);

            Assert.IsTrue(passes.All(x => x.Length == 20));
            Assert.IsTrue(passes.All(x => x.StartsWith("#")));

            // Make sure at least one letter from each group in present in each string.
            Assert.IsTrue(charGroups.All(grp =>
                passes.All(p =>
                    p.Any(c => grp.Contains(c)))));
        }

        [TestMethod]
        public void TestGenerate4()
        {
            char[][] charGroups = Characters.GetDefaultCharGroups();
            IPasswordGenerator pg = PasswordGenerator.Create(20);
            pg.FirstCharacter = char.Parse("D");
            pg.NumberToCreate = 7;
            pg.MinLength = 6;
            pg.MaxLength = 16;

            string[] passes = pg.Generate();
            Assert.IsNotNull(passes);
            Assert.AreEqual(7, passes.Length);

            Assert.IsTrue(passes.All(x => x.Length >= 6 && x.Length <= 16));
        }
    }
}

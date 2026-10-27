// <copyright file="StringExtensionTests.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StringResourceVisualizer.Tests
{
    [TestClass]
    public class StringExtensionTests
    {
        [TestMethod]
        public async Task IndexOfAny_NoValues_FindsNothing()
        {
            var actual = await "Resources.Greeting".IndexOfAnyAsync();

            Assert.AreEqual(-1, actual);
        }

        [TestMethod]
        public async Task IndexOfAny_ValueThatIsNotThere_FindsNothing()
        {
            var actual = await "var x = 1;".IndexOfAnyAsync("Resources.");

            Assert.AreEqual(-1, actual);
        }

        [TestMethod]
        public async Task IndexOfAny_SearchIsCaseSensitive()
        {
            var actual = await "var x = resources.Greeting;".IndexOfAnyAsync("Resources.");

            Assert.AreEqual(-1, actual);
        }

        [TestMethod]
        public async Task IndexOfAny_SingleValue_FindsItsFirstOccurrence()
        {
            var actual = await "var x = Resources.A + Resources.B;".IndexOfAnyAsync("Resources.");

            Assert.AreEqual(8, actual);
        }

        [TestMethod]
        public async Task IndexOfAny_SeveralValues_FindsTheEarliestOne()
        {
            var actual = await "Strings.A + Resources.B".IndexOfAnyAsync("Resources.", "Strings.");

            Assert.AreEqual(0, actual);
        }

        [TestMethod]
        public async Task IndexOfAny_SeveralValues_FindsTheEarliestOneWhateverTheOrderOfTheValues()
        {
            var actual = await "Strings.A + Resources.B".IndexOfAnyAsync("Strings.", "Resources.");

            Assert.AreEqual(0, actual);
        }

        [TestMethod]
        public async Task IndexOfAny_SeveralValues_SkipsTheOnesThatAreNotThere()
        {
            var actual = await "x = Strings.A".IndexOfAnyAsync("Resources.", "Strings.");

            Assert.AreEqual(4, actual);
        }

        [TestMethod]
        public async Task IndexOfAny_DuplicateValues_DoNotMatter()
        {
            var actual = await "x = Resources.A".IndexOfAnyAsync("Resources.", "Resources.");

            Assert.AreEqual(4, actual);
        }

        [TestMethod]
        public async Task GetAllIndexes_NameBeforeALongerName_IsFound()
        {
            var actual = await "Strings.A + Resources.B".GetAllIndexesAsync("Strings.", "Resources.");

            CollectionAssert.AreEqual(new[] { 0, 12 }, actual);
        }

        [TestMethod]
        public async Task GetAllIndexes_NameAfterALongerName_IsFound()
        {
            var actual = await "Resources.A + Strings.B".GetAllIndexesAsync("Strings.", "Resources.");

            CollectionAssert.AreEqual(new[] { 0, 14 }, actual);
        }

        [TestMethod]
        public async Task GetAllIndexes_NamesOfDifferentLengthsInAnyOrder_AreAllFoundFromLeftToRight()
        {
            var actual = await "Strings.A, Resources.B, Strings.C".GetAllIndexesAsync("Strings.", "Resources.");

            CollectionAssert.AreEqual(new[] { 0, 11, 24 }, actual);
        }

        [TestMethod]
        public async Task GetAllIndexes_EveryUseOfTheSameName_IsFound()
        {
            var actual = await "Resources.A + Resources.B".GetAllIndexesAsync("Resources.");

            CollectionAssert.AreEqual(new[] { 0, 14 }, actual);
        }

        [TestMethod]
        public async Task GetAllIndexes_NameThatEndsWithAnotherName_FindsBoth()
        {
            var actual = await "MyResources.A".GetAllIndexesAsync("Resources.", "MyResources.");

            CollectionAssert.AreEqual(new[] { 0, 2 }, actual);
        }

        [TestMethod]
        public async Task GetAllIndexes_NothingFound_IsEmpty()
        {
            var actual = await "var x = 1;".GetAllIndexesAsync("Resources.");

            Assert.AreEqual(0, actual.Count);
        }

        [TestMethod]
        public async Task GetAllIndexes_NoValues_IsEmpty()
        {
            var actual = await "Resources.A".GetAllIndexesAsync();

            Assert.AreEqual(0, actual.Count);
        }
    }
}

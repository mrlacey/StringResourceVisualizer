// <copyright file="ResourceAdornmentManagerTests.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StringResourceVisualizer.Tests
{
    [TestClass]
    public class ResourceAdornmentManagerTests
    {
        [TestMethod]
        public void FormatDisplayString_SingleLine()
        {
            var input = "I am a single line string";

            TestIt(input, input);
        }

        [TestMethod]
        public void FormatDisplayString_MultipleLines()
        {
            var input = "I am a the first line\r\nand this is the second line";
            var expected = "I am a the first line⏎";

            TestIt(input, expected);
        }

        [TestMethod]
        public void FormatDisplayString_TwoLines_FirstIsBlank()
        {
            var input = "\r\nthis is the second line";
            var expected = "⏎this is the second line";

            TestIt(input, expected);
        }

        [TestMethod]
        public void FormatDisplayString_MultipleLines_FirstIsBlank()
        {
            var input = "\r\nthis is the second line\r\nand this is the third line";
            var expected = "⏎this is the second line⏎";

            TestIt(input, expected);
        }

        private void TestIt(string input, string expected)
        {
            var actual = ResourceAdornmentManager.FormatDisplayText(input);

            Assert.AreEqual(expected, actual);
        }
    }
}

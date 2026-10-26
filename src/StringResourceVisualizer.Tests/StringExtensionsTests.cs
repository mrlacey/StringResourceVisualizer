// <copyright file="StringExtensionsTests.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StringResourceVisualizer.Tests
{
    [TestClass]
    public class StringExtensionsTests
    {
        [TestMethod]
        public void IsSolutionFileName_Sln()
        {
            TestIt(@"C:\Source\MyApp\MyApp.sln", true);
        }

        [TestMethod]
        public void IsSolutionFileName_Slnx()
        {
            TestIt(@"C:\Source\MyApp\MyApp.slnx", true);
        }

        [TestMethod]
        public void IsSolutionFileName_Sln_UpperCase()
        {
            TestIt(@"C:\Source\MyApp\MyApp.SLN", true);
        }

        [TestMethod]
        public void IsSolutionFileName_Slnx_MixedCase()
        {
            TestIt(@"C:\Source\MyApp\MyApp.SlnX", true);
        }

        [TestMethod]
        public void IsSolutionFileName_ProjectFile()
        {
            TestIt(@"C:\Source\MyApp\MyApp.csproj", false);
        }

        [TestMethod]
        public void IsSolutionFileName_SolutionFilter()
        {
            TestIt(@"C:\Source\MyApp\MyApp.slnf", false);
        }

        [TestMethod]
        public void IsSolutionFileName_ExtensionInTheMiddleOfThePath()
        {
            TestIt(@"C:\Source\MyApp.sln\readme.txt", false);
        }

        [TestMethod]
        public void IsSolutionFileName_Empty()
        {
            TestIt(string.Empty, false);
        }

        [TestMethod]
        public void IsSolutionFileName_Null()
        {
            TestIt(null, false);
        }

        private void TestIt(string input, bool expected)
        {
            var actual = input.IsSolutionFileName();

            Assert.AreEqual(expected, actual);
        }
    }
}
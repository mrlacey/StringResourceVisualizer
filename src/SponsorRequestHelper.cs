// <copyright file="SponsorRequestHelper.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Threading.Tasks;

namespace StringResourceVisualizer
{
    public class SponsorRequestHelper
    {
        public static async Task CheckIfNeedToShowAsync()
        {
            if (await SponsorDetector.IsSponsorAsync())
            {
                if (new Random().Next(1, 10) == 2)
                {
                    ShowThanksForSponsorshipMessage();
                }
            }
            else
            {
                ShowPromptForSponsorship();
            }
        }

        private static void ShowThanksForSponsorshipMessage()
        {
            GeneralOutputPane.Instance.WriteLine("Thank you for your sponsorship. It really helps.");
            GeneralOutputPane.Instance.WriteLine("If you have ideas for new features or suggestions for new features");
            GeneralOutputPane.Instance.WriteLine("please raise an issue at https://github.com/mrlacey/StringResourceVisualizer/issues");
            GeneralOutputPane.Instance.WriteLine(string.Empty);
        }

        private static void ShowPromptForSponsorship()
        {
            GeneralOutputPane.Instance.WriteLine("Sorry to interrupt. I know your time is busy, presumably that's why you installed this extension.");
            GeneralOutputPane.Instance.WriteLine("I'm happy that the extensions I've created have been able to help you and many others");
            GeneralOutputPane.Instance.WriteLine("but I also need to make a living, and two years without work and extended periods of illness have been a challenge. - I didn't qualify for any government support either. :(");
            GeneralOutputPane.Instance.WriteLine(string.Empty);
            GeneralOutputPane.Instance.WriteLine("Show your support by making a one-off or recurring donation at https://github.com/sponsors/mrlacey");
            GeneralOutputPane.Instance.WriteLine(string.Empty);
            GeneralOutputPane.Instance.WriteLine("If you become a sponsor, I'll tell you how to hide this message too. ;)");
            GeneralOutputPane.Instance.WriteLine(string.Empty);
            GeneralOutputPane.Instance.Activate();
        }
    }
}

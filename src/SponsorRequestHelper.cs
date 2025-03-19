// <copyright file="SponsorRequestHelper.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

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
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    await ShowThanksForSponsorshipMessageAsync();
                }
            }
            else
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await ShowPromptForSponsorshipAsync();
            }
        }

        private static async Task ShowThanksForSponsorshipMessageAsync()
        {
            await OutputPane.Instance.WriteAsync("Thank you for your sponsorship. It really helps.");
            await OutputPane.Instance.WriteAsync("If you have ideas for new features or suggestions for new features");
            await OutputPane.Instance.WriteAsync("please raise an issue at https://github.com/mrlacey/StringResourceVisualizer/issues");
            await OutputPane.Instance.WriteAsync(string.Empty);
        }

        private static async Task ShowPromptForSponsorshipAsync()
		{
			await OutputPane.Instance.WriteAsync("********************************************************************************************************");
			await OutputPane.Instance.WriteAsync("This is a free extension that is made possible thanks to the kind and generous donations of:");
			await OutputPane.Instance.WriteAsync("");
			await OutputPane.Instance.WriteAsync("Daniel, James, Mike, Bill, unicorns39283, Martin, Richard, Alan, Howard, Mike, Dave, Joe, ");
			await OutputPane.Instance.WriteAsync("Alvin, Anders, Melvyn, Nik, Kevin, Richard, Orien, Shmueli, Gabriel, Martin, Neil, Daniel, ");
			await OutputPane.Instance.WriteAsync("Victor, Uno, Paula, Tom, Nick, Niki, chasingcode, luatnt, holeow, logarrhythmic, kokolorix, ");
			await OutputPane.Instance.WriteAsync("Guiorgy, Jessé, pharmacyhalo, MXM-7, atexinspect, João, hals1010, WTD-leachA, andermikael, ");
			await OutputPane.Instance.WriteAsync("spudwa, Cleroth, relentless-dev-purchases & 20+ more");
			await OutputPane.Instance.WriteAsync("");
			await OutputPane.Instance.WriteAsync("Join them to show you appreciation and ensure future maintenance and development by becoming a sponsor.");
			await OutputPane.Instance.WriteAsync("");
			await OutputPane.Instance.WriteAsync("Go to https://github.com/sponsors/mrlacey");
			await OutputPane.Instance.WriteAsync("");
			await OutputPane.Instance.WriteAsync("Any amount, as either a one-off or on a monthly basis, is appreciated more than you can imagine.");
			await OutputPane.Instance.WriteAsync("");
			await OutputPane.Instance.WriteAsync("I'll also tell you how to hide this message too.  ;)");
			await OutputPane.Instance.WriteAsync("");
			await OutputPane.Instance.WriteAsync("");
			await OutputPane.Instance.WriteAsync("If you can't afford to support financially, you can always");
			await OutputPane.Instance.WriteAsync("leave a positive review at https://marketplace.visualstudio.com/items?itemName=MattLaceyLtd.StringResourceVisualizer&ssr=false#review-details");
			await OutputPane.Instance.WriteAsync("");
			await OutputPane.Instance.WriteAsync("********************************************************************************************************");
		}
    }
}

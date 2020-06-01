// <copyright file="MyRunningDocTableEvents.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace StringResourceVisualizer
{
    internal class MyRunningDocTableEvents : IVsRunningDocTableEvents
    {
        private static MyRunningDocTableEvents instance;

        private MyRunningDocTableEvents()
        {
        }

        public static MyRunningDocTableEvents Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MyRunningDocTableEvents();
                }

                return instance;
            }
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () => await ConstFinder.ReloadConstsAsync());

            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () => await ConstFinder.ReloadConstsAsync());

            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }
    }
}

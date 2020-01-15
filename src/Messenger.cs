// <copyright file="Messenger.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

namespace StringResourceVisualizer
{
    public static class Messenger
    {
        public delegate void ReloadResourcesEventHandler();

        public static event ReloadResourcesEventHandler ReloadResources;

        public static void RequestReloadResources()
        {
            System.Diagnostics.Debug.WriteLine("RequestReloadResources");
            ReloadResources?.Invoke();
        }
    }
}

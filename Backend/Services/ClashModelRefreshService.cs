using System;
using System.Threading;
using Autodesk.Navisworks.Api;

namespace NavPro.Backend.Services
{
    /// <summary>Refreshes changed model files already referenced by the active Navisworks document.</summary>
    public sealed class ClashModelRefreshService
    {
        private readonly Document document;
        private readonly int hostThreadId;

        public ClashModelRefreshService(Document document)
        {
            this.document = document ?? throw new ArgumentNullException(nameof(document));
            hostThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public bool RefreshChangedFiles()
        {
            CheckHost();
            if (document.Models.Count == 0)
                throw new InvalidOperationException("Open or append at least one model before refreshing files.");

            // Navisworks refreshes existing references whose source files changed on disk.
            // This does not discover or append new files, or remap changed file paths.
            return document.UpdateFiles();
        }

        private void CheckHost()
        {
            if (Thread.CurrentThread.ManagedThreadId != hostThreadId)
                throw new InvalidOperationException("Navisworks model refresh must run on the host UI thread.");
        }
    }
}

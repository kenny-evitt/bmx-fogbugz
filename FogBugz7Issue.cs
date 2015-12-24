using System;
using Inedo.BuildMaster.Extensibility.Providers.IssueTracking;

namespace Inedo.BuildMasterExtensions.FogBugz
{
    [Serializable]
    internal sealed class FogBugz7Issue : IssueTrackerIssue
    {
        public FogBugz7Issue(string id, string status, string title, string description, string release, bool isResolved)
            : base(id, status, title, description, release)
        {
            this.IsResolved = isResolved;
        }

        public bool IsResolved { get; private set; }
    }
}

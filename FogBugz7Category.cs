﻿using System;
using Inedo.BuildMaster.Extensibility.Providers.IssueTracking;

namespace Inedo.BuildMasterExtensions.FogBugz
{
    [Serializable]
    internal sealed class FogBugz7Category : IssueTrackerCategory
    {
        public FogBugz7Category(string id, string name)
            : base(id, name, null)
        {
        }
    }
}

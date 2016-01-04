using System;
using Inedo.BuildMaster.Extensibility.IssueTrackerConnections;
using Inedo.BuildMaster.Extensibility.Providers.IssueTracking;

namespace Inedo.BuildMasterExtensions.FogBugz
{
    [Serializable]
    internal sealed class FogBugz7Issue : IssueTrackerIssue, IIssueTrackerIssue
    {
        private readonly FogBugz7Provider _provider;
        private readonly DateTime _dateTimeOpened;
        private readonly string _submitter;

        public FogBugz7Issue(string id, string status, string title, string description, string release, bool isResolved, FogBugz7Provider fogBugzProvider, string dateTimeOpened, string submitter)
            : base(id, status, title, description, release)
        {
            this.IsResolved = isResolved;
            _provider = fogBugzProvider;
            _dateTimeOpened = DateTime.Parse(dateTimeOpened);
            _submitter = submitter;
        }

        public string Description
        {
            get
            {
                return null;
            }
        }

        public string Id
        {
            get
            {
                return this.IssueId;
            }
        }

        public bool IsClosed
        {
            get
            {
                return this.IsResolved;
            }
        }

        public bool IsResolved { get; private set; }

        public DateTime SubmittedDate
        {
            get
            {
                return _dateTimeOpened;
            }
        }

        public string Status
        {
            get
            {
                return this.IssueStatus;
            }
        }

        public string Submitter
        {
            get
            {
                return _submitter;
            }
        }

        public string Title
        {
            get
            {
                return this.IssueTitle;
            }
        }

        public string Url
        {
            get
            {
                var url = new Uri(new Uri(_provider.FogBugzApiUrl), "default.asp?" + this.IssueId).ToString();
                return url.ToString();
            }
        }
    }
}

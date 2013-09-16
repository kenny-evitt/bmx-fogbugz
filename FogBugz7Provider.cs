using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Extensibility.Providers.IssueTracking;
using Inedo.BuildMaster.Web;

namespace Inedo.BuildMasterExtensions.FogBugz
{
    /// <summary>
    /// Connects to FogBugz 7 or newer.
    /// </summary>
    [ProviderProperties(
        "FogBugz",
        "Supports FogBugz 7 or newer.")]
    [CustomEditor(typeof(FogBugz7ProviderEditor))]
    public sealed class FogBugz7Provider : IssueTrackingProviderBase, ICategoryFilterable, IUpdatingProvider, IReleaseNumberCreator, IReleaseNumberCloser
    {
        /// <summary>
        /// Tested version of FogBugz.
        /// </summary>
        private const int SupportedVersion = 7;

        private WeakReference webClient;
        private readonly object webClientLock = new object();
        private string fullApiUrl;

        /// <summary>
        /// Initializes a new instance of the <see cref="FogBugz7Provider"/> class.
        /// </summary>
        public FogBugz7Provider()
        {
        }

        /// <summary>
        /// Gets or sets the URL of the FogBugz API.
        /// </summary>
        /// <remarks>
        /// This is usually something like: http://fogbugz/api.xml
        /// </remarks>
        [Persistent]
        public string FogBugzApiUrl { get; set; }
        /// <summary>
        /// Gets or sets the e-mail address of the user used to log in.
        /// </summary>
        [Persistent]
        public string UserEmail { get; set; }
        /// <summary>
        /// Gets or sets the password of the user used to log in.
        /// </summary>
        [Persistent]
        public string Password { get; set; }
        /// <summary>
        /// Gets or sets the category ID filter.
        /// </summary>
        public string[] CategoryIdFilter { get; set; }
        /// <summary>
        /// Gets an inheritor-defined array of category types.
        /// </summary>
        public string[] CategoryTypeNames
        {
            get { return new[] { "Project" }; }
        }
        /// <summary>
        /// Gets a value indicating whether an issue's description can be appended to.
        /// </summary>
        public bool CanAppendIssueDescriptions
        {
            get { return false; }
        }
        /// <summary>
        /// Gets a value indicating whether an issue's status can be changed.
        /// </summary>
        public bool CanChangeIssueStatuses
        {
            get { return false; }
        }
        /// <summary>
        /// Gets a value indicating whether an issue can be closed.
        /// </summary>
        public bool CanCloseIssues
        {
            get { return true; }
        }

        /// <summary>
        /// Gets the current WebClient for accessing FogBugz.
        /// </summary>
        private WebClient ApiClient
        {
            get
            {
                lock (this.webClientLock)
                {
                    WebClient client;

                    if (this.webClient == null)
                    {
                        client = new WebClient();
                        this.webClient = new WeakReference(client);
                    }
                    else
                    {
                        client = this.webClient.Target as WebClient;
                        if (client == null)
                        {
                            client = new WebClient();
                            this.webClient = new WeakReference(client);
                        }
                    }

                    return client;
                }
            }
        }
        /// <summary>
        /// Gets the actual URL of the FogBugz API.
        /// </summary>
        private string ActualApiUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(this.fullApiUrl))
                    return this.fullApiUrl;

                this.fullApiUrl = GetApiUrl();
                return this.fullApiUrl;
            }
        }

        /// <summary>
        /// Gets a URL to the specified issue.
        /// </summary>
        /// <param name="issue">The issue whose URL is returned.</param>
        /// <returns>
        /// The URL of the specified issue if applicable; otherwise null.
        /// </returns>
        public override string GetIssueUrl(IssueTrackerIssue issue)
        {
            if (issue == null)
                return null;

            var url = new Uri(new Uri(this.FogBugzApiUrl), "default.asp?" + issue.IssueId).ToString();
            return url.ToString();
        }
        /// <summary>
        /// Returns an array of <see cref="Issue"/> objects that are for the current
        /// release.
        /// </summary>
        /// <param name="releaseNumber">Release number of issues to return.</param>
        /// <returns>
        /// Array of issues for the specified release.
        /// </returns>
        public override IssueTrackerIssue[] GetIssues(string releaseNumber)
        {
            var token = LogOn();
            try
            {
                var resolvedStatuses = GetIssueResolvedTable(token);

                var projectQuery = "";
                if (this.CategoryIdFilter != null && this.CategoryIdFilter.Length > 0 && !string.IsNullOrEmpty(this.CategoryIdFilter[0]))
                {
                    var projectInfo = Api(
                        "viewProject",
                        new Dictionary<string, string>
                        {
                            { "token", token },
                            { "ixProject", this.CategoryIdFilter[0] }
                        });

                    var projectNameNode = projectInfo.SelectSingleNode("/response/project/sProject");
                    if (projectNameNode != null && !string.IsNullOrEmpty(projectNameNode.InnerText))
                        projectQuery = " project:\"" + projectNameNode.InnerText + "\""; 
                }

                var issuesResponse = Api(
                    "search",
                    new Dictionary<string, string>
                    {
                        { "token", token },
                        { "q", "milestone:\"" + releaseNumber + "\"" + projectQuery },
                        { "cols", "sTitle,sLatestTextSummary,ixStatus,sStatus" }
                    });

                var issues = new List<FogBugz7Issue>();
                var issueNodes = issuesResponse.SelectNodes("/response/cases/case");
                foreach (XmlElement issueNode in issueNodes)
                {
                    int statusId = int.Parse(issueNode.SelectSingleNode("ixStatus").InnerText);

                    issues.Add(new FogBugz7Issue(
                        issueNode.GetAttribute("ixBug"),
                        issueNode.SelectSingleNode("sStatus").InnerText,
                        issueNode.SelectSingleNode("sTitle").InnerText,
                        issueNode.SelectSingleNode("sLatestTextSummary").InnerText ?? "",
                        releaseNumber,
                        resolvedStatuses[statusId]));
                }

                return issues.ToArray();
            }
            finally
            {
                LogOff(token);
            }
        }
        /// <summary>
        /// Returns a value indicating if the specified issue is closed.
        /// </summary>
        /// <param name="issue">Issue to check for a closed state.</param>
        /// <returns>
        /// True if issue is closed; otherwise false.
        /// </returns>
        public override bool IsIssueClosed(IssueTrackerIssue issue)
        {
            return ((FogBugz7Issue)issue).IsResolved;
        }
        /// <summary>
        /// When implemented in a derived class, indicates whether the provider
        /// is installed and available for use in the current execution context.
        /// </summary>
        /// <returns></returns>
        public override bool IsAvailable()
        {
            return true;
        }
        /// <summary>
        /// When implemented in a derived class, attempts to connect with the
        /// current configuration and, if not successful, throws a
        /// <see cref="ConnectionException"/>.
        /// </summary>
        public override void ValidateConnection()
        {
            string token = null;
            try
            {
                token = LogOn();
            }
            catch (WebException ex)
            {
                throw new NotAvailableException(string.Format("Unable to connect to FogBugz. Verify that the URL is correct and accessible via the browser. Full error: {0}", ex.Message), ex);
            }
            finally
            {
                if(token != null)
                    LogOff(token);
            }
        }
        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return "Connects to FogBugz v7 or later.";
        }
        /// <summary>
        /// Returns an array of all appropriate categories defined within the provider.
        /// </summary>
        /// <returns></returns>
        public IssueTrackerCategory[] GetCategories()
        {
            var token = LogOn();
            try
            {
                var projects = new List<IssueTrackerCategory>();
                var projectsResponse = Api(
                    "listProjects",
                    new Dictionary<string, string>
                    {
                        { "token", token }
                    });

                var projectNodes = projectsResponse.SelectNodes("/response/projects/project");
                foreach (XmlElement projectNode in projectNodes)
                {
                    var projectId = projectNode.SelectSingleNode("ixProject").InnerText;
                    var projectName = projectNode.SelectSingleNode("sProject").InnerText;
                    projects.Add(new IssueTrackerCategory(projectId, projectName));
                }

                return projects.ToArray();
            }
            finally
            {
                LogOff(token);
            }
        }
        /// <summary>
        /// Appends the specified text to the specified issue.
        /// </summary>
        /// <param name="issueId">Id of the issue.</param>
        /// <param name="textToAppend">Text to append to the issue description.</param>
        public void AppendIssueDescription(string issueId, string textToAppend)
        {
            var token = LogOn();
            try
            {
                Api(
                    "edit",
                    new Dictionary<string, string>
                    {
                        { "token", token },
                        { "ixBug", issueId },
                        { "sEvent", textToAppend }
                    });
            }
            finally
            {
                LogOff(token);
            }
        }
        /// <summary>
        /// Changes the specified issue's status
        /// </summary>
        /// <param name="issueId">Id of the issue.</param>
        /// <param name="newStatus">New status of the issue.</param>
        public void ChangeIssueStatus(string issueId, string newStatus)
        {
            var token = LogOn();
            try
            {
                var statuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (XmlElement statusNode 
                    in Api(
                        "listStatuses",
                        new Dictionary<string, string>
                        {
                            { "token", token }
                        })
                        .SelectNodes("/response/statuses/status"))
                    statuses[statusNode.SelectSingleNode("sStatus").InnerText] = statusNode.SelectSingleNode("ixStatus").InnerText;

                if (statuses.ContainsKey(newStatus))
                {
                    Api(
                        "resolve",
                        new Dictionary<string, string>
                        {
                            { "token", token },
                            { "ixBug", issueId },
                            { "ixStatus", statuses[newStatus] }
                        });
                }
                else 
                {
                    throw new ArgumentOutOfRangeException(newStatus + " is not a valid FogBugz status. Expected one of: ",
                        string.Join("; ", new List<string>(statuses.Keys).ToArray()));
                }
            }
            finally
            {
                LogOff(token);
            }    
        }
        /// <summary>
        /// Closes the specified issue.
        /// </summary>
        /// <param name="issueId">Id of the issue.</param>
        public void CloseIssue(string issueId)
        {
            var token = LogOn();
            try
            {
                Api(
                    "close",
                    new Dictionary<string, string>
                    {
                        { "token", token },
                        { "ixBug", issueId }
                    });
            }
            finally
            {
                LogOff(token);
            }
        }
        /// <summary>
        /// Creates the specified release number at the source
        /// </summary>
        /// <param name="releaseNumber"></param>
        public void CreateReleaseNumber(string releaseNumber)
        {
            if (this.CategoryIdFilter == null || this.CategoryIdFilter.Length < 1 || string.IsNullOrEmpty(this.CategoryIdFilter[0]))
                return;

            var token = LogOn();
            try
            {
                Api(
                    "newFixFor",
                    new Dictionary<string, string>
                    {
                        { "token", token },
                        { "ixProject", this.CategoryIdFilter[0] },
                        { "sFixFor", releaseNumber },
                        { "fAssignable", "1" }
                    });
            }
            finally
            {
                LogOff(token);
            }
        }
        /// <summary>
        /// Closes the release number.
        /// </summary>
        /// <param name="releaseNumber">The release number.</param>
        public void CloseReleaseNumber(string releaseNumber)
        {
            if (this.CategoryIdFilter == null || this.CategoryIdFilter.Length < 1 || string.IsNullOrEmpty(this.CategoryIdFilter[0]))
                return;

            var token = LogOn();

            try
            {
                var viewMilestoneResponse =
                    Api(
                        "viewFixFor",
                        new Dictionary<string, string>
                        {
                            { "token", token },
                            { "ixProject", this.CategoryIdFilter[0] },
                            { "sFixFor", releaseNumber }
                        });

                var milestoneIdNode = (XmlElement)viewMilestoneResponse.SelectSingleNode("/response/fixfor/ixFixFor");

                // If the response node is empty then either the milestone doesn't exist or it's already been inactivated (or deleted).

                if (milestoneIdNode != null)
                {
                    var milestoneId = milestoneIdNode.InnerText;

                    Api(
                        "editFixFor",
                        new Dictionary<string, string>
                        {
                            { "token", token },
                            { "ixFixFor", milestoneId },
                            { "sFixFor", releaseNumber },
                            { "fAssignable", "0" }
                        });
                }
            }
            finally
            {
                LogOff(token);
            }
        }

        /// <summary>
        /// Logs on to the FogBugz API.
        /// </summary>
        /// <returns>Token for the session.</returns>
        private string LogOn()
        {
            var tokenResponse = Api(
                "logon",
                new Dictionary<string, string>
                {
                    { "email", this.UserEmail },
                    { "password", this.Password }
                });

            var tokenNode = (XmlElement)tokenResponse.SelectSingleNode("/response/token");
            if (tokenNode == null)
                throw new InvalidOperationException("Expected token in FogBugz API response.");

            return tokenNode.InnerText;
        }
        /// <summary>
        /// Logs off of the FogBugz API.
        /// </summary>
        /// <param name="token">Token of session to end.</param>
        private void LogOff(string token)
        {
            try
            {
                Api(
                    "logoff",
                    new Dictionary<string, string>
                    {
                        { "token", token }
                    });
            }
            catch
            {
            }
        }
        /// <summary>
        /// Invokes a command on the FogBugz API.
        /// </summary>
        /// <param name="command">Command to invoke.</param>
        /// <param name="args">Arguments to pass on the query string.</param>
        /// <returns>Response of the command.</returns>
        private XmlDocument Api(string command, IEnumerable<KeyValuePair<string, string>> args)
        {
            var query = new StringBuilder();

            if (!string.IsNullOrEmpty(command))
            {
                query.Append("?cmd=");
                query.Append(HttpUtility.UrlEncode(command));
            }

            if (args != null)
            {
                foreach (var arg in args)
                {
                    query.Append(query.Length > 0 ? '&' : '?');
                    query.Append(HttpUtility.UrlEncode(arg.Key));
                    query.Append('=');
                    query.Append(HttpUtility.UrlEncode(arg.Value));
                }
            }

            var response = this.ApiClient.DownloadString(this.ActualApiUrl + query.ToString());
            var responseXml = new XmlDocument();
            responseXml.LoadXml(response);
            var errorNode = (XmlElement)responseXml.SelectSingleNode("/response/error");
            if (errorNode != null)
                throw new InvalidOperationException(string.Format("FogBugz returned error code {0}: {1}", errorNode.GetAttribute("code"), errorNode.InnerText));

            return responseXml;
        }
        /// <summary>
        /// Returns a string containing the actual FogBugz API URL.
        /// </summary>
        /// <returns>String containing the FogBugz API URL.</returns>
        private string GetApiUrl()
        {
            var xmlUrl = this.FogBugzApiUrl;
            if (!xmlUrl.EndsWith("api.xml", StringComparison.OrdinalIgnoreCase))
                xmlUrl = new Uri(new Uri(xmlUrl), "api.xml").ToString();

            var response = this.ApiClient.DownloadString(this.FogBugzApiUrl);
            var responseXml = new XmlDocument();
            responseXml.LoadXml(response);

            var minVersion = (XmlElement)responseXml.SelectSingleNode("/response/minversion");
            if (minVersion == null || string.IsNullOrEmpty(minVersion.InnerText))
                throw new InvalidOperationException("Error parsing response: expected minversion element not found.");

            if (int.Parse(minVersion.InnerText) > SupportedVersion)
            {
                var version = (XmlElement)responseXml.SelectSingleNode("/response/version");
                throw new NotSupportedException(string.Format("FogBugz version {0} is not supported by this provider. Please contact support.", version.InnerText));
            }

            var apiUrl = (XmlElement)responseXml.SelectSingleNode("/response/url");
            if (apiUrl == null)
                throw new InvalidOperationException("Error parsing response: expected url element not found.");

            var fullUrl = new Uri(new Uri(this.FogBugzApiUrl), apiUrl.InnerText.TrimEnd('?'));
            return fullUrl.ToString();
        }
        /// <summary>
        /// Returns a table containing issue statuses.
        /// </summary>
        /// <param name="token">Token of the current session.</param>
        /// <returns>Table containing issue resolutions.</returns>
        private Dictionary<int, bool> GetIssueResolvedTable(string token)
        {
            var statusNodes = Api(
                "listStatuses",
                new Dictionary<string, string>
                {
                    { "token", token },
                });

            var statuses = new Dictionary<int, bool>();
            foreach (XmlElement statusNode in statusNodes.SelectNodes("/response/statuses/status"))
            {
                int id = int.Parse(statusNode.SelectSingleNode("ixStatus").InnerText);
                bool resolved = bool.Parse(statusNode.SelectSingleNode("fResolved").InnerText);
                statuses[id] = resolved;
            }

            return statuses;
        }
    }
}

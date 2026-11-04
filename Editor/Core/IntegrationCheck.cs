using System.Collections.Generic;

namespace AdMobMediationManager.Editor
{
    /// <summary>How a package was added to the project.</summary>
    internal enum InstallSource
    {
        /// <summary>Not installed.</summary>
        None,

        /// <summary>Through the Unity Package Manager (for example from OpenUPM).</summary>
        PackageManager,

        /// <summary>By importing a <c>.unitypackage</c> into the Assets folder.</summary>
        UnityPackage,
    }

    /// <summary>An adapter found in the project.</summary>
    internal sealed class InstalledAdapter
    {
        public InstalledAdapter(AdapterInfo adapter, InstallSource source, string version)
        {
            Adapter = adapter;
            Source = source;
            Version = version ?? string.Empty;
        }

        public AdapterInfo Adapter { get; }

        public InstallSource Source { get; }

        /// <summary>The Unity plugin version, or empty when it cannot be told (common for .unitypackage installs).</summary>
        public string Version { get; }
    }

    /// <summary>
    /// Everything the integration check needs to know about the project, gathered by the Editor code
    /// (<see cref="ProjectScanner"/>) so that the rules below can be tested without Unity.
    /// </summary>
    internal sealed class ProjectSnapshot
    {
        public InstallSource GoogleMobileAdsSource = InstallSource.None;
        public string GoogleMobileAdsVersion = string.Empty;
        public bool ExternalDependencyManagerFound;

        /// <summary>False when <c>GoogleMobileAdsSettings.asset</c> does not exist yet.</summary>
        public bool SettingsAssetFound;

        public string AndroidAppId = string.Empty;
        public string IOSAppId = string.Empty;
        public string UserTrackingUsageDescription = string.Empty;

        /// <summary>True when the active build target or the installed modules include Android.</summary>
        public bool TargetsAndroid = true;

        /// <summary>True when the active build target or the installed modules include iOS.</summary>
        public bool TargetsIOS = true;

        public List<InstalledAdapter> Adapters = new List<InstalledAdapter>();
    }

    /// <summary>How serious an <see cref="Issue"/> is.</summary>
    internal enum Severity
    {
        /// <summary>Good to know; nothing is broken.</summary>
        Info,

        /// <summary>Works now but may cause trouble, for example before a release.</summary>
        Warning,

        /// <summary>Ads will not work, or the build will fail, until this is fixed.</summary>
        Error,
    }

    /// <summary>Identifies each kind of finding, so the window can offer a matching fix button.</summary>
    internal enum IssueCode
    {
        GoogleMobileAdsMissing,
        ExternalDependencyManagerMissing,
        SettingsAssetMissing,
        AndroidAppIdMissing,
        AndroidAppIdInvalid,
        AndroidAppIdIsTest,
        IOSAppIdMissing,
        IOSAppIdInvalid,
        IOSAppIdIsTest,
        TrackingDescriptionMissing,
        AdapterInstalledTwice,
        NoAdapters,
    }

    /// <summary>One finding of the integration check.</summary>
    internal sealed class Issue
    {
        public Issue(IssueCode code, Severity severity, string message, AdapterInfo adapter = null)
        {
            Code = code;
            Severity = severity;
            Message = message;
            Adapter = adapter;
        }

        public IssueCode Code { get; }

        public Severity Severity { get; }

        /// <summary>A sentence that says what is wrong and how to fix it.</summary>
        public string Message { get; }

        /// <summary>The adapter the issue is about, when it is about one.</summary>
        public AdapterInfo Adapter { get; }
    }

    /// <summary>
    /// The rules behind the Integration Status tab. Each rule looks at a <see cref="ProjectSnapshot"/>
    /// and reports problems that commonly stop mediated ads from showing.
    /// </summary>
    internal static class IntegrationCheck
    {
        /// <summary>Runs every rule and returns the findings, most serious first.</summary>
        public static List<Issue> Run(ProjectSnapshot s)
        {
            var issues = new List<Issue>();

            if (s.GoogleMobileAdsSource == InstallSource.None)
            {
                issues.Add(new Issue(IssueCode.GoogleMobileAdsMissing, Severity.Error,
                    "The Google Mobile Ads Unity plugin is not installed. Every mediation adapter needs it."));
            }
            else
            {
                if (!s.ExternalDependencyManagerFound)
                {
                    issues.Add(new Issue(IssueCode.ExternalDependencyManagerMissing, Severity.Warning,
                        "External Dependency Manager (EDM4U) was not found. Without it the native Android and iOS SDKs of the adapters are not downloaded."));
                }

                if (!s.SettingsAssetFound)
                {
                    issues.Add(new Issue(IssueCode.SettingsAssetMissing, Severity.Error,
                        "GoogleMobileAdsSettings.asset does not exist yet. Open the App Settings tab and save the App IDs to create it."));
                }
                else
                {
                    if (s.TargetsAndroid)
                    {
                        AddAppIdIssues(issues, s.AndroidAppId, "Android",
                            IssueCode.AndroidAppIdMissing, IssueCode.AndroidAppIdInvalid, IssueCode.AndroidAppIdIsTest);
                    }

                    if (s.TargetsIOS)
                    {
                        AddAppIdIssues(issues, s.IOSAppId, "iOS",
                            IssueCode.IOSAppIdMissing, IssueCode.IOSAppIdInvalid, IssueCode.IOSAppIdIsTest);

                        if (string.IsNullOrWhiteSpace(s.UserTrackingUsageDescription))
                        {
                            issues.Add(new Issue(IssueCode.TrackingDescriptionMissing, Severity.Info,
                                "No App Tracking Transparency usage description is set. Add one if the app asks for tracking permission on iOS."));
                        }
                    }
                }
            }

            var seen = new Dictionary<AdapterInfo, InstallSource>();
            var reported = new HashSet<AdapterInfo>();
            foreach (InstalledAdapter installed in s.Adapters)
            {
                InstallSource other;
                if (seen.TryGetValue(installed.Adapter, out other) && other != installed.Source && reported.Add(installed.Adapter))
                {
                    issues.Add(new Issue(IssueCode.AdapterInstalledTwice, Severity.Error,
                        installed.Adapter.DisplayName + " is installed both as a package and as a .unitypackage. Remove one of them to avoid duplicate classes.",
                        installed.Adapter));
                }

                seen[installed.Adapter] = installed.Source;
            }

            if (s.GoogleMobileAdsSource != InstallSource.None && s.Adapters.Count == 0)
            {
                issues.Add(new Issue(IssueCode.NoAdapters, Severity.Info,
                    "No mediation adapters are installed, so only AdMob demand will serve. Add networks in the Networks tab."));
            }

            // Stable sort: errors first, then warnings, then info, each in rule order.
            var sorted = new List<Issue>(issues.Count);
            foreach (Severity severity in new[] { Severity.Error, Severity.Warning, Severity.Info })
            {
                foreach (Issue issue in issues)
                {
                    if (issue.Severity == severity)
                    {
                        sorted.Add(issue);
                    }
                }
            }

            return sorted;
        }

        private static void AddAppIdIssues(List<Issue> issues, string appId, string platform,
            IssueCode missing, IssueCode invalid, IssueCode test)
        {
            switch (AdMobIdValidator.CheckAppId(appId))
            {
                case IdCheck.Empty:
                    issues.Add(new Issue(missing, Severity.Error,
                        "The " + platform + " App ID is empty. The app will crash on launch when the SDK starts without it."));
                    break;
                case IdCheck.WrongKind:
                    issues.Add(new Issue(invalid, Severity.Error,
                        "The " + platform + " App ID looks like an ad unit ID ('/'). App IDs contain '~' instead."));
                    break;
                case IdCheck.Malformed:
                    issues.Add(new Issue(invalid, Severity.Error,
                        "The " + platform + " App ID is not in the form ca-app-pub-XXXXXXXXXXXXXXXX~NNNNNNNNNN."));
                    break;
                case IdCheck.TestId:
                    issues.Add(new Issue(test, Severity.Warning,
                        "The " + platform + " App ID is Google's sample ID. Replace it with your own before releasing."));
                    break;
            }
        }
    }
}

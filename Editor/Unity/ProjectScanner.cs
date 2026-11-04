using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AdMobMediationManager.Editor
{
    /// <summary>The state of one adapter as shown in the Networks tab.</summary>
    internal sealed class AdapterRow
    {
        public AdapterInfo Info;

        /// <summary>How the adapter is installed. Package Manager wins when both kinds are present.</summary>
        public InstallSource Source = InstallSource.None;

        /// <summary>True when the adapter is installed both as a package and as a .unitypackage.</summary>
        public bool Duplicate;

        /// <summary>Installed Unity plugin version (empty when unknown).</summary>
        public string Version = string.Empty;

        /// <summary>Latest version on the registry (empty when unknown).</summary>
        public string Latest = string.Empty;

        /// <summary>Project path of the .unitypackage install, when there is one.</summary>
        public string LegacyFolder;

        /// <summary>Native SDKs the adapter pulls in, read from its dependencies file.</summary>
        public List<NativeDependency> Native = new List<NativeDependency>();

        public bool Installed
        {
            get { return Source != InstallSource.None; }
        }

        public bool UpdateAvailable
        {
            get { return Source == InstallSource.PackageManager && PackageVersion.IsUpdateAvailable(Version, Latest); }
        }

        /// <summary>True when the adapter can be installed through the Package Manager.</summary>
        public bool HasPackage
        {
            get { return !string.IsNullOrEmpty(Info.PackageName); }
        }
    }

    /// <summary>What <see cref="ProjectScanner.Scan"/> found.</summary>
    internal sealed class ScanResult
    {
        public ProjectSnapshot Snapshot = new ProjectSnapshot();
        public AdapterRow GoogleMobileAds;
        public AdapterRow ExternalDependencyManager;
        public List<AdapterRow> Adapters = new List<AdapterRow>();
    }

    /// <summary>
    /// Looks at the project (packages, the Assets folder and the Google Mobile Ads settings) and
    /// turns what it finds into a <see cref="ScanResult"/>.
    /// </summary>
    /// <remarks>
    /// Scanning only reads; it never changes the project. It is cheap enough to run whenever the
    /// window gains focus or the asset database changes.
    /// </remarks>
    internal static class ProjectScanner
    {
        /// <summary>Folder used by a .unitypackage install of EDM4U.</summary>
        private const string LegacyEdmFolder = "Assets/ExternalDependencyManager";

        /// <summary>A type that exists in every version of the Google Mobile Ads plugin's API.</summary>
        private const string MobileAdsTypeName = "GoogleMobileAds.Api.MobileAds";

        /// <summary>The EDM4U type that resolves Android dependencies.</summary>
        private const string EdmTypeName = "GooglePlayServices.PlayServicesResolver";

        public static ScanResult Scan(PackageService packages)
        {
            var result = new ScanResult();
            ProjectSnapshot snapshot = result.Snapshot;

            result.GoogleMobileAds = CoreRow(packages, AdapterCatalog.GoogleMobileAdsPackage, "Google Mobile Ads",
                "https://developers.google.com/admob/unity/quick-start", TypeExists(MobileAdsTypeName));
            snapshot.GoogleMobileAdsSource = result.GoogleMobileAds.Source;
            snapshot.GoogleMobileAdsVersion = result.GoogleMobileAds.Version;

            result.ExternalDependencyManager = CoreRow(packages, AdapterCatalog.ExternalDependencyManagerPackage,
                "External Dependency Manager", "https://github.com/googlesamples/unity-jar-resolver",
                Directory.Exists(LegacyEdmFolder) || TypeExists(EdmTypeName));
            snapshot.ExternalDependencyManagerFound = result.ExternalDependencyManager.Installed;

            ReadSettings(snapshot);
            snapshot.TargetsAndroid = TargetsPlatform(BuildTargetGroup.Android, BuildTarget.Android);
            snapshot.TargetsIOS = TargetsPlatform(BuildTargetGroup.iOS, BuildTarget.iOS);

            ScanAdapters(packages, result);
            return result;
        }

        private static AdapterRow CoreRow(PackageService packages, string packageName, string displayName, string docs, bool foundOutsidePackages)
        {
            var row = new AdapterRow { Info = new AdapterInfo(displayName, packageName, docs) };
            PackageInfo info = packages.GetInstalled(packageName);
            if (info != null)
            {
                row.Source = InstallSource.PackageManager;
                row.Version = info.version;
            }
            else if (foundOutsidePackages)
            {
                row.Source = InstallSource.UnityPackage;
            }

            row.Latest = packages.GetLatest(packageName);
            return row;
        }

        private static void ReadSettings(ProjectSnapshot snapshot)
        {
            GoogleMobileAdsSettingsBridge settings = GoogleMobileAdsSettingsBridge.Load();
            snapshot.SettingsAssetFound = settings != null;
            if (settings == null)
            {
                return;
            }

            snapshot.AndroidAppId = settings.AndroidAppId;
            snapshot.IOSAppId = settings.IOSAppId;

            // Older plugin versions have no ATT field; treat it as "not applicable" rather than missing.
            snapshot.UserTrackingUsageDescription = settings.HasTrackingField ? settings.UserTrackingUsageDescription : "-";
        }

        private static void ScanAdapters(PackageService packages, ScanResult result)
        {
            var rows = new Dictionary<string, AdapterRow>(StringComparer.Ordinal);
            foreach (AdapterInfo adapter in AdapterCatalog.Adapters)
            {
                rows[adapter.DisplayName] = new AdapterRow { Info = adapter };
            }

            // Adapters installed as packages, including ones this version of the catalog does not know yet.
            foreach (PackageInfo info in packages.InstalledWithPrefix(AdapterCatalog.AdapterPackagePrefix))
            {
                AdapterInfo adapter = AdapterCatalog.FindByPackage(info.name)
                    ?? new AdapterInfo(ShortName(info.displayName, info.name), info.name, AdapterCatalog.MediationDocsUrl);
                AdapterRow row = GetRow(rows, adapter);
                row.Source = InstallSource.PackageManager;
                row.Version = info.version;
                row.Native.AddRange(ReadNativeDependencies(info.resolvedPath));
                result.Snapshot.Adapters.Add(new InstalledAdapter(row.Info, InstallSource.PackageManager, info.version));
            }

            // Adapters imported from .unitypackage files.
            if (Directory.Exists(AdapterCatalog.LegacyMediationFolder))
            {
                foreach (string folder in Directory.GetDirectories(AdapterCatalog.LegacyMediationFolder))
                {
                    string folderName = Path.GetFileName(folder);
                    AdapterInfo adapter = AdapterCatalog.FindByLegacyFolder(folderName)
                        ?? new AdapterInfo(folderName, string.Empty, AdapterCatalog.MediationDocsUrl, folderName);
                    AdapterRow row = GetRow(rows, adapter);
                    row.Duplicate = row.Source == InstallSource.PackageManager;
                    row.LegacyFolder = folder.Replace('\\', '/');
                    if (row.Source == InstallSource.None)
                    {
                        row.Source = InstallSource.UnityPackage;
                        row.Native.AddRange(ReadNativeDependencies(folder));
                    }

                    result.Snapshot.Adapters.Add(new InstalledAdapter(row.Info, InstallSource.UnityPackage, string.Empty));
                }
            }

            foreach (AdapterRow row in rows.Values)
            {
                if (row.HasPackage)
                {
                    row.Latest = packages.GetLatest(row.Info.PackageName);
                }

                result.Adapters.Add(row);
            }

            result.Adapters.Sort((a, b) => string.Compare(a.Info.DisplayName, b.Info.DisplayName, StringComparison.OrdinalIgnoreCase));
        }

        // Rows are keyed by display name so that a package and a legacy folder of the same network share a row.
        private static AdapterRow GetRow(Dictionary<string, AdapterRow> rows, AdapterInfo adapter)
        {
            AdapterRow row;
            if (!rows.TryGetValue(adapter.DisplayName, out row))
            {
                row = new AdapterRow { Info = adapter };
                rows[adapter.DisplayName] = row;
            }

            return row;
        }

        /// <summary>"Google Mobile Ads Foo Mediation" becomes "Foo".</summary>
        private static string ShortName(string displayName, string packageName)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                return packageName.Substring(AdapterCatalog.AdapterPackagePrefix.Length);
            }

            string name = displayName;
            foreach (string noise in new[] { "Google Mobile Ads ", " Mediation for Unity", " Mediation" })
            {
                name = name.Replace(noise, string.Empty);
            }

            return name.Trim();
        }

        /// <summary>Reads every EDM4U <c>*Dependencies.xml</c> file below <paramref name="root"/>.</summary>
        private static IEnumerable<NativeDependency> ReadNativeDependencies(string root)
        {
            var result = new List<NativeDependency>();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return result;
            }

            try
            {
                foreach (string file in Directory.GetFiles(root, "*Dependencies.xml", SearchOption.AllDirectories))
                {
                    result.AddRange(DependencyFileParser.Parse(File.ReadAllText(file)));
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // A file we cannot read only means fewer details in the window.
            }

            return result;
        }

        private static bool TypeExists(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType(fullName, false) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TargetsPlatform(BuildTargetGroup group, BuildTarget target)
        {
            return EditorUserBuildSettings.activeBuildTarget == target
                || BuildPipeline.IsBuildTargetSupported(group, target);
        }
    }
}

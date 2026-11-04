using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AdMobMediationManager.Editor
{
    /// <summary>
    /// The AdMob Mediation Manager window (<c>Tools &gt; AdMob Mediation Manager</c>).
    /// </summary>
    /// <remarks>
    /// <para>Three tabs:</para>
    /// <list type="bullet">
    /// <item><b>Networks</b>: install, update and remove the Google Mobile Ads plugin, EDM4U and every
    /// mediation adapter, and see which native SDK versions each adapter brings in.</item>
    /// <item><b>App Settings</b>: edit the AdMob App IDs stored by the plugin and manage an
    /// <see cref="AdUnitIdSet"/> asset.</item>
    /// <item><b>Integration Status</b>: a checklist of common mistakes, with one-click fixes.</item>
    /// </list>
    /// <para>
    /// The window is drawn with IMGUI so that it behaves the same on every supported Unity version.
    /// All state that matters lives in the project (packages, assets); the window only caches a
    /// <see cref="ScanResult"/> and rebuilds it whenever the project changes.
    /// </para>
    /// </remarks>
    internal sealed class MediationManagerWindow : EditorWindow
    {
        private const string MenuPath = "Tools/AdMob Mediation Manager";
        private const string ProductName = "AdMob Mediation Manager";

        private static readonly string[] TabNames = { "Networks", "App Settings", "Integration Status" };

        // Serialized so the selected tab and search survive domain reloads (which happen after every install).
        [SerializeField] private int tab;
        [SerializeField] private string search = string.Empty;
        [SerializeField] private Vector2 scroll;
        [SerializeField] private AdUnitIdSet adUnitIdSet;

        private readonly HashSet<string> expanded = new HashSet<string>(StringComparer.Ordinal);
        private PackageService packages;
        private ScanResult scan;
        private List<Issue> issues = new List<Issue>();

        // App Settings edits are kept here until the user presses Save.
        private GoogleMobileAdsSettingsBridge settings;
        private string androidAppId = string.Empty;
        private string iosAppId = string.Empty;
        private string trackingDescription = string.Empty;
        private bool settingsDirty;

        private UnityEditor.Editor adUnitEditor;
        private GUIStyle nameStyle;
        private GUIStyle wrapStyle;

        /// <summary>Opens the window, or focuses it when it is already open.</summary>
        [MenuItem(MenuPath, false, 2000)]
        public static void Open()
        {
            var window = GetWindow<MediationManagerWindow>();
            window.titleContent = new GUIContent(ProductName);
            window.minSize = new Vector2(560, 380);
            window.Show();
        }

        private void OnEnable()
        {
            packages = new PackageService();
            packages.Changed += OnPackagesChanged;
            EditorApplication.projectChanged += Rescan;
            Rescan();
            packages.Refresh();
        }

        private void OnDisable()
        {
            if (packages != null)
            {
                packages.Changed -= OnPackagesChanged;
            }

            EditorApplication.projectChanged -= Rescan;
            if (adUnitEditor != null)
            {
                DestroyImmediate(adUnitEditor);
            }
        }

        private void OnFocus()
        {
            if (packages != null)
            {
                Rescan();
            }
        }

        private void OnPackagesChanged()
        {
            Rescan();
        }

        /// <summary>Rebuilds <see cref="scan"/> and the issue list from the project, then repaints.</summary>
        private void Rescan()
        {
            scan = ProjectScanner.Scan(packages);
            issues = IntegrationCheck.Run(scan.Snapshot);
            if (!settingsDirty)
            {
                LoadSettingsFields();
            }

            Repaint();
        }

        private void LoadSettingsFields()
        {
            settings = GoogleMobileAdsSettingsBridge.Load();
            androidAppId = settings != null ? settings.AndroidAppId : string.Empty;
            iosAppId = settings != null ? settings.IOSAppId : string.Empty;
            trackingDescription = settings != null ? settings.UserTrackingUsageDescription : string.Empty;
            settingsDirty = false;
        }

        // ------------------------------------------------------------------ drawing

        private void OnGUI()
        {
            if (scan == null)
            {
                Rescan();
            }

            InitStyles();
            DrawToolbar();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(4);
            switch (tab)
            {
                case 0: DrawNetworksTab(); break;
                case 1: DrawSettingsTab(); break;
                default: DrawStatusTab(); break;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();
            DrawStatusBar();
        }

        private void InitStyles()
        {
            if (nameStyle != null)
            {
                return;
            }

            nameStyle = new GUIStyle(EditorStyles.boldLabel);
            wrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int errors = issues.FindAll(i => i.Severity == Severity.Error).Count;
                string[] labels = (string[])TabNames.Clone();
                if (errors > 0)
                {
                    labels[2] += " (" + errors + ")";
                }

                tab = GUILayout.Toolbar(tab, labels, EditorStyles.toolbarButton, GUILayout.Width(380));
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(packages.Busy))
                {
                    if (GUILayout.Button(new GUIContent("Refresh", "Re-read packages and check the registry for new versions."), EditorStyles.toolbarButton))
                    {
                        Rescan();
                        packages.Refresh();
                    }
                }
            }
        }

        private void DrawStatusBar()
        {
            string status = packages.Status;
            if (string.IsNullOrEmpty(status))
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(status, wrapStyle);
            }
        }

        // ------------------------------------------------------------------ Networks tab

        private void DrawNetworksTab()
        {
            if (!PackageService.RegistryConfigured())
            {
                EditorGUILayout.HelpBox(
                    "The OpenUPM registry is not in Packages/manifest.json yet, so the latest versions cannot be checked. " +
                    "It is added (after you confirm) the first time you install something.",
                    MessageType.Info);
                if (GUILayout.Button("Add the OpenUPM registry now", GUILayout.Width(220)) && packages.EnsureRegistry())
                {
                    packages.Refresh();
                }

                EditorGUILayout.Space(4);
            }

            Header("SDK");
            DrawRow(scan.GoogleMobileAds, false);
            DrawRow(scan.ExternalDependencyManager, false);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                Header("Mediation networks");
                GUILayout.FlexibleSpace();
                List<AdapterRow> outdated = scan.Adapters.FindAll(r => r.UpdateAvailable);
                using (new EditorGUI.DisabledScope(outdated.Count == 0 || packages.Busy))
                {
                    if (GUILayout.Button("Update all (" + outdated.Count + ")", GUILayout.Width(110)))
                    {
                        var add = new List<string>();
                        foreach (AdapterRow row in outdated)
                        {
                            add.Add(row.Info.PackageName + "@" + row.Latest);
                        }

                        packages.Apply(add, null);
                    }
                }
            }

            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            if (scan.GoogleMobileAds.Source == InstallSource.None)
            {
                EditorGUILayout.HelpBox("Install Google Mobile Ads first. Every adapter depends on it.", MessageType.Warning);
            }

            foreach (AdapterRow row in scan.Adapters)
            {
                if (search.Length > 0 && row.Info.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                DrawRow(row, true);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                "Installing an adapter only adds its code. You also need to add the network to a mediation group " +
                "in the AdMob web console and follow the network's own setup steps (open Docs).",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawRow(AdapterRow row, bool isAdapter)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool hasDetails = row.Native.Count > 0;
                    string key = row.Info.DisplayName;
                    if (hasDetails)
                    {
                        bool open = expanded.Contains(key);
                        bool now = EditorGUILayout.Foldout(open, row.Info.DisplayName, true);
                        if (now != open)
                        {
                            if (now) { expanded.Add(key); } else { expanded.Remove(key); }
                        }
                    }
                    else
                    {
                        GUILayout.Label(row.Info.DisplayName, nameStyle, GUILayout.MinWidth(150));
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label(new GUIContent(InstalledLabel(row), SourceTooltip(row.Source)), GUILayout.Width(130));
                    GUILayout.Label(new GUIContent(LatestLabel(row), "Newest version on the registry"), GUILayout.Width(100));
                    DrawRowButtons(row, isAdapter);
                    if (GUILayout.Button(new GUIContent("Docs", row.Info.DocsUrl), GUILayout.Width(48)))
                    {
                        Application.OpenURL(row.Info.DocsUrl);
                    }
                }

                if (row.Duplicate)
                {
                    EditorGUILayout.HelpBox(
                        "Also found as a .unitypackage in " + row.LegacyFolder + ". Remove that copy to avoid duplicate classes.",
                        MessageType.Error);
                }

                if (row.Native.Count > 0 && expanded.Contains(row.Info.DisplayName))
                {
                    DrawNativeDependencies(row);
                }
            }
        }

        private void DrawRowButtons(AdapterRow row, bool isAdapter)
        {
            using (new EditorGUI.DisabledScope(packages.Busy))
            {
                switch (row.Source)
                {
                    case InstallSource.None:
                        using (new EditorGUI.DisabledScope(!row.HasPackage || (isAdapter && scan.GoogleMobileAds.Source == InstallSource.None)))
                        {
                            if (GUILayout.Button("Install", GUILayout.Width(150)))
                            {
                                packages.Apply(new[] { row.Info.PackageName }, null);
                            }
                        }

                        break;

                    case InstallSource.PackageManager:
                        using (new EditorGUI.DisabledScope(!row.UpdateAvailable))
                        {
                            if (GUILayout.Button(row.UpdateAvailable ? "Update" : "Up to date", GUILayout.Width(80)))
                            {
                                packages.Apply(new[] { row.Info.PackageName + "@" + row.Latest }, null);
                            }
                        }

                        if (row.Duplicate)
                        {
                            if (GUILayout.Button(new GUIContent("Fix", "Move the .unitypackage copy to the trash"), GUILayout.Width(66)))
                            {
                                TrashLegacyFolder(row);
                            }
                        }
                        else if (GUILayout.Button("Remove", GUILayout.Width(66)))
                        {
                            RemovePackage(row);
                        }

                        break;

                    case InstallSource.UnityPackage:
                        using (new EditorGUI.DisabledScope(!row.HasPackage || string.IsNullOrEmpty(row.LegacyFolder)))
                        {
                            if (GUILayout.Button(new GUIContent("Migrate", "Replace the .unitypackage copy with the OpenUPM package"), GUILayout.Width(80)))
                            {
                                MigrateToPackage(row);
                            }
                        }

                        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(row.LegacyFolder)))
                        {
                            if (GUILayout.Button("Remove", GUILayout.Width(66)))
                            {
                                TrashLegacyFolder(row);
                            }
                        }

                        break;
                }
            }
        }

        private static void DrawNativeDependencies(AdapterRow row)
        {
            EditorGUI.indentLevel++;
            foreach (NativeDependency dependency in row.Native)
            {
                string platform = dependency.Platform == AdPlatform.IOS ? "iOS" : "Android";
                string version = string.IsNullOrEmpty(dependency.Version) ? "(any)" : dependency.Version;
                EditorGUILayout.LabelField(platform + "  " + dependency.Name, version, EditorStyles.miniLabel);
            }

            EditorGUI.indentLevel--;
        }

        private static string InstalledLabel(AdapterRow row)
        {
            switch (row.Source)
            {
                case InstallSource.PackageManager:
                    return "Installed " + row.Version;
                case InstallSource.UnityPackage:
                    return "Installed (.unitypackage)";
                default:
                    return "Not installed";
            }
        }

        private static string LatestLabel(AdapterRow row)
        {
            if (!row.HasPackage)
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(row.Latest) ? "Latest ?" : "Latest " + row.Latest;
        }

        private static string SourceTooltip(InstallSource source)
        {
            switch (source)
            {
                case InstallSource.PackageManager: return "Installed through the Unity Package Manager";
                case InstallSource.UnityPackage: return "Imported from a .unitypackage into the Assets folder";
                default: return string.Empty;
            }
        }

        private void RemovePackage(AdapterRow row)
        {
            if (EditorUtility.DisplayDialog("Remove " + row.Info.DisplayName + "?",
                    "This removes " + row.Info.PackageName + " from Packages/manifest.json.", "Remove", "Cancel"))
            {
                packages.Apply(null, new[] { row.Info.PackageName });
            }
        }

        private void TrashLegacyFolder(AdapterRow row)
        {
            if (!EditorUtility.DisplayDialog("Remove the .unitypackage copy of " + row.Info.DisplayName + "?",
                    "This moves " + row.LegacyFolder + " to the system trash. You can restore it from there.",
                    "Move to trash", "Cancel"))
            {
                return;
            }

            if (!AssetDatabase.MoveAssetToTrash(row.LegacyFolder))
            {
                Debug.LogWarning("[" + ProductName + "] Could not move " + row.LegacyFolder + " to the trash.");
            }

            Rescan();
        }

        private void MigrateToPackage(AdapterRow row)
        {
            if (!EditorUtility.DisplayDialog("Migrate " + row.Info.DisplayName + " to the Package Manager?",
                    "This moves " + row.LegacyFolder + " to the system trash and installs " + row.Info.PackageName +
                    " from OpenUPM instead. The adapter's Unity plugin version may change.",
                    "Migrate", "Cancel"))
            {
                return;
            }

            // Check the registry first so that a cancelled dialog does not leave the project without the adapter.
            if (!packages.EnsureRegistry())
            {
                return;
            }

            if (!AssetDatabase.MoveAssetToTrash(row.LegacyFolder))
            {
                Debug.LogWarning("[" + ProductName + "] Could not move " + row.LegacyFolder + " to the trash; migration stopped.");
                return;
            }

            packages.Apply(new[] { row.Info.PackageName }, null);
        }

        // ------------------------------------------------------------------ App Settings tab

        private void DrawSettingsTab()
        {
            Header("AdMob App IDs");
            if (scan.GoogleMobileAds.Source == InstallSource.None)
            {
                EditorGUILayout.HelpBox("Install Google Mobile Ads (Networks tab) to edit the App IDs.", MessageType.Info);
            }
            else if (settings == null)
            {
                EditorGUILayout.HelpBox("The plugin's settings asset does not exist yet.", MessageType.Info);
                if (GUILayout.Button("Create settings asset", GUILayout.Width(180)))
                {
                    if (!GoogleMobileAdsSettingsBridge.OpenPluginSettings())
                    {
                        Debug.LogWarning("[" + ProductName + "] The menu '" + GoogleMobileAdsSettingsBridge.PluginSettingsMenu + "' was not found.");
                    }

                    LoadSettingsFields();
                }
            }
            else if (!settings.IsSupported)
            {
                EditorGUILayout.HelpBox(
                    "This version of the Google Mobile Ads plugin stores its settings differently. Use the plugin's own settings instead.",
                    MessageType.Warning);
                if (GUILayout.Button("Open Google Mobile Ads settings", GUILayout.Width(220)))
                {
                    GoogleMobileAdsSettingsBridge.OpenPluginSettings();
                }
            }
            else
            {
                DrawAppIdFields();
            }

            EditorGUILayout.Space(12);
            DrawAdUnitIdSet();
        }

        private void DrawAppIdFields()
        {
            EditorGUI.BeginChangeCheck();
            androidAppId = EditorGUILayout.TextField(new GUIContent("Android App ID", "From AdMob: Apps > App settings > App ID"), androidAppId);
            DrawIdHint(AdMobIdValidator.CheckAppId(androidAppId));
            iosAppId = EditorGUILayout.TextField(new GUIContent("iOS App ID", "From AdMob: Apps > App settings > App ID"), iosAppId);
            DrawIdHint(AdMobIdValidator.CheckAppId(iosAppId));
            if (settings.HasTrackingField)
            {
                EditorGUILayout.LabelField(new GUIContent("iOS tracking prompt text", "NSUserTrackingUsageDescription, shown by App Tracking Transparency"));
                trackingDescription = EditorGUILayout.TextArea(trackingDescription, GUILayout.MinHeight(36));
            }

            if (EditorGUI.EndChangeCheck())
            {
                settingsDirty = true;
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Use Google sample App IDs", "Fill in Google's public test App IDs for development"), GUILayout.Width(190)))
                {
                    androidAppId = GoogleTestIds.AndroidAppId;
                    iosAppId = GoogleTestIds.IOSAppId;
                    settingsDirty = true;
                    GUI.FocusControl(null);
                }

                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!settingsDirty))
                {
                    if (GUILayout.Button("Revert", GUILayout.Width(70)))
                    {
                        LoadSettingsFields();
                        GUI.FocusControl(null);
                    }

                    if (GUILayout.Button("Save", GUILayout.Width(70)))
                    {
                        SaveSettings();
                    }
                }
            }
        }

        private void SaveSettings()
        {
            if (settings == null)
            {
                return;
            }

            // ApplyModifiedProperties (inside Save) records the change for Undo.
            settings.Reload();
            settings.AndroidAppId = androidAppId;
            settings.IOSAppId = iosAppId;
            if (settings.HasTrackingField)
            {
                settings.UserTrackingUsageDescription = trackingDescription;
            }

            settings.Save();
            settingsDirty = false;
            GUI.FocusControl(null);
            Rescan();
        }

        private static void DrawIdHint(IdCheck check)
        {
            switch (check)
            {
                case IdCheck.TestId:
                    EditorGUILayout.HelpBox("Google's sample ID: fine for development, replace it before release.", MessageType.Warning);
                    break;
                case IdCheck.WrongKind:
                    EditorGUILayout.HelpBox("This is an ad unit ID ('/'). App IDs contain '~'.", MessageType.Error);
                    break;
                case IdCheck.Malformed:
                    EditorGUILayout.HelpBox("Expected ca-app-pub-XXXXXXXXXXXXXXXX~NNNNNNNNNN.", MessageType.Error);
                    break;
            }
        }

        private void DrawAdUnitIdSet()
        {
            Header("Ad unit IDs (optional)");
            EditorGUILayout.LabelField(
                "An Ad Unit ID Set keeps every ad unit ID in one asset and can swap them all for Google's test ad units with one switch. " +
                "Read it from your ad code with adUnits.Get(AdFormat.Rewarded).",
                EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                adUnitIdSet = (AdUnitIdSet)EditorGUILayout.ObjectField("Asset", adUnitIdSet, typeof(AdUnitIdSet), false);
                if (adUnitIdSet == null && GUILayout.Button("Create...", GUILayout.Width(80)))
                {
                    CreateAdUnitIdSet();
                }
            }

            if (adUnitIdSet == null)
            {
                return;
            }

            UnityEditor.Editor.CreateCachedEditor(adUnitIdSet, null, ref adUnitEditor);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                adUnitEditor.OnInspectorGUI();
            }

            foreach (AdUnitIdSet.Entry entry in adUnitIdSet.Entries)
            {
                if (entry == null)
                {
                    continue;
                }

                CheckEntryId(entry.format + " (Android)", entry.androidId);
                CheckEntryId(entry.format + " (iOS)", entry.iosId);
            }

            if (adUnitIdSet.UseTestAdUnits)
            {
                EditorGUILayout.HelpBox("Test ad units are on. Turn them off for release builds.", MessageType.Info);
            }
        }

        private static void CheckEntryId(string label, string id)
        {
            IdCheck check = AdMobIdValidator.CheckAdUnitId(id);
            if (check == IdCheck.WrongKind)
            {
                EditorGUILayout.HelpBox(label + ": this is an App ID ('~'). Ad unit IDs contain '/'.", MessageType.Error);
            }
            else if (check == IdCheck.Malformed)
            {
                EditorGUILayout.HelpBox(label + ": expected ca-app-pub-XXXXXXXXXXXXXXXX/NNNNNNNNNN.", MessageType.Error);
            }
        }

        private void CreateAdUnitIdSet()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Ad Unit ID Set", "AdUnitIdSet", "asset",
                "Choose where to save the Ad Unit ID Set.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var asset = CreateInstance<AdUnitIdSet>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            adUnitIdSet = asset;
            EditorGUIUtility.PingObject(asset);
        }

        // ------------------------------------------------------------------ Integration Status tab

        private void DrawStatusTab()
        {
            Header("Integration status");
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No problems found.", MessageType.Info);
            }

            foreach (Issue issue in issues)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(issue.Message, ToMessageType(issue.Severity));
                    DrawFixButton(issue);
                }
            }

            EditorGUILayout.Space(8);
            Header("Summary");
            ProjectSnapshot s = scan.Snapshot;
            EditorGUILayout.LabelField("Google Mobile Ads", InstalledLabel(scan.GoogleMobileAds));
            EditorGUILayout.LabelField("External Dependency Manager", InstalledLabel(scan.ExternalDependencyManager));
            EditorGUILayout.LabelField("Mediation adapters", s.Adapters.Count.ToString());
            EditorGUILayout.LabelField("Android App ID", Describe(AdMobIdValidator.CheckAppId(s.AndroidAppId)));
            EditorGUILayout.LabelField("iOS App ID", Describe(AdMobIdValidator.CheckAppId(s.IOSAppId)));

            EditorGUILayout.Space(8);
            if (GUILayout.Button(new GUIContent("Copy report to clipboard",
                    "A plain-text summary of versions and findings, handy for bug reports. IDs are not included."), GUILayout.Width(200)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildReport();
                ShowNotification(new GUIContent("Report copied"));
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                "To confirm that each network actually fills on a device, open the Ad Inspector from your game " +
                "(MobileAds.OpenAdInspector) on a test device registered in AdMob.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawFixButton(Issue issue)
        {
            string label = null;
            Action fix = null;
            switch (issue.Code)
            {
                case IssueCode.GoogleMobileAdsMissing:
                    label = "Install";
                    fix = () => packages.Apply(new[] { AdapterCatalog.GoogleMobileAdsPackage }, null);
                    break;
                case IssueCode.ExternalDependencyManagerMissing:
                    label = "Install";
                    fix = () => packages.Apply(new[] { AdapterCatalog.ExternalDependencyManagerPackage }, null);
                    break;
                case IssueCode.SettingsAssetMissing:
                    label = "Create";
                    fix = () =>
                    {
                        GoogleMobileAdsSettingsBridge.OpenPluginSettings();
                        Rescan();
                    };
                    break;
                case IssueCode.AndroidAppIdMissing:
                case IssueCode.AndroidAppIdInvalid:
                case IssueCode.AndroidAppIdIsTest:
                case IssueCode.IOSAppIdMissing:
                case IssueCode.IOSAppIdInvalid:
                case IssueCode.IOSAppIdIsTest:
                case IssueCode.TrackingDescriptionMissing:
                    label = "Edit";
                    fix = () => tab = 1;
                    break;
                case IssueCode.AdapterInstalledTwice:
                    label = "Show";
                    fix = () =>
                    {
                        tab = 0;
                        search = issue.Adapter != null ? issue.Adapter.DisplayName : string.Empty;
                    };
                    break;
                case IssueCode.NoAdapters:
                    label = "Networks";
                    fix = () => tab = 0;
                    break;
            }

            if (label == null)
            {
                return;
            }

            using (new EditorGUI.DisabledScope(packages.Busy))
            {
                if (GUILayout.Button(label, GUILayout.Width(80), GUILayout.Height(38)))
                {
                    fix();
                }
            }
        }

        /// <summary>
        /// Builds a privacy-safe report: versions and findings only. App IDs and ad unit IDs are
        /// deliberately left out so the report can be pasted into public issue trackers.
        /// </summary>
        private string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine(ProductName + " report");
            sb.AppendLine("Unity " + Application.unityVersion + ", build target " + EditorUserBuildSettings.activeBuildTarget);
            sb.AppendLine("Google Mobile Ads: " + InstalledLabel(scan.GoogleMobileAds));
            sb.AppendLine("External Dependency Manager: " + InstalledLabel(scan.ExternalDependencyManager));
            sb.AppendLine("Android App ID: " + Describe(AdMobIdValidator.CheckAppId(scan.Snapshot.AndroidAppId)));
            sb.AppendLine("iOS App ID: " + Describe(AdMobIdValidator.CheckAppId(scan.Snapshot.IOSAppId)));
            sb.AppendLine();
            sb.AppendLine("Adapters:");
            foreach (AdapterRow row in scan.Adapters)
            {
                if (!row.Installed)
                {
                    continue;
                }

                sb.Append("- ").Append(row.Info.DisplayName).Append(": ").Append(InstalledLabel(row));
                if (row.UpdateAvailable)
                {
                    sb.Append(" (latest ").Append(row.Latest).Append(')');
                }

                sb.AppendLine();
                foreach (NativeDependency dependency in row.Native)
                {
                    sb.Append("    ").Append(dependency.Platform == AdPlatform.IOS ? "iOS " : "Android ")
                        .Append(dependency.Name).Append(' ').AppendLine(dependency.Version);
                }
            }

            sb.AppendLine();
            sb.AppendLine("Findings:");
            if (issues.Count == 0)
            {
                sb.AppendLine("- none");
            }

            foreach (Issue issue in issues)
            {
                sb.Append("- [").Append(issue.Severity).Append("] ").AppendLine(issue.Message);
            }

            return sb.ToString();
        }

        private static string Describe(IdCheck check)
        {
            switch (check)
            {
                case IdCheck.Empty: return "not set";
                case IdCheck.Valid: return "set";
                case IdCheck.TestId: return "Google sample ID";
                case IdCheck.WrongKind: return "wrong kind of ID";
                default: return "invalid format";
            }
        }

        private static MessageType ToMessageType(Severity severity)
        {
            switch (severity)
            {
                case Severity.Error: return MessageType.Error;
                case Severity.Warning: return MessageType.Warning;
                default: return MessageType.Info;
            }
        }

        private static void Header(string text)
        {
            GUILayout.Label(text, EditorStyles.boldLabel);
        }
    }
}

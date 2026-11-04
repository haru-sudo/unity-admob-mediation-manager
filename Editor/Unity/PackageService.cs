using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace AdMobMediationManager.Editor
{
    /// <summary>
    /// Talks to the Unity Package Manager: lists what is installed, asks the registry for the
    /// latest versions, and adds or removes packages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Package Manager requests are asynchronous. This class polls them from
    /// <see cref="EditorApplication.update"/> and raises <see cref="Changed"/> whenever its state
    /// changes, so the window only has to repaint.
    /// </para>
    /// <para>
    /// Adding or removing packages recompiles scripts, which reloads the Editor's script domain and
    /// throws away every object in memory, including this one. Changes are therefore sent as a
    /// single <see cref="Client.AddAndRemove"/> request, and the window simply refreshes after the
    /// reload instead of trying to continue a queue.
    /// </para>
    /// </remarks>
    internal sealed class PackageService
    {
        private readonly Dictionary<string, PackageInfo> installed = new Dictionary<string, PackageInfo>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> latest = new Dictionary<string, string>(StringComparer.Ordinal);

        private ListRequest listRequest;
        private SearchRequest searchRequest;
        private AddAndRemoveRequest changeRequest;

        /// <summary>Raised on the main thread whenever installed packages, versions or <see cref="Status"/> change.</summary>
        public event Action Changed;

        /// <summary>True while a Package Manager request is running.</summary>
        public bool Busy
        {
            get { return listRequest != null || searchRequest != null || changeRequest != null; }
        }

        /// <summary>A short line describing what is happening, or the last error. Empty when idle.</summary>
        public string Status { get; private set; } = string.Empty;

        /// <summary>True when the last registry search failed (often: no network, or the registry is not configured).</summary>
        public bool SearchFailed { get; private set; }

        /// <summary>Returns the installed package called <paramref name="name"/>, or null.</summary>
        public PackageInfo GetInstalled(string name)
        {
            PackageInfo info;
            return installed.TryGetValue(name, out info) ? info : null;
        }

        /// <summary>Every installed package whose name starts with <paramref name="prefix"/>.</summary>
        public IEnumerable<PackageInfo> InstalledWithPrefix(string prefix)
        {
            foreach (KeyValuePair<string, PackageInfo> pair in installed)
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    yield return pair.Value;
                }
            }
        }

        /// <summary>
        /// Returns the newest version the registry offers for <paramref name="name"/> that is compatible
        /// with this Unity version, or empty when it is not known (not searched yet, or offline).
        /// </summary>
        public string GetLatest(string name)
        {
            string version;
            return latest.TryGetValue(name, out version) ? version : string.Empty;
        }

        /// <summary>
        /// Reloads the list of installed packages, then asks the registries for the latest versions.
        /// Does nothing when a request is already running.
        /// </summary>
        public void Refresh()
        {
            if (Busy)
            {
                return;
            }

            SetStatus("Reading installed packages...");
            listRequest = Client.List(false, false);
            Watch();
        }

        /// <summary>
        /// Installs, updates or removes packages in one request.
        /// </summary>
        /// <param name="toAdd">Package identifiers to add, either <c>name</c> (latest) or <c>name@version</c>.</param>
        /// <param name="toRemove">Package names to remove.</param>
        public void Apply(IList<string> toAdd, IList<string> toRemove)
        {
            if (Busy)
            {
                Debug.LogWarning("[AdMob Mediation Manager] Wait for the current Package Manager operation to finish.");
                return;
            }

            string[] add = toAdd == null ? new string[0] : new List<string>(toAdd).ToArray();
            string[] remove = toRemove == null ? new string[0] : new List<string>(toRemove).ToArray();
            if (add.Length == 0 && remove.Length == 0)
            {
                return;
            }

            if (add.Length > 0 && !EnsureRegistry())
            {
                return;
            }

            SetStatus("Updating packages... Unity will recompile when it is done.");
            changeRequest = Client.AddAndRemove(add, remove);
            Watch();
        }

        /// <summary>
        /// Makes sure <c>Packages/manifest.json</c> contains the OpenUPM scoped registry with the
        /// scopes the Google packages need. Asks before changing the file.
        /// </summary>
        /// <returns>False when the user declined or the manifest could not be updated.</returns>
        public bool EnsureRegistry()
        {
            string path = ManifestPath;
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException e)
            {
                ReportError("Could not read " + path + ": " + e.Message);
                return false;
            }

            ManifestEditor.Result result;
            try
            {
                result = ManifestEditor.EnsureScopedRegistry(
                    text, AdapterCatalog.RegistryName, AdapterCatalog.RegistryUrl, AdapterCatalog.RegistryScopes);
            }
            catch (FormatException e)
            {
                ReportError("Packages/manifest.json could not be parsed: " + e.Message);
                return false;
            }

            if (!result.Changed)
            {
                return true;
            }

            bool ok = EditorUtility.DisplayDialog(
                "Add the OpenUPM registry?",
                "Google publishes the Google Mobile Ads plugin and its mediation adapters on OpenUPM.\n\n" +
                "This adds the registry " + AdapterCatalog.RegistryUrl + " with the scopes\n  " +
                string.Join("\n  ", AdapterCatalog.RegistryScopes) + "\nto Packages/manifest.json.",
                "Add registry",
                "Cancel");
            if (!ok)
            {
                return false;
            }

            try
            {
                File.WriteAllText(path, result.Text);
            }
            catch (IOException e)
            {
                ReportError("Could not write " + path + ": " + e.Message);
                return false;
            }

            Client.Resolve();
            return true;
        }

        /// <summary>True when the manifest already lists the OpenUPM registry for the Google packages.</summary>
        public static bool RegistryConfigured()
        {
            try
            {
                return ManifestEditor.RegistryCovers(
                    File.ReadAllText(ManifestPath), AdapterCatalog.RegistryUrl, AdapterCatalog.GoogleMobileAdsPackage);
            }
            catch (Exception e) when (e is IOException || e is FormatException)
            {
                return false;
            }
        }

        private static string ManifestPath
        {
            get { return Path.Combine(Directory.GetCurrentDirectory(), "Packages", "manifest.json"); }
        }

        private void Watch()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        // Called every Editor frame while a request is running.
        private void Poll()
        {
            if (listRequest != null && listRequest.IsCompleted)
            {
                OnListCompleted();
            }

            if (searchRequest != null && searchRequest.IsCompleted)
            {
                OnSearchCompleted();
            }

            if (changeRequest != null && changeRequest.IsCompleted)
            {
                OnChangeCompleted();
            }

            if (!Busy)
            {
                EditorApplication.update -= Poll;
            }
        }

        private void OnListCompleted()
        {
            ListRequest request = listRequest;
            listRequest = null;
            if (request.Status != StatusCode.Success)
            {
                ReportError("Could not list packages: " + ErrorText(request.Error));
                return;
            }

            installed.Clear();
            foreach (PackageInfo info in request.Result)
            {
                installed[info.name] = info;

                // Installed packages already carry the newest compatible version the registry knows about.
                string newest = info.versions != null ? info.versions.latestCompatible : null;
                if (!string.IsNullOrEmpty(newest))
                {
                    latest[info.name] = newest;
                }
            }

            if (RegistryConfigured())
            {
                // SearchAll returns every package the configured registries serve. With OpenUPM scoped
                // to the Google packages, that is exactly the plugin, the adapters and EDM4U.
                SetStatus("Checking for the latest versions...");
                searchRequest = Client.SearchAll(false);
            }
            else
            {
                SetStatus(string.Empty);
            }
        }

        private void OnSearchCompleted()
        {
            SearchRequest request = searchRequest;
            searchRequest = null;
            SearchFailed = request.Status != StatusCode.Success;
            if (SearchFailed)
            {
                ReportError("Could not reach the package registry: " + ErrorText(request.Error));
                return;
            }

            foreach (PackageInfo info in request.Result)
            {
                string newest = info.versions != null ? info.versions.latestCompatible : null;
                if (string.IsNullOrEmpty(newest) && info.versions != null)
                {
                    newest = info.versions.latest;
                }

                if (!string.IsNullOrEmpty(newest) && IsGooglePackage(info.name))
                {
                    latest[info.name] = newest;
                }
            }

            SetStatus(string.Empty);
        }

        private void OnChangeCompleted()
        {
            AddAndRemoveRequest request = changeRequest;
            changeRequest = null;
            if (request.Status != StatusCode.Success)
            {
                ReportError("Package Manager could not apply the change: " + ErrorText(request.Error));
                return;
            }

            // When scripts recompile the domain reloads and the window refreshes itself on enable.
            // When nothing recompiles (for example only a version number changed), refresh here.
            SetStatus(string.Empty);
            Refresh();
        }

        private static bool IsGooglePackage(string name)
        {
            return ManifestEditor.ScopeMatches(AdapterCatalog.GoogleMobileAdsPackage, name)
                || name == AdapterCatalog.ExternalDependencyManagerPackage;
        }

        private static string ErrorText(Error error)
        {
            return error == null ? "unknown error" : error.message;
        }

        private void ReportError(string message)
        {
            Debug.LogWarning("[AdMob Mediation Manager] " + message);
            SetStatus(message);
        }

        private void SetStatus(string status)
        {
            Status = status;
            Changed?.Invoke();
        }
    }
}

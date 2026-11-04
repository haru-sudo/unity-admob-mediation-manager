using System;
using System.Collections.Generic;

namespace AdMobMediationManager.Editor
{
    /// <summary>
    /// Describes one Google Mobile Ads mediation adapter plugin for Unity.
    /// </summary>
    internal sealed class AdapterInfo
    {
        public AdapterInfo(string displayName, string packageName, string docsUrl, params string[] legacyFolderNames)
        {
            DisplayName = displayName;
            PackageName = packageName;
            DocsUrl = docsUrl;
            LegacyFolderNames = legacyFolderNames;
        }

        /// <summary>Name shown in the window, e.g. "AppLovin".</summary>
        public string DisplayName { get; }

        /// <summary>UPM package name on OpenUPM, e.g. "com.google.ads.mobile.mediation.applovin".</summary>
        public string PackageName { get; }

        /// <summary>Google's integration guide for this network.</summary>
        public string DocsUrl { get; }

        /// <summary>
        /// Folder names under <c>Assets/GoogleMobileAds/Mediation/</c> that a <c>.unitypackage</c>
        /// install of this adapter uses. Matching ignores case.
        /// </summary>
        public IReadOnlyList<string> LegacyFolderNames { get; }

        /// <summary>Returns true when <paramref name="folderName"/> is one of <see cref="LegacyFolderNames"/>.</summary>
        public bool MatchesLegacyFolder(string folderName)
        {
            foreach (string name in LegacyFolderNames)
            {
                if (string.Equals(name, folderName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// The list of mediation adapters the window knows about, and the packages they rely on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Google publishes the Google Mobile Ads Unity plugin and every adapter plugin on the OpenUPM
    /// registry. Package names were checked against OpenUPM's package list (2026-11).
    /// Versions are not hard-coded: the window asks the registry for the latest version.
    /// </para>
    /// <para>
    /// To add a network Google adds later, append an entry to <see cref="Adapters"/>; nothing else
    /// needs to change.
    /// </para>
    /// </remarks>
    internal static class AdapterCatalog
    {
        /// <summary>The OpenUPM registry URL.</summary>
        public const string RegistryUrl = "https://package.openupm.com";

        /// <summary>The name used when the registry is added to manifest.json.</summary>
        public const string RegistryName = "package.openupm.com";

        /// <summary>The Google Mobile Ads Unity plugin (the SDK every adapter depends on).</summary>
        public const string GoogleMobileAdsPackage = "com.google.ads.mobile";

        /// <summary>External Dependency Manager for Unity, which resolves the native Android/iOS libraries.</summary>
        public const string ExternalDependencyManagerPackage = "com.google.external-dependency-manager";

        /// <summary>Prefix shared by every adapter package name.</summary>
        public const string AdapterPackagePrefix = "com.google.ads.mobile.mediation.";

        /// <summary>Where a <c>.unitypackage</c> install of the plugin puts its adapters.</summary>
        public const string LegacyMediationFolder = "Assets/GoogleMobileAds/Mediation";

        /// <summary>Google's mediation overview, used when a network has no page of its own.</summary>
        public const string MediationDocsUrl = "https://developers.google.com/admob/unity/mediation";

        /// <summary>The registry scopes that cover the plugin, the adapters and EDM4U.</summary>
        public static readonly string[] RegistryScopes =
        {
            GoogleMobileAdsPackage,
            ExternalDependencyManagerPackage,
        };

        private const string Docs = MediationDocsUrl + "/";

        /// <summary>Every known adapter, sorted by display name.</summary>
        public static readonly IReadOnlyList<AdapterInfo> Adapters = new[]
        {
            new AdapterInfo("AppLovin", AdapterPackagePrefix + "applovin", Docs + "applovin", "AppLovin"),
            new AdapterInfo("BidMachine", AdapterPackagePrefix + "bidmachine", MediationDocsUrl, "BidMachine"),
            new AdapterInfo("Chartboost", AdapterPackagePrefix + "chartboost", Docs + "chartboost", "Chartboost"),
            new AdapterInfo("DT Exchange", AdapterPackagePrefix + "dtexchange", Docs + "dt-exchange", "DTExchange", "Fyber"),
            new AdapterInfo("i-mobile", AdapterPackagePrefix + "imobile", Docs + "i-mobile", "IMobile"),
            new AdapterInfo("InMobi", AdapterPackagePrefix + "inmobi", Docs + "inmobi", "InMobi"),
            new AdapterInfo("ironSource", AdapterPackagePrefix + "ironsource", Docs + "ironsource", "IronSource"),
            new AdapterInfo("Liftoff Monetize", AdapterPackagePrefix + "liftoffmonetize", Docs + "liftoff-monetize", "LiftoffMonetize", "Vungle"),
            new AdapterInfo("LINE Ads Network", AdapterPackagePrefix + "line", Docs + "line", "Line"),
            new AdapterInfo("maio", AdapterPackagePrefix + "maio", Docs + "maio", "Maio"),
            new AdapterInfo("Meta Audience Network", AdapterPackagePrefix + "metaaudiencenetwork", Docs + "meta", "MetaAudienceNetwork", "Meta"),
            new AdapterInfo("Mintegral", AdapterPackagePrefix + "mintegral", Docs + "mintegral", "Mintegral"),
            new AdapterInfo("Moloco", AdapterPackagePrefix + "moloco", Docs + "moloco", "Moloco"),
            new AdapterInfo("myTarget", AdapterPackagePrefix + "mytarget", Docs + "mytarget", "MyTarget"),
            new AdapterInfo("Pangle", AdapterPackagePrefix + "pangle", Docs + "pangle", "Pangle"),
            new AdapterInfo("PubMatic", AdapterPackagePrefix + "pubmatic", MediationDocsUrl, "PubMatic"),
            new AdapterInfo("Unity Ads", AdapterPackagePrefix + "unity", Docs + "unity", "UnityAds", "Unity"),
            new AdapterInfo("Verve", AdapterPackagePrefix + "verve", MediationDocsUrl, "Verve"),
        };

        /// <summary>Returns the adapter whose package is <paramref name="packageName"/>, or null.</summary>
        public static AdapterInfo FindByPackage(string packageName)
        {
            foreach (AdapterInfo adapter in Adapters)
            {
                if (adapter.PackageName == packageName)
                {
                    return adapter;
                }
            }

            return null;
        }

        /// <summary>Returns the adapter installed into the legacy folder <paramref name="folderName"/>, or null.</summary>
        public static AdapterInfo FindByLegacyFolder(string folderName)
        {
            foreach (AdapterInfo adapter in Adapters)
            {
                if (adapter.MatchesLegacyFolder(folderName))
                {
                    return adapter;
                }
            }

            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdMobMediationManager
{
    /// <summary>
    /// An asset that keeps the ad unit IDs of a game in one place, per format and per platform,
    /// with a single switch that replaces all of them with Google's test ad units.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Create one from <c>Assets &gt; Create &gt; AdMob Mediation Manager &gt; Ad Unit ID Set</c>
    /// (or from the App Settings tab of the manager window) and reference it from your ad code:
    /// </para>
    /// <code>
    /// [SerializeField] AdUnitIdSet adUnits;
    ///
    /// void LoadInterstitial()
    /// {
    ///     InterstitialAd.Load(adUnits.Get(AdFormat.Interstitial), new AdRequest(), OnLoaded);
    /// }
    /// </code>
    /// <para>
    /// Ad unit IDs are not secrets (they ship inside every build), but many teams still prefer not to
    /// publish them. If your repository is public, consider keeping this asset out of version control.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "AdUnitIdSet", menuName = "AdMob Mediation Manager/Ad Unit ID Set", order = 1000)]
    public sealed class AdUnitIdSet : ScriptableObject
    {
        /// <summary>The ad unit IDs of one format on both platforms.</summary>
        [Serializable]
        public sealed class Entry
        {
            /// <summary>The ad format these IDs belong to.</summary>
            [Tooltip("The ad format these IDs belong to.")]
            public AdFormat format;

            /// <summary>Ad unit ID for Android.</summary>
            [Tooltip("Ad unit ID for Android, in the form ca-app-pub-XXXXXXXXXXXXXXXX/NNNNNNNNNN.")]
            public string androidId = string.Empty;

            /// <summary>Ad unit ID for iOS.</summary>
            [Tooltip("Ad unit ID for iOS, in the form ca-app-pub-XXXXXXXXXXXXXXXX/NNNNNNNNNN.")]
            public string iosId = string.Empty;
        }

        [SerializeField]
        [Tooltip("When on, Get() returns Google's test ad units instead of the IDs below. " +
                 "Turn it on while developing and off for release builds.")]
        private bool useTestAdUnits = true;

        [SerializeField]
        [Tooltip("One entry per ad format the game uses.")]
        private List<Entry> entries = new List<Entry>();

        /// <summary>
        /// When true, <see cref="Get"/> returns Google's test ad units for every format.
        /// </summary>
        public bool UseTestAdUnits
        {
            get { return useTestAdUnits; }
            set { useTestAdUnits = value; }
        }

        /// <summary>The configured entries. Edit them in the Inspector.</summary>
        public IReadOnlyList<Entry> Entries
        {
            get { return entries; }
        }

        /// <summary>
        /// Returns the ad unit ID to load for <paramref name="format"/> on the current platform.
        /// </summary>
        /// <returns>
        /// The configured ID, Google's test ID when <see cref="UseTestAdUnits"/> is on, or
        /// <see cref="AdUnitIdResolver.Unused"/> when the platform is not Android/iOS or no ID is set.
        /// </returns>
        public string Get(AdFormat format)
        {
            Entry entry = Find(format);
            return AdUnitIdResolver.Resolve(
                format,
                CurrentPlatform(),
                entry != null ? entry.androidId : null,
                entry != null ? entry.iosId : null,
                useTestAdUnits);
        }

        /// <summary>Returns the entry for <paramref name="format"/>, or null when none is configured.</summary>
        public Entry Find(AdFormat format)
        {
            foreach (Entry entry in entries)
            {
                if (entry != null && entry.format == format)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>
        /// The platform the code was compiled for. In the Editor this follows the active build target,
        /// so switching the build target to Android or iOS lets you preview the IDs in Play Mode.
        /// </summary>
        private static AdPlatform? CurrentPlatform()
        {
#if UNITY_ANDROID
            return AdPlatform.Android;
#elif UNITY_IOS
            return AdPlatform.IOS;
#else
            return null;
#endif
        }
    }
}

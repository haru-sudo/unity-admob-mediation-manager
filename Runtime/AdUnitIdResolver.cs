namespace AdMobMediationManager
{
    /// <summary>
    /// Decides which ad unit ID to hand to the Google Mobile Ads SDK.
    /// Kept free of UnityEngine so that it can be unit tested outside the Editor.
    /// </summary>
    public static class AdUnitIdResolver
    {
        /// <summary>
        /// The string the Google Mobile Ads SDK treats as "no ad unit" on platforms it does not support
        /// (for example the Editor or a desktop build).
        /// </summary>
        public const string Unused = "unused";

        /// <summary>
        /// Picks the ad unit ID for one format.
        /// </summary>
        /// <param name="format">The ad format being requested.</param>
        /// <param name="platform">The platform the build targets, or null on an unsupported platform.</param>
        /// <param name="androidId">The production ad unit ID configured for Android.</param>
        /// <param name="iosId">The production ad unit ID configured for iOS.</param>
        /// <param name="useTestAdUnits">When true, Google's sample ad unit is returned instead.</param>
        /// <returns>
        /// Google's sample ID when <paramref name="useTestAdUnits"/> is true, the configured ID otherwise.
        /// <see cref="Unused"/> on unsupported platforms or when nothing is configured.
        /// </returns>
        public static string Resolve(AdFormat format, AdPlatform? platform, string androidId, string iosId, bool useTestAdUnits)
        {
            if (platform == null)
            {
                return Unused;
            }

            if (useTestAdUnits)
            {
                return GoogleTestIds.AdUnitId(format, platform.Value);
            }

            string id = platform.Value == AdPlatform.IOS ? iosId : androidId;
            return string.IsNullOrWhiteSpace(id) ? Unused : id.Trim();
        }
    }
}

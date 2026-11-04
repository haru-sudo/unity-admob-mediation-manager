namespace AdMobMediationManager
{
    /// <summary>
    /// Google's official, public sample IDs for testing AdMob integrations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These IDs always serve test ads and are safe to use during development: clicking a test ad
    /// never counts as invalid traffic on your account. They are published in the Google Mobile Ads
    /// documentation ("Enable test ads"), and they are the only ad IDs that appear in this package.
    /// </para>
    /// <para>
    /// Never ship a release build with these IDs: test ads earn no revenue. The Integration Status
    /// tab of the AdMob Mediation Manager window warns when a test App ID is configured.
    /// </para>
    /// <para>
    /// Note that mediation partners do not serve ads through these IDs. To check that a mediated
    /// network is filling, use the Ad Inspector with your own ad unit on a registered test device.
    /// </para>
    /// </remarks>
    public static class GoogleTestIds
    {
        /// <summary>The prefix that every Google sample ID shares (the public sample publisher).</summary>
        public const string SamplePublisherPrefix = "ca-app-pub-3940256099942544";

        /// <summary>Sample App ID for Android.</summary>
        public const string AndroidAppId = "ca-app-pub-3940256099942544~3347511713";

        /// <summary>Sample App ID for iOS.</summary>
        public const string IOSAppId = "ca-app-pub-3940256099942544~1458002511";

        /// <summary>
        /// Returns Google's sample App ID for <paramref name="platform"/>.
        /// </summary>
        public static string AppId(AdPlatform platform)
        {
            return platform == AdPlatform.IOS ? IOSAppId : AndroidAppId;
        }

        /// <summary>
        /// Returns Google's sample ad unit ID for the given format and platform.
        /// </summary>
        /// <param name="format">The ad format.</param>
        /// <param name="platform">The target platform.</param>
        /// <returns>A sample ad unit ID that always serves test ads.</returns>
        public static string AdUnitId(AdFormat format, AdPlatform platform)
        {
            bool ios = platform == AdPlatform.IOS;
            switch (format)
            {
                case AdFormat.Banner:
                    return ios ? "ca-app-pub-3940256099942544/2934735716" : "ca-app-pub-3940256099942544/6300978111";
                case AdFormat.Interstitial:
                    return ios ? "ca-app-pub-3940256099942544/4411468910" : "ca-app-pub-3940256099942544/1033173712";
                case AdFormat.Rewarded:
                    return ios ? "ca-app-pub-3940256099942544/1712485313" : "ca-app-pub-3940256099942544/5224354917";
                case AdFormat.RewardedInterstitial:
                    return ios ? "ca-app-pub-3940256099942544/6978759866" : "ca-app-pub-3940256099942544/5354046379";
                case AdFormat.AppOpen:
                    return ios ? "ca-app-pub-3940256099942544/5575463023" : "ca-app-pub-3940256099942544/9257395921";
                case AdFormat.Native:
                    return ios ? "ca-app-pub-3940256099942544/3986624511" : "ca-app-pub-3940256099942544/2247696110";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Returns true when <paramref name="id"/> is one of Google's sample IDs
        /// (an App ID or an ad unit ID that belongs to the public sample publisher).
        /// </summary>
        public static bool IsTestId(string id)
        {
            return !string.IsNullOrEmpty(id)
                && id.Trim().StartsWith(SamplePublisherPrefix, System.StringComparison.Ordinal);
        }
    }
}

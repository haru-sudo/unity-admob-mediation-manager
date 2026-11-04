namespace AdMobMediationManager
{
    /// <summary>
    /// The ad formats offered by the Google Mobile Ads SDK.
    /// The numeric values are stored in serialized assets, so never reorder or renumber them.
    /// </summary>
    public enum AdFormat
    {
        /// <summary>A rectangular ad that stays on screen (fixed size or adaptive).</summary>
        Banner = 0,

        /// <summary>A full-screen ad shown at natural breaks, such as between levels.</summary>
        Interstitial = 1,

        /// <summary>A full-screen ad that grants the player a reward after it is watched.</summary>
        Rewarded = 2,

        /// <summary>A rewarded ad that can be shown at natural breaks without an opt-in prompt.</summary>
        RewardedInterstitial = 3,

        /// <summary>A full-screen ad shown while the app is launched or brought back to the foreground.</summary>
        AppOpen = 4,

        /// <summary>An ad whose assets are laid out by the app to match its own look.</summary>
        Native = 5,
    }

    /// <summary>The two mobile platforms that AdMob supports.</summary>
    public enum AdPlatform
    {
        /// <summary>Google Play / Android.</summary>
        Android = 0,

        /// <summary>App Store / iOS (including iPadOS).</summary>
        IOS = 1,
    }
}

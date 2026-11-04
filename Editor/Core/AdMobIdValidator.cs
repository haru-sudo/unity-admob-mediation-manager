using System.Text.RegularExpressions;

namespace AdMobMediationManager.Editor
{
    /// <summary>What <see cref="AdMobIdValidator"/> found out about an ID.</summary>
    internal enum IdCheck
    {
        /// <summary>Nothing has been entered.</summary>
        Empty,

        /// <summary>The ID is well formed and belongs to a real publisher.</summary>
        Valid,

        /// <summary>The ID is one of Google's public sample IDs (see <see cref="GoogleTestIds"/>).</summary>
        TestId,

        /// <summary>An ad unit ID (with '/') was entered where an App ID (with '~') is expected, or the reverse.</summary>
        WrongKind,

        /// <summary>The text does not look like an AdMob ID at all.</summary>
        Malformed,
    }

    /// <summary>
    /// Checks the shape of AdMob IDs.
    /// App IDs look like <c>ca-app-pub-XXXXXXXXXXXXXXXX~NNNNNNNNNN</c>,
    /// ad unit IDs like <c>ca-app-pub-XXXXXXXXXXXXXXXX/NNNNNNNNNN</c>.
    /// </summary>
    /// <remarks>
    /// Only the format is checked; whether the ID exists in an AdMob account can only be known
    /// by the SDK at run time.
    /// </remarks>
    internal static class AdMobIdValidator
    {
        private static readonly Regex AppIdPattern = new Regex(@"^ca-app-pub-\d{16}~\d{10}$", RegexOptions.CultureInvariant);
        private static readonly Regex AdUnitIdPattern = new Regex(@"^ca-app-pub-\d{16}/\d{10}$", RegexOptions.CultureInvariant);

        /// <summary>Checks a value entered as an App ID.</summary>
        public static IdCheck CheckAppId(string id)
        {
            return Check(id, AppIdPattern, AdUnitIdPattern);
        }

        /// <summary>Checks a value entered as an ad unit ID.</summary>
        public static IdCheck CheckAdUnitId(string id)
        {
            return Check(id, AdUnitIdPattern, AppIdPattern);
        }

        private static IdCheck Check(string id, Regex expected, Regex otherKind)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return IdCheck.Empty;
            }

            string trimmed = id.Trim();
            if (expected.IsMatch(trimmed))
            {
                return GoogleTestIds.IsTestId(trimmed) ? IdCheck.TestId : IdCheck.Valid;
            }

            return otherKind.IsMatch(trimmed) ? IdCheck.WrongKind : IdCheck.Malformed;
        }
    }
}

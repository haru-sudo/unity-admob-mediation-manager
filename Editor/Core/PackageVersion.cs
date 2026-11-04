using System;
using System.Globalization;

namespace AdMobMediationManager.Editor
{
    /// <summary>
    /// Compares package versions such as <c>9.2.0</c>, <c>3.12.1-preview.2</c> or the four-part
    /// versions some adapters use (<c>8.0.0.1</c>).
    /// </summary>
    /// <remarks>
    /// Follows Semantic Versioning ordering: numeric parts are compared as numbers (missing parts
    /// count as 0), and a version with a pre-release suffix is older than the same version without one.
    /// Build metadata after <c>+</c> is ignored.
    /// </remarks>
    internal static class PackageVersion
    {
        /// <summary>
        /// Returns a negative number when <paramref name="a"/> is older than <paramref name="b"/>,
        /// zero when they are equal and a positive number when <paramref name="a"/> is newer.
        /// Empty or null versions sort before everything else.
        /// </summary>
        public static int Compare(string a, string b)
        {
            bool aEmpty = string.IsNullOrWhiteSpace(a);
            bool bEmpty = string.IsNullOrWhiteSpace(b);
            if (aEmpty || bEmpty)
            {
                return aEmpty == bEmpty ? 0 : (aEmpty ? -1 : 1);
            }

            string aCore, aPre, bCore, bPre;
            Split(a, out aCore, out aPre);
            Split(b, out bCore, out bPre);

            string[] aParts = aCore.Split('.');
            string[] bParts = bCore.Split('.');
            int length = Math.Max(aParts.Length, bParts.Length);
            for (int i = 0; i < length; i++)
            {
                int c = CompareIdentifier(i < aParts.Length ? aParts[i] : "0", i < bParts.Length ? bParts[i] : "0");
                if (c != 0)
                {
                    return c;
                }
            }

            // 1.0.0-preview < 1.0.0
            if (aPre.Length == 0 || bPre.Length == 0)
            {
                return aPre.Length == bPre.Length ? 0 : (aPre.Length == 0 ? 1 : -1);
            }

            string[] aIds = aPre.Split('.');
            string[] bIds = bPre.Split('.');
            int idLength = Math.Min(aIds.Length, bIds.Length);
            for (int i = 0; i < idLength; i++)
            {
                int c = CompareIdentifier(aIds[i], bIds[i]);
                if (c != 0)
                {
                    return c;
                }
            }

            return aIds.Length.CompareTo(bIds.Length);
        }

        /// <summary>Returns true when <paramref name="latest"/> is newer than <paramref name="installed"/>.</summary>
        public static bool IsUpdateAvailable(string installed, string latest)
        {
            return !string.IsNullOrWhiteSpace(installed)
                && !string.IsNullOrWhiteSpace(latest)
                && Compare(latest, installed) > 0;
        }

        private static void Split(string version, out string core, out string preRelease)
        {
            string v = version.Trim();
            if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                v = v.Substring(1);
            }

            int plus = v.IndexOf('+');
            if (plus >= 0)
            {
                v = v.Substring(0, plus);
            }

            int dash = v.IndexOf('-');
            core = dash >= 0 ? v.Substring(0, dash) : v;
            preRelease = dash >= 0 ? v.Substring(dash + 1) : string.Empty;
        }

        // Numeric identifiers compare as numbers and sort before alphanumeric ones (SemVer 11.4).
        private static int CompareIdentifier(string a, string b)
        {
            long an, bn;
            bool aNum = long.TryParse(a, NumberStyles.None, CultureInfo.InvariantCulture, out an);
            bool bNum = long.TryParse(b, NumberStyles.None, CultureInfo.InvariantCulture, out bn);
            if (aNum && bNum)
            {
                return an.CompareTo(bn);
            }

            if (aNum != bNum)
            {
                return aNum ? -1 : 1;
            }

            return string.CompareOrdinal(a, b);
        }
    }
}

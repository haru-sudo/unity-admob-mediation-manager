using System;
using System.Collections.Generic;
using System.Xml;

namespace AdMobMediationManager.Editor
{
    /// <summary>One native library an adapter asks External Dependency Manager to fetch.</summary>
    internal sealed class NativeDependency
    {
        public NativeDependency(AdPlatform platform, string name, string version)
        {
            Platform = platform;
            Name = name;
            Version = version;
        }

        /// <summary>Android (Gradle/Maven artifact) or iOS (CocoaPod).</summary>
        public AdPlatform Platform { get; }

        /// <summary>The Maven <c>group:artifact</c> or the CocoaPod name.</summary>
        public string Name { get; }

        /// <summary>The requested version as written in the file, without range brackets. May be empty.</summary>
        public string Version { get; }
    }

    /// <summary>
    /// Reads the <c>*Dependencies.xml</c> files that adapter plugins ship for
    /// External Dependency Manager for Unity (EDM4U).
    /// </summary>
    /// <remarks>
    /// These files are the most reliable place to learn which native adapter versions a Unity
    /// plugin version brings in, for example:
    /// <code>
    /// &lt;dependencies&gt;
    ///   &lt;androidPackages&gt;
    ///     &lt;androidPackage spec="com.google.ads.mediation:applovin:13.0.0.0" /&gt;
    ///   &lt;/androidPackages&gt;
    ///   &lt;iosPods&gt;
    ///     &lt;iosPod name="GoogleMobileAdsMediationAppLovin" version="13.0.0.0" /&gt;
    ///   &lt;/iosPods&gt;
    /// &lt;/dependencies&gt;
    /// </code>
    /// </remarks>
    internal static class DependencyFileParser
    {
        /// <summary>
        /// Parses the XML text of one dependencies file. Returns an empty list for malformed input
        /// rather than throwing, because a broken third-party file should not break the window.
        /// </summary>
        public static List<NativeDependency> Parse(string xml)
        {
            var result = new List<NativeDependency>();
            if (string.IsNullOrWhiteSpace(xml))
            {
                return result;
            }

            var document = new XmlDocument();
            try
            {
                // Never resolve external entities from a file on disk.
                document.XmlResolver = null;
                document.LoadXml(xml);
            }
            catch (XmlException)
            {
                return result;
            }

            foreach (XmlElement element in document.GetElementsByTagName("androidPackage"))
            {
                NativeDependency dependency = ParseAndroidSpec(element.GetAttribute("spec"));
                if (dependency != null)
                {
                    result.Add(dependency);
                }
            }

            foreach (XmlElement element in document.GetElementsByTagName("iosPod"))
            {
                string name = element.GetAttribute("name").Trim();
                if (name.Length > 0)
                {
                    result.Add(new NativeDependency(AdPlatform.IOS, name, CleanVersion(element.GetAttribute("version"))));
                }
            }

            return result;
        }

        /// <summary>
        /// Splits a Maven spec <c>group:artifact:version</c>. Returns null when it has fewer than two parts.
        /// </summary>
        public static NativeDependency ParseAndroidSpec(string spec)
        {
            if (string.IsNullOrWhiteSpace(spec))
            {
                return null;
            }

            string[] parts = spec.Trim().Split(':');
            if (parts.Length < 2)
            {
                return null;
            }

            string name = parts[0] + ":" + parts[1];
            string version = parts.Length >= 3 ? CleanVersion(parts[2]) : string.Empty;
            return new NativeDependency(AdPlatform.Android, name, version);
        }

        // "[13.0.0.0]" and "~> 13.0" become "13.0.0.0" and "13.0".
        private static string CleanVersion(string version)
        {
            if (version == null)
            {
                return string.Empty;
            }

            string v = version.Trim().Trim('[', ']', '(', ')');
            foreach (string op in new[] { "~>", ">=", "<=", "=", ">", "<" })
            {
                if (v.StartsWith(op, StringComparison.Ordinal))
                {
                    v = v.Substring(op.Length).Trim();
                    break;
                }
            }

            return v;
        }
    }
}

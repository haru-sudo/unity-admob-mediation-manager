using System;
using System.Collections.Generic;

namespace AdMobMediationManager.Editor
{
    /// <summary>
    /// Edits the text of <c>Packages/manifest.json</c> so that the project can resolve packages
    /// from a scoped registry (OpenUPM, where Google publishes the Google Mobile Ads plugin and
    /// its mediation adapters).
    /// </summary>
    /// <remarks>
    /// Works on strings only, so it can be tested without Unity. The caller reads and writes the file.
    /// </remarks>
    internal static class ManifestEditor
    {
        /// <summary>The outcome of <see cref="EnsureScopedRegistry"/>.</summary>
        internal struct Result
        {
            /// <summary>The manifest text to write back (equal to the input when nothing changed).</summary>
            public string Text;

            /// <summary>True when the registry or at least one scope had to be added.</summary>
            public bool Changed;
        }

        /// <summary>
        /// Makes sure the manifest lists a scoped registry at <paramref name="url"/> that covers
        /// every scope in <paramref name="scopes"/>.
        /// </summary>
        /// <param name="manifestJson">Current contents of manifest.json.</param>
        /// <param name="name">Display name to use when the registry has to be created.</param>
        /// <param name="url">Registry URL. An existing entry with this URL (ignoring a trailing slash) is reused.</param>
        /// <param name="scopes">Package name prefixes the registry must serve.</param>
        /// <exception cref="FormatException">The manifest is not valid JSON or not shaped like a manifest.</exception>
        public static Result EnsureScopedRegistry(string manifestJson, string name, string url, IEnumerable<string> scopes)
        {
            JsonObject root = MiniJson.Parse(manifestJson) as JsonObject;
            if (root == null)
            {
                throw new FormatException("manifest.json must contain a JSON object");
            }

            var registries = root["scopedRegistries"] as List<object>;
            if (registries == null)
            {
                if (root.ContainsKey("scopedRegistries"))
                {
                    throw new FormatException("\"scopedRegistries\" in manifest.json must be an array");
                }

                registries = new List<object>();
            }

            JsonObject registry = FindRegistry(registries, url);
            bool changed = false;
            if (registry == null)
            {
                registry = new JsonObject();
                registry["name"] = name;
                registry["url"] = url;
                registry["scopes"] = new List<object>();
                registries.Add(registry);
                changed = true;
            }

            var existingScopes = registry["scopes"] as List<object>;
            if (existingScopes == null)
            {
                existingScopes = new List<object>();
                registry["scopes"] = existingScopes;
                changed = true;
            }

            foreach (string scope in scopes)
            {
                if (!existingScopes.Contains(scope))
                {
                    existingScopes.Add(scope);
                    changed = true;
                }
            }

            if (!changed)
            {
                return new Result { Text = manifestJson, Changed = false };
            }

            // Unity expects scopedRegistries to sit next to dependencies; adding it at the end is fine.
            root["scopedRegistries"] = registries;
            return new Result { Text = MiniJson.Serialize(root), Changed = true };
        }

        /// <summary>
        /// Returns true when the manifest already has a registry at <paramref name="url"/> that
        /// covers <paramref name="packageName"/> through one of its scopes.
        /// </summary>
        public static bool RegistryCovers(string manifestJson, string url, string packageName)
        {
            JsonObject root = MiniJson.Parse(manifestJson) as JsonObject;
            var registries = root == null ? null : root["scopedRegistries"] as List<object>;
            JsonObject registry = registries == null ? null : FindRegistry(registries, url);
            var scopes = registry == null ? null : registry["scopes"] as List<object>;
            if (scopes == null)
            {
                return false;
            }

            foreach (object scope in scopes)
            {
                if (scope is string s && ScopeMatches(s, packageName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Applies Unity's scope rule: a scope matches a package whose name equals it or starts with it followed by a dot.
        /// </summary>
        public static bool ScopeMatches(string scope, string packageName)
        {
            return packageName == scope
                || (packageName.Length > scope.Length
                    && packageName.StartsWith(scope, StringComparison.Ordinal)
                    && packageName[scope.Length] == '.');
        }

        private static JsonObject FindRegistry(List<object> registries, string url)
        {
            string wanted = NormalizeUrl(url);
            foreach (object item in registries)
            {
                if (item is JsonObject registry
                    && registry["url"] is string existing
                    && NormalizeUrl(existing) == wanted)
                {
                    return registry;
                }
            }

            return null;
        }

        private static string NormalizeUrl(string url)
        {
            return url.Trim().TrimEnd('/').ToLowerInvariant();
        }
    }
}

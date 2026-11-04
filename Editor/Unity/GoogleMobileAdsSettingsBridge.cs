using UnityEditor;
using UnityEngine;

namespace AdMobMediationManager.Editor
{
    /// <summary>
    /// Reads and writes the App IDs stored by the Google Mobile Ads Unity plugin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The plugin keeps its settings in <c>Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset</c>
    /// (the same values as <c>Assets &gt; Google Mobile Ads &gt; Settings...</c>). Its settings class is
    /// internal to the plugin, so this bridge edits the serialized fields by name through
    /// <see cref="SerializedObject"/>, which also gives Undo support for free.
    /// </para>
    /// <para>
    /// If a future plugin version renames a field, <see cref="IsSupported"/> becomes false and the window
    /// falls back to opening the plugin's own settings.
    /// </para>
    /// </remarks>
    internal sealed class GoogleMobileAdsSettingsBridge
    {
        /// <summary>Where the plugin stores its settings asset.</summary>
        public const string AssetPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";

        /// <summary>The plugin's own settings menu item. Opening it also creates the asset if missing.</summary>
        public const string PluginSettingsMenu = "Assets/Google Mobile Ads/Settings...";

        private const string AndroidField = "adMobAndroidAppId";
        private const string IOSField = "adMobIOSAppId";
        private const string TrackingField = "userTrackingUsageDescription";

        private readonly SerializedObject serialized;

        private GoogleMobileAdsSettingsBridge(ScriptableObject asset)
        {
            Asset = asset;
            serialized = new SerializedObject(asset);
        }

        /// <summary>The settings asset itself.</summary>
        public ScriptableObject Asset { get; }

        /// <summary>True when the asset has the fields this bridge knows how to edit.</summary>
        public bool IsSupported
        {
            get { return serialized.FindProperty(AndroidField) != null && serialized.FindProperty(IOSField) != null; }
        }

        public string AndroidAppId
        {
            get { return Read(AndroidField); }
            set { Write(AndroidField, value); }
        }

        public string IOSAppId
        {
            get { return Read(IOSField); }
            set { Write(IOSField, value); }
        }

        /// <summary>The iOS App Tracking Transparency prompt text (NSUserTrackingUsageDescription).</summary>
        public string UserTrackingUsageDescription
        {
            get { return Read(TrackingField); }
            set { Write(TrackingField, value); }
        }

        /// <summary>True when the plugin keeps an ATT description in this version of the settings asset.</summary>
        public bool HasTrackingField
        {
            get { return serialized.FindProperty(TrackingField) != null; }
        }

        /// <summary>Loads the settings asset, or returns null when it does not exist yet.</summary>
        public static GoogleMobileAdsSettingsBridge Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetPath);
            return asset == null ? null : new GoogleMobileAdsSettingsBridge(asset);
        }

        /// <summary>
        /// Asks the Google Mobile Ads plugin to create (if needed) and select its settings asset.
        /// Returns false when the plugin's menu item is not available.
        /// </summary>
        public static bool OpenPluginSettings()
        {
            return EditorApplication.ExecuteMenuItem(PluginSettingsMenu);
        }

        /// <summary>Writes pending changes to disk.</summary>
        public void Save()
        {
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(Asset);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Re-reads the asset, discarding unsaved changes made through this bridge.</summary>
        public void Reload()
        {
            serialized.Update();
        }

        private string Read(string field)
        {
            SerializedProperty property = serialized.FindProperty(field);
            return property != null ? property.stringValue ?? string.Empty : string.Empty;
        }

        private void Write(string field, string value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null)
            {
                property.stringValue = value == null ? string.Empty : value.Trim();
            }
        }
    }
}

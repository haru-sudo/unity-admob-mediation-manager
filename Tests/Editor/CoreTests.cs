using System.Collections.Generic;
using NUnit.Framework;

namespace AdMobMediationManager.Editor.Tests
{
    // Unit tests for the Unity-independent logic. They run in the Unity Test Runner (EditMode).

    public class MiniJsonTests
    {
        [Test]
        public void RoundTrip_KeepsKeyOrderAndValues()
        {
            const string json = "{\"b\": 1, \"a\": [true, false, null, \"x\\n\\\"y\\\"\"], \"c\": {}}";
            var root = (JsonObject)MiniJson.Parse(json);
            var keys = new List<string>();
            foreach (var pair in root)
            {
                keys.Add(pair.Key);
            }

            CollectionAssert.AreEqual(new[] { "b", "a", "c" }, keys);
            Assert.AreEqual(1.0, root["b"]);
            var array = (List<object>)root["a"];
            Assert.AreEqual(true, array[0]);
            Assert.IsNull(array[2]);
            Assert.AreEqual("x\n\"y\"", array[3]);

            var again = (JsonObject)MiniJson.Parse(MiniJson.Serialize(root));
            Assert.AreEqual("x\n\"y\"", ((List<object>)again["a"])[3]);
        }

        [Test]
        public void Serialize_UsesUnityManifestStyle()
        {
            var root = new JsonObject();
            var deps = new JsonObject();
            deps["com.unity.ugui"] = "1.0.0";
            root["dependencies"] = deps;
            Assert.AreEqual("{\n  \"dependencies\": {\n    \"com.unity.ugui\": \"1.0.0\"\n  }\n}\n", MiniJson.Serialize(root));
        }

        [TestCase("{")]
        [TestCase("{\"a\" 1}")]
        [TestCase("[1,]")]
        [TestCase("{} x")]
        [TestCase("\"unterminated")]
        public void Parse_RejectsInvalidJson(string json)
        {
            Assert.Throws<System.FormatException>(() => MiniJson.Parse(json));
        }

        [Test]
        public void Parse_ReadsUnicodeEscapes()
        {
            Assert.AreEqual("\u00e9", MiniJson.Parse("\"\\u00e9\""));
        }
    }

    public class ManifestEditorTests
    {
        private const string Url = AdapterCatalog.RegistryUrl;
        private static readonly string[] Scopes = AdapterCatalog.RegistryScopes;

        [Test]
        public void AddsRegistry_WhenMissing()
        {
            const string manifest = "{\n  \"dependencies\": {\n    \"com.unity.ugui\": \"1.0.0\"\n  }\n}\n";
            ManifestEditor.Result result = ManifestEditor.EnsureScopedRegistry(manifest, "OpenUPM", Url, Scopes);

            Assert.IsTrue(result.Changed);
            StringAssert.Contains("\"scopedRegistries\"", result.Text);
            StringAssert.Contains("\"com.unity.ugui\": \"1.0.0\"", result.Text);
            Assert.IsTrue(ManifestEditor.RegistryCovers(result.Text, Url, "com.google.ads.mobile.mediation.applovin"));
            Assert.IsTrue(ManifestEditor.RegistryCovers(result.Text, Url, "com.google.external-dependency-manager"));
        }

        [Test]
        public void AddsOnlyMissingScopes_ToExistingRegistry()
        {
            const string manifest =
                "{\"scopedRegistries\": [{\"name\": \"x\", \"url\": \"https://package.openupm.com/\", \"scopes\": [\"com.google.ads.mobile\", \"com.other\"]}], \"dependencies\": {}}";
            ManifestEditor.Result result = ManifestEditor.EnsureScopedRegistry(manifest, "OpenUPM", Url, Scopes);

            Assert.IsTrue(result.Changed);
            var root = (JsonObject)MiniJson.Parse(result.Text);
            var registries = (List<object>)root["scopedRegistries"];
            Assert.AreEqual(1, registries.Count, "the existing registry must be reused, not duplicated");
            var scopes = (List<object>)((JsonObject)registries[0])["scopes"];
            CollectionAssert.AreEqual(new object[] { "com.google.ads.mobile", "com.other", "com.google.external-dependency-manager" }, scopes);
        }

        [Test]
        public void LeavesTextUntouched_WhenAlreadyConfigured()
        {
            const string manifest =
                "{ \"dependencies\": {}, \"scopedRegistries\": [ { \"name\": \"x\", \"url\": \"https://package.openupm.com\", " +
                "\"scopes\": [ \"com.google.ads.mobile\", \"com.google.external-dependency-manager\" ] } ] }";
            ManifestEditor.Result result = ManifestEditor.EnsureScopedRegistry(manifest, "OpenUPM", Url, Scopes);

            Assert.IsFalse(result.Changed);
            Assert.AreSame(manifest, result.Text);
        }

        [Test]
        public void ScopeMatching_FollowsUnityRules()
        {
            Assert.IsTrue(ManifestEditor.ScopeMatches("com.google.ads.mobile", "com.google.ads.mobile"));
            Assert.IsTrue(ManifestEditor.ScopeMatches("com.google.ads.mobile", "com.google.ads.mobile.mediation.unity"));
            Assert.IsFalse(ManifestEditor.ScopeMatches("com.google.ads.mobile", "com.google.ads.mobilefoo"));
        }

        [Test]
        public void RejectsManifestThatIsNotAnObject()
        {
            Assert.Throws<System.FormatException>(() => ManifestEditor.EnsureScopedRegistry("[]", "OpenUPM", Url, Scopes));
        }
    }

    public class PackageVersionTests
    {
        [TestCase("1.2.3", "1.2.3", 0)]
        [TestCase("1.2.3", "1.2.4", -1)]
        [TestCase("1.10.0", "1.9.9", 1)]
        [TestCase("8.0.0", "8.0.0.1", -1)]
        [TestCase("8.0", "8.0.0", 0)]
        [TestCase("2.0.0-preview.1", "2.0.0", -1)]
        [TestCase("2.0.0-preview.2", "2.0.0-preview.10", -1)]
        [TestCase("2.0.0-alpha", "2.0.0-1", 1)]
        [TestCase("v3.1.0", "3.1.0", 0)]
        [TestCase("3.1.0+build.5", "3.1.0", 0)]
        [TestCase("", "1.0.0", -1)]
        public void Compare_OrdersVersions(string a, string b, int expected)
        {
            Assert.AreEqual(expected, System.Math.Sign(PackageVersion.Compare(a, b)));
            Assert.AreEqual(-expected, System.Math.Sign(PackageVersion.Compare(b, a)));
        }

        [Test]
        public void UpdateAvailable_NeedsBothVersions()
        {
            Assert.IsTrue(PackageVersion.IsUpdateAvailable("1.0.0", "1.1.0"));
            Assert.IsFalse(PackageVersion.IsUpdateAvailable("1.1.0", "1.1.0"));
            Assert.IsFalse(PackageVersion.IsUpdateAvailable("", "1.1.0"));
            Assert.IsFalse(PackageVersion.IsUpdateAvailable("1.0.0", ""));
        }
    }

    public class DependencyFileParserTests
    {
        [Test]
        public void ReadsAndroidAndIOSDependencies()
        {
            const string xml =
                "<dependencies>" +
                "<androidPackages><androidPackage spec=\"com.google.ads.mediation:example:[1.2.3.0]\">" +
                "<repositories><repository>https://example.invalid/maven</repository></repositories></androidPackage></androidPackages>" +
                "<iosPods><iosPod name=\"GoogleMobileAdsMediationExample\" version=\"~> 1.2.3.0\" /></iosPods>" +
                "</dependencies>";
            List<NativeDependency> deps = DependencyFileParser.Parse(xml);

            Assert.AreEqual(2, deps.Count);
            Assert.AreEqual(AdPlatform.Android, deps[0].Platform);
            Assert.AreEqual("com.google.ads.mediation:example", deps[0].Name);
            Assert.AreEqual("1.2.3.0", deps[0].Version);
            Assert.AreEqual(AdPlatform.IOS, deps[1].Platform);
            Assert.AreEqual("GoogleMobileAdsMediationExample", deps[1].Name);
            Assert.AreEqual("1.2.3.0", deps[1].Version);
        }

        [Test]
        public void ReturnsEmpty_ForBrokenXml()
        {
            Assert.IsEmpty(DependencyFileParser.Parse("<dependencies><oops"));
            Assert.IsEmpty(DependencyFileParser.Parse(""));
        }

        [Test]
        public void AndroidSpecWithoutVersion_HasEmptyVersion()
        {
            NativeDependency dep = DependencyFileParser.ParseAndroidSpec("com.example:lib");
            Assert.AreEqual("com.example:lib", dep.Name);
            Assert.AreEqual(string.Empty, dep.Version);
            Assert.IsNull(DependencyFileParser.ParseAndroidSpec("nonsense"));
        }
    }

    public class IdTests
    {
        [Test]
        public void AppIdChecks()
        {
            Assert.AreEqual(IdCheck.Empty, AdMobIdValidator.CheckAppId("  "));
            Assert.AreEqual(IdCheck.TestId, AdMobIdValidator.CheckAppId(GoogleTestIds.AndroidAppId));
            Assert.AreEqual(IdCheck.TestId, AdMobIdValidator.CheckAppId(GoogleTestIds.IOSAppId));
            Assert.AreEqual(IdCheck.Valid, AdMobIdValidator.CheckAppId("ca-app-pub-0000000000000000~0000000000"));
            Assert.AreEqual(IdCheck.WrongKind, AdMobIdValidator.CheckAppId("ca-app-pub-0000000000000000/0000000000"));
            Assert.AreEqual(IdCheck.Malformed, AdMobIdValidator.CheckAppId("ca-app-pub-123~456"));
        }

        [Test]
        public void EveryGoogleTestAdUnit_IsAWellFormedTestId()
        {
            foreach (AdFormat format in System.Enum.GetValues(typeof(AdFormat)))
            {
                foreach (AdPlatform platform in System.Enum.GetValues(typeof(AdPlatform)))
                {
                    string id = GoogleTestIds.AdUnitId(format, platform);
                    Assert.AreEqual(IdCheck.TestId, AdMobIdValidator.CheckAdUnitId(id), format + " " + platform);
                }
            }
        }

        [Test]
        public void Resolver_PicksTheRightId()
        {
            const string android = "ca-app-pub-0000000000000000/1111111111";
            const string ios = "ca-app-pub-0000000000000000/2222222222";
            Assert.AreEqual(android, AdUnitIdResolver.Resolve(AdFormat.Banner, AdPlatform.Android, android, ios, false));
            Assert.AreEqual(ios, AdUnitIdResolver.Resolve(AdFormat.Banner, AdPlatform.IOS, android, ios, false));
            Assert.AreEqual(GoogleTestIds.AdUnitId(AdFormat.Banner, AdPlatform.IOS),
                AdUnitIdResolver.Resolve(AdFormat.Banner, AdPlatform.IOS, android, ios, true));
            Assert.AreEqual(AdUnitIdResolver.Unused, AdUnitIdResolver.Resolve(AdFormat.Banner, null, android, ios, true));
            Assert.AreEqual(AdUnitIdResolver.Unused, AdUnitIdResolver.Resolve(AdFormat.Banner, AdPlatform.Android, " ", ios, false));
        }
    }

    public class CatalogTests
    {
        [Test]
        public void PackageNamesAreUniqueAndPrefixed()
        {
            var names = new HashSet<string>();
            foreach (AdapterInfo adapter in AdapterCatalog.Adapters)
            {
                StringAssert.StartsWith(AdapterCatalog.AdapterPackagePrefix, adapter.PackageName);
                Assert.IsTrue(names.Add(adapter.PackageName), adapter.PackageName + " is listed twice");
                StringAssert.StartsWith("https://", adapter.DocsUrl);
            }
        }

        [Test]
        public void LegacyFoldersMatchIgnoringCase()
        {
            Assert.AreEqual("Unity Ads", AdapterCatalog.FindByLegacyFolder("unityads").DisplayName);
            Assert.AreEqual("Meta Audience Network", AdapterCatalog.FindByLegacyFolder("Meta").DisplayName);
            Assert.IsNull(AdapterCatalog.FindByLegacyFolder("NotANetwork"));
        }
    }

    public class IntegrationCheckTests
    {
        private static ProjectSnapshot Healthy()
        {
            return new ProjectSnapshot
            {
                GoogleMobileAdsSource = InstallSource.PackageManager,
                GoogleMobileAdsVersion = "10.0.0",
                ExternalDependencyManagerFound = true,
                SettingsAssetFound = true,
                AndroidAppId = "ca-app-pub-0000000000000000~0000000000",
                IOSAppId = "ca-app-pub-0000000000000000~1111111111",
                UserTrackingUsageDescription = "Used to show relevant ads.",
                Adapters = new List<InstalledAdapter>
                {
                    new InstalledAdapter(AdapterCatalog.Adapters[0], InstallSource.PackageManager, "1.0.0"),
                },
            };
        }

        private static List<IssueCode> Codes(ProjectSnapshot s)
        {
            return IntegrationCheck.Run(s).ConvertAll(i => i.Code);
        }

        [Test]
        public void HealthyProject_HasNoFindings()
        {
            CollectionAssert.IsEmpty(Codes(Healthy()));
        }

        [Test]
        public void MissingPlugin_IsTheOnlyErrorReported()
        {
            var s = new ProjectSnapshot();
            CollectionAssert.AreEqual(new[] { IssueCode.GoogleMobileAdsMissing }, Codes(s));
        }

        [Test]
        public void AppIdProblems_AreReportedPerPlatform()
        {
            ProjectSnapshot s = Healthy();
            s.AndroidAppId = "";
            s.IOSAppId = GoogleTestIds.IOSAppId;
            List<Issue> issues = IntegrationCheck.Run(s);

            Assert.AreEqual(IssueCode.AndroidAppIdMissing, issues[0].Code);
            Assert.AreEqual(Severity.Error, issues[0].Severity);
            Assert.AreEqual(IssueCode.IOSAppIdIsTest, issues[1].Code);
            Assert.AreEqual(Severity.Warning, issues[1].Severity);
        }

        [Test]
        public void PlatformsNotTargeted_AreSkipped()
        {
            ProjectSnapshot s = Healthy();
            s.IOSAppId = "";
            s.UserTrackingUsageDescription = "";
            s.TargetsIOS = false;
            CollectionAssert.IsEmpty(Codes(s));
        }

        [Test]
        public void DuplicateInstall_IsAnError_ReportedOnce()
        {
            ProjectSnapshot s = Healthy();
            AdapterInfo adapter = AdapterCatalog.Adapters[0];
            s.Adapters.Add(new InstalledAdapter(adapter, InstallSource.UnityPackage, ""));
            s.Adapters.Add(new InstalledAdapter(adapter, InstallSource.PackageManager, "1.0.0"));
            List<Issue> issues = IntegrationCheck.Run(s);

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(IssueCode.AdapterInstalledTwice, issues[0].Code);
            Assert.AreSame(adapter, issues[0].Adapter);
        }

        [Test]
        public void Findings_AreSortedBySeverity()
        {
            ProjectSnapshot s = Healthy();
            s.Adapters.Clear();                     // info
            s.ExternalDependencyManagerFound = false; // warning
            s.AndroidAppId = "bad";                 // error
            List<Issue> issues = IntegrationCheck.Run(s);

            CollectionAssert.AreEqual(
                new[] { Severity.Error, Severity.Warning, Severity.Info },
                issues.ConvertAll(i => i.Severity));
        }
    }
}

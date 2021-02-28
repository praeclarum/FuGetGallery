using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace FuGetGallery
{
    public static class NugetPackageSources
    {
        private static IConfiguration _configuration;

        private static readonly Lazy<IReadOnlyList<NugetPackageSource>> _packageSources
            = new Lazy<IReadOnlyList<NugetPackageSource>> (GetPackageSources);

        public static void Configure (IConfiguration configuration)
            => _configuration = configuration;

        public static IReadOnlyList<NugetPackageSource> PackageSources => _packageSources.Value;

        private static List<NugetPackageSource> GetPackageSources ()
        {
            var sources = new List<NugetPackageSource> ();

            var sections = _configuration.GetSection ("NugetPackageSources").GetChildren ();

            foreach (var urlConfigSection in sections) {
                var domainUrl = urlConfigSection["DomainUrl"];
                var displayName = urlConfigSection["DisplayName"];
                var displayUrl = urlConfigSection["DisplayUrl"];
                var packageDetailUrlFormat = urlConfigSection["PackageDetailsUrlFormat"];
                var queryUrlFormat = urlConfigSection["QueryUrlFormat"];
                var versionUrlFormat = urlConfigSection["VersionsUrlFormat"];
                var downloadUrlFormat = urlConfigSection["DownloadUrlFormat"];

                sources.Add(new NugetPackageSource(domainUrl, displayName, displayUrl,
                    packageDetailUrlFormat, queryUrlFormat, versionUrlFormat, downloadUrlFormat));
            }

            if (sources.Any ())
                return sources;

            // Default configuration if there is no configuration added in appsettings.
            return new List<NugetPackageSource> {
                new NugetPackageSource(
                    "https://www.nuget.org",
                    "Nuget Gallery",
                    "nuget.org",
                    "https://www.nuget.org/packages/{0}/{1}",
                    "https://api-v2v3search-0.nuget.org/query?prerelease=true&semVerLevel=2.0.0&q={0}",
                    "https://api.nuget.org/v3/registration3/{0}/index.json",
                    "https://www.nuget.org/api/v2/package/{0}/{1}")
            };
        }
    }
}

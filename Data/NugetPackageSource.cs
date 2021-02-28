namespace FuGetGallery
{
    public class NugetPackageSource
    {
        public NugetPackageSource (string domainUrl, string displayName, string displayUrl, string packageDetailsUrlFormat, string queryUrlFormat, string versionUrlFormat, string downloadUrlFormat)
        {
            DomainUrl = domainUrl;
            DisplayName = displayName;
            DisplayUrl = displayUrl;
            PackageDetailsUrlFormat = packageDetailsUrlFormat;
            QueryUrlFormat = queryUrlFormat;
            VersionUrlFormat = versionUrlFormat;
            DownloadUrlFormat = downloadUrlFormat;
        }

        public string DomainUrl { get; }
        public string DisplayName { get; }
        public string DisplayUrl { get; }
        public string PackageDetailsUrlFormat { get; }
        public string QueryUrlFormat { get; }
        public string VersionUrlFormat { get; }
        public string DownloadUrlFormat { get; }
    }
}

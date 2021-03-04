
namespace FuGetGallery
{
    public class PackageFile
    {
        public Entry ArchiveEntry { get; }
        public string FileName => ArchiveEntry?.FullName;
        public long SizeInBytes => ArchiveEntry != null ? ArchiveEntry.Length : 0;

        public PackageFile (Entry entry)
        {
            ArchiveEntry = entry;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Diagnostics;
using SQLite;

namespace FuGetGallery
{
    public class PackageDependents
    {
        public List<string> DependentIds { get; set; } = new List<string> ();

        static readonly PackageDependentsCache cache = new PackageDependentsCache ();

        public static Task<PackageDependents> GetAsync (object inputId, HttpClient client, CancellationToken token)
        {
            var cleanId = (inputId ?? "").ToString().Trim().ToLowerInvariant();
            return cache.GetAsync (cleanId, client, token);
        }

        public static void Invalidate (string packageId)
        {
            var cleanId = (packageId ?? "").ToString().Trim().ToLowerInvariant();
            cache.Invalidate (cleanId);
        }

        class PackageDependentsCache : DataCache<string, PackageDependents>
        {
            public PackageDependentsCache () : base (TimeSpan.FromMinutes (20)) { }
            
            protected override async Task<PackageDependents> GetValueAsync (string lowerId, HttpClient httpClient, CancellationToken token)
            {
                var deps = new PackageDependents ();
                try {
                    var db = new Database ();
                    foreach (var d in await db.Table<StoredPackageDependency>().Where (x => x.LowerPackageId == lowerId).ToListAsync ()) {
                        deps.DependentIds.Add (d.DependentPackageId);
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine (ex);
                }
                return deps;
            }
        }
    }

    class StoredPackageDependency
    {
        [Unique(Name="StoredPackageDependency_U", Order=0), NotNull]
        public string LowerPackageId { get; set; }
        [Unique(Name="StoredPackageDependency_U", Order=1), NotNull]
        public string LowerDependentPackageId { get; set; }

        [NotNull]
        public string DependentPackageId { get; set; }
    }
}

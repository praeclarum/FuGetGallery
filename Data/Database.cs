using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Linq;
using Mono.Cecil;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using SQLite;

namespace FuGetGallery
{
    public class Database : SQLiteAsyncConnection
    {
        public static readonly SQLiteConnectionString ConnectionString;

        static Database()
        {
            var homePath = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrEmpty(homePath)) {
                homePath = Environment.CurrentDirectory;
            }
            var dbsPath = Path.Combine(homePath, "Databases");
            if (!Directory.Exists(dbsPath)) {
                Directory.CreateDirectory(dbsPath);
            }
            var dbPath = Path.Combine(dbsPath, "FuGet.sqlite3");
            Console.WriteLine("DATABASE: {0}", dbPath);
            var cs = new SQLite.SQLiteConnectionString(
                dbPath,
                SQLite.SQLiteOpenFlags.Create | SQLite.SQLiteOpenFlags.ReadWrite | SQLite.SQLiteOpenFlags.FullMutex,
                storeDateTimeAsTicks: true);
            ConnectionString = cs;
        }

        public Database () : base (ConnectionString)
        {
            // Trace = true;
            // Tracer = x => Console.WriteLine(x);
        }

        public async Task MigrateAsync ()
        {
            await CreateTablesAsync (
                CreateFlags.None,
                typeof (StoredPackageDependency));
        }
    }
}

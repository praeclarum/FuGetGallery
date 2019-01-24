using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using ListDiff;
using Mono.Cecil;
using System.Linq;
using System.Net.Http;

namespace FuGetGallery
{
    public class ApiDiff
    {
        static readonly ApiDiffCache cache = new ApiDiffCache ();

        public PackageData Package { get; }
        public PackageTargetFramework Framework { get; }
        public PackageData OtherPackage { get; }
        public PackageTargetFramework OtherFramework { get; }
        public string Error { get; } = "";

        public List<NamespaceDiffInfo> Namespaces { get; } = new List<NamespaceDiffInfo> ();

        public class DiffInfo
        {
            public ListDiffActionType Action;
        }

        public class NamespaceDiffInfo : DiffInfo
        {
            public string Namespace;
            public List<TypeDiffInfo> Types = new List<TypeDiffInfo> ();
        }

        public class TypeDiffInfo : DiffInfo
        {
            public TypeDefinition Type;
            public PackageTargetFramework Framework;
            public List<MemberDiffInfo> Members = new List<MemberDiffInfo> ();
        }

        public class MemberDiffInfo : DiffInfo
        {
            public IMemberDefinition Member;
        }

        public ApiDiff (PackageData package, PackageTargetFramework framework, PackageData otherPackage, PackageTargetFramework otherFramework)
        {
            this.Package = package;
            this.Framework = framework;
            this.OtherPackage = otherPackage;
            this.OtherFramework = otherFramework;

            if (otherFramework == null) {
                Error = $"Could not find framework matching \"{framework?.Moniker}\" in {otherPackage?.Id} {otherPackage?.Version}.";
                return;
            }

            var asmDiff = OtherFramework.PublicAssemblies.Diff (Framework.PublicAssemblies, (x, y) => x.Definition.Name.Name == y.Definition.Name.Name);

            var types = new List<TypeDiffInfo> ();
            foreach (var aa in asmDiff.Actions) {
                IEnumerable<Tuple<TypeDefinition, PackageTargetFramework>> srcTypes;
                IEnumerable<Tuple<TypeDefinition, PackageTargetFramework>> destTypes;
                switch (aa.ActionType) {
                    case ListDiffActionType.Add:
                        srcTypes = Enumerable.Empty<Tuple<TypeDefinition, PackageTargetFramework>> ();
                        destTypes = aa.DestinationItem.PublicTypes.Select (x => Tuple.Create (x, Framework));
                        break;
                    case ListDiffActionType.Remove:
                        srcTypes = aa.SourceItem.PublicTypes.Select (x => Tuple.Create (x, OtherFramework));
                        destTypes = Enumerable.Empty<Tuple<TypeDefinition, PackageTargetFramework>> ();
                        break;
                    default:
                        srcTypes = aa.SourceItem.PublicTypes.Select (x => Tuple.Create (x, OtherFramework));
                        destTypes = aa.DestinationItem.PublicTypes.Select (x => Tuple.Create (x, Framework));
                        break;
                }
                if (aa.ActionType == ListDiffActionType.Remove)
                    continue;
                var typeDiff = srcTypes.Diff (destTypes, (x, y) => x.Item1.FullName == y.Item1.FullName);
                foreach (var ta in typeDiff.Actions) {
                    var ti = new TypeDiffInfo { Action = ta.ActionType };

                    IEnumerable<IMemberDefinition> srcMembers;
                    IEnumerable<IMemberDefinition> destMembers;
                    switch (ta.ActionType) {
                        case ListDiffActionType.Add:
                            ti.Type = ta.DestinationItem.Item1;
                            ti.Framework = ta.DestinationItem.Item2;
                            srcMembers = Enumerable.Empty<IMemberDefinition> ();
                            destMembers = ti.Type.GetPublicMembers ();
                            break;
                        case ListDiffActionType.Remove:
                            ti.Type = ta.SourceItem.Item1;
                            ti.Framework = ta.SourceItem.Item2;
                            srcMembers = ti.Type.GetPublicMembers ();
                            destMembers = Enumerable.Empty<IMemberDefinition> ();
                            break;
                        default:
                            ti.Type = ta.DestinationItem.Item1;
                            ti.Framework = ta.DestinationItem.Item2;
                            srcMembers = ta.SourceItem.Item1.GetPublicMembers ();
                            destMembers = ta.DestinationItem.Item1.GetPublicMembers ();
                            break;
                    }

                    if (ta.ActionType == ListDiffActionType.Remove) {
                        types.Add (ti);
                        continue;
                    }
                    var memDiff = srcMembers.Diff (destMembers, (x, y) => x.FullName == y.FullName);
                    foreach (var ma in memDiff.Actions) {
                        var mi = new MemberDiffInfo { Action = ma.ActionType };
                        switch (ma.ActionType) {
                            case ListDiffActionType.Add:
                                mi.Member = ma.DestinationItem;
                                ti.Members.Add (mi);
                                break;
                            case ListDiffActionType.Remove:
                                mi.Member = ma.SourceItem;
                                ti.Members.Add (mi);
                                break;
                            default:
                                mi.Member = ma.DestinationItem;
                                break;
                        }
                    }
                    if (ta.ActionType == ListDiffActionType.Add || ti.Members.Count > 0)
                        types.Add (ti);
                }
            }
            foreach (var ns in types.GroupBy (x => x.Type.Namespace)) {
                var ni = new NamespaceDiffInfo { Action = ListDiffActionType.Update };
                ni.Namespace = ns.Key;
                ni.Types.AddRange (ns);
                Namespaces.Add (ni);
            }
            Namespaces.Sort ((x, y) => string.Compare (x.Namespace, y.Namespace, StringComparison.Ordinal));
        }

        public static async Task<ApiDiff> GetAsync (
            object inputId,
            object inputVersion,
            object inputFramework,
            object inputOtherVersion,
            HttpClient httpClient,
            CancellationToken token)
        {
            var versions = await PackageVersions.GetAsync (inputId, httpClient, token).ConfigureAwait (false);
            var version = versions.GetVersion (inputVersion);
            var otherVersion = versions.GetVersion (inputOtherVersion);
            var framework = (inputFramework ?? "").ToString ().ToLowerInvariant ().Trim ();

            return await cache.GetAsync(
                    Tuple.Create (versions.LowerId, version.VersionString, framework),
                    otherVersion.VersionString,
                    httpClient,
                    token)
                .ConfigureAwait (false);
        }

        class ApiDiffCache : DataCache<Tuple<string, string, string>, string, ApiDiff>
        {
            public ApiDiffCache () : base (TimeSpan.FromDays (365)) { }

            protected override async Task<ApiDiff> GetValueAsync (
                Tuple<string, string, string> packageSpec,
                string otherVersion,
                HttpClient httpClient,
                CancellationToken token)
            {
                var packageId = packageSpec.Item1;
                var version = packageSpec.Item2;
                var inputFramework = packageSpec.Item3;

                var package = await PackageData.GetAsync (packageId, version, httpClient, token).ConfigureAwait (false);
                var otherPackage = await PackageData.GetAsync (packageId, otherVersion, httpClient, token).ConfigureAwait (false);

                var framework = package.FindClosestTargetFramework (inputFramework);
                var otherFramework = otherPackage.FindClosestTargetFramework (inputFramework);

                return await Task.Run (() => new ApiDiff (package, framework, otherPackage, otherFramework)).ConfigureAwait (false);
            }
        }
    }
}

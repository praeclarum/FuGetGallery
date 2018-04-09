using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Linq;
using Mono.Cecil;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace FuGetGallery
{
    public class PackageAssembly : PackageFile
    {
        readonly PackageTargetFramework framework;
        readonly Lazy<AssemblyDefinition> definition;
        readonly Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> decompiler;
        readonly Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> idecompiler;
        readonly ConcurrentDictionary<TypeDefinition, TypeDocumentation> typeDocs =
            new ConcurrentDictionary<TypeDefinition, TypeDocumentation> ();

        readonly ICSharpCode.Decompiler.CSharp.OutputVisitor.CSharpFormattingOptions format;

        public PackageAssemblyXmlDocs XmlDocs { get; set; }

        public AssemblyDefinition Definition => definition.Value;
        
        public bool IsBuildAssembly => ArchiveEntry.FullName.StartsWith ("build/", StringComparison.InvariantCultureIgnoreCase);

        public PackageAssembly (ZipArchiveEntry entry, PackageTargetFramework framework)
            : base (entry)
        {
            this.framework = framework;            
            definition = new Lazy<AssemblyDefinition> (() => {
                if (ArchiveEntry == null)
                    return null;
                var ms = new MemoryStream ((int)ArchiveEntry.Length);
                using (var es = ArchiveEntry.Open ()) {
                    es.CopyTo (ms);
                    ms.Position = 0;
                }
                return AssemblyDefinition.ReadAssembly (ms, new ReaderParameters {
                    AssemblyResolver = framework.AssemblyResolver,
                });
            }, true);
            format = ICSharpCode.Decompiler.CSharp.OutputVisitor.FormattingOptionsFactory.CreateMono ();
            format.SpaceBeforeMethodCallParentheses = false;
            format.SpaceBeforeMethodDeclarationParentheses = false;
            format.SpaceBeforeConstructorDeclarationParentheses = false;
            idecompiler = new Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> (() => {
                var m = Definition?.MainModule;
                if (m == null)
                    return null;
                return new ICSharpCode.Decompiler.CSharp.CSharpDecompiler (m, new ICSharpCode.Decompiler.DecompilerSettings {
                    ShowXmlDocumentation = false,
                    ThrowOnAssemblyResolveErrors = false,
                    AlwaysUseBraces = false,
                    CSharpFormattingOptions = format,
                    ExpandMemberDefinitions = false,
                    DecompileMemberBodies = false,
                    UseExpressionBodyForCalculatedGetterOnlyProperties = true,
                });
            }, true);
            decompiler = new Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> (() => {
                var m = Definition?.MainModule;
                if (m == null)
                    return null;
                return new ICSharpCode.Decompiler.CSharp.CSharpDecompiler (m, new ICSharpCode.Decompiler.DecompilerSettings {
                    ShowXmlDocumentation = false,
                    ThrowOnAssemblyResolveErrors = false,
                    AlwaysUseBraces = false,
                    CSharpFormattingOptions = format,
                    ExpandMemberDefinitions = true,
                    DecompileMemberBodies = true,
                    UseExpressionBodyForCalculatedGetterOnlyProperties = true,
                });
            }, true);
        }

        public TypeDocumentation GetTypeDocumentation (TypeDefinition typeDefinition)
        {
            if (typeDocs.TryGetValue (typeDefinition, out var docs)) {
                return docs;
            }
            var asmName = typeDefinition.Module.Assembly.Name.Name;
            docs = new TypeDocumentation (typeDefinition, framework, XmlDocs, decompiler, idecompiler, format);
            typeDocs.TryAdd (typeDefinition, docs);
            return docs;
        }
    }
}

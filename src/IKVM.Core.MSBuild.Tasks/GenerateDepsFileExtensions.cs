namespace IKVM.Core.MSBuild.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Microsoft.Extensions.DependencyModel;

    public class GenerateDepsFileExtensions : Task
    {

        const string METADATA_LIBRARY_NAME = "LibraryName";
        const string METADATA_LIBRARY_VERSION = "LibraryVersion";
        const string METADATA_LIBRARY_TYPE = "LibraryType";
        const string METADATA_LIBRARY_ASSET_RUNTIME = "Runtime";
        const string METADATA_LIBRARY_ASSET_PATH = "AssetPath";
        const string METADATA_LIBRARY_ASSET_ASSEMBLYVERSION = "AssemblyVersion";
        const string METADATA_LIBRARY_ASSET_FILEVERSION = "FileVersion";

        [Required]
        public string DepsFilePath { get; set; }

        public ITaskItem[] AdditionalRuntimeLibraryAssets { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] AdditionalRuntimeNativeAssets { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// Reads in the .deps.json file.
        /// </summary>
        /// <returns></returns>
        DependencyContext ReadDepsFile()
        {
            using var rdr = File.OpenRead(DepsFilePath);
            return new DependencyContextJsonReader().Read(rdr);
        }

        /// <summary>
        /// Writes out the .deps.json file.
        /// </summary>
        /// <param name="context"></param>
        void WriteDepsFile(DependencyContext context)
        {
            using var wrt = File.OpenWrite(DepsFilePath);
            new DependencyContextWriter().Write(context, wrt);
        }

        public override bool Execute()
        {
            WriteDepsFile(BuildDependencyContext(ReadDepsFile()));
            return true;
        }

        /// <summary>
        /// Builds the new <see cref="DependencyContext"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        DependencyContext BuildDependencyContext(DependencyContext context)
        {
            return new DependencyContext(BuildTargetInfo(context), BuildCompilationOptions(context), BuildCompileLibraries(context), BuildRuntimeLibraries(context), BuildRuntimeGraph(context));
        }

        TargetInfo BuildTargetInfo(DependencyContext context)
        {
            return context.Target;
        }

        CompilationOptions BuildCompilationOptions(DependencyContext context)
        {
            return context.CompilationOptions;
        }

        IEnumerable<CompilationLibrary> BuildCompileLibraries(DependencyContext context)
        {
            return context.CompileLibraries;
        }

        IEnumerable<RuntimeLibrary> BuildRuntimeLibraries(DependencyContext context)
        {
            if ((AdditionalRuntimeNativeAssets == null || AdditionalRuntimeNativeAssets.Length == 0) && (AdditionalRuntimeLibraryAssets == null || AdditionalRuntimeLibraryAssets.Length == 0))
                return context.RuntimeLibraries;
            else
                return BuildRuntimeLibrariesWithAdditional(context);
        }

        IEnumerable<RuntimeLibrary> BuildRuntimeLibrariesWithAdditional(DependencyContext context)
        {
            var names = context.RuntimeLibraries
                .Select(i => i.Name)
                .Concat(AdditionalRuntimeLibraryAssets.Select(i => i.GetMetadata(METADATA_LIBRARY_NAME)).Where(i => i != null))
                .Concat(AdditionalRuntimeNativeAssets.Select(i => i.GetMetadata(METADATA_LIBRARY_NAME)).Where(i => i != null))
                .Distinct();
            foreach (var name in names)
                yield return BuildRuntimeLibraryWithAdditional(context, name);
        }

        RuntimeLibrary BuildRuntimeLibraryWithAdditional(DependencyContext context, string name)
        {
            string version = null;
            string type = null;
            var path = "";
            var hash = "";
            var hashPath = "";
            var serviceable = false;
            var runtimeAssemblyGroups = BuildRuntimeLibraryRuntimeAssemblyGroupsWithAdditional(context, name).ToList();
            var nativeLibraryGroups = BuildRuntimeLibraryNativeAssetGroupsWithAdditional(context, name).ToList();
            var resourceAssemblies = new List<ResourceAssembly>();
            var dependencies = new List<Dependency>();

            // merge in existing items
            var i = context.RuntimeLibraries.FirstOrDefault(i => i.Name == name);
            if (i != null)
            {
                version = i.Version;
                type = i.Type;
                path = i.Path;
                hash = i.Hash;
                hashPath = i.HashPath;
                serviceable = i.Serviceable;
                resourceAssemblies.AddRange(i.ResourceAssemblies);
                dependencies.AddRange(i.Dependencies);
            }

            // merge in new items
            foreach (var addl in AdditionalRuntimeLibraryAssets.Where(i => i.GetMetadata(METADATA_LIBRARY_NAME) == name))
            {
                if (addl.GetMetadata(METADATA_LIBRARY_VERSION) is string _version and not null)
                    version ??= _version;
                if (addl.GetMetadata(METADATA_LIBRARY_TYPE) is string _type and not null)
                    type ??= _type;
            }

            // merge in new items
            foreach (var addl in AdditionalRuntimeNativeAssets.Where(i => i.GetMetadata(METADATA_LIBRARY_NAME) == name))
            {
                if (addl.GetMetadata(METADATA_LIBRARY_VERSION) is string _version and not null)
                    version ??= _version;
                if (addl.GetMetadata(METADATA_LIBRARY_TYPE) is string _type and not null)
                    type ??= _type;
            }

            if (string.IsNullOrEmpty(version))
                version = "0.0.0";
            if (string.IsNullOrEmpty(type))
                type = null;
            if (string.IsNullOrEmpty(hash))
                hash = "";
            if (string.IsNullOrEmpty(hashPath))
                hashPath = null;

            return new RuntimeLibrary(type, name, version, hash, runtimeAssemblyGroups, nativeLibraryGroups, resourceAssemblies, dependencies, serviceable, path, hashPath);
        }

        IEnumerable<RuntimeAssetGroup> BuildRuntimeLibraryRuntimeAssemblyGroupsWithAdditional(DependencyContext context, string name)
        {
            var keys = context.RuntimeLibraries
                .SelectMany(i => i.RuntimeAssemblyGroups.Select(j => new { i.Name, j.Runtime }))
                .Concat(AdditionalRuntimeLibraryAssets.Select(i => new { Name = i.GetMetadata(METADATA_LIBRARY_NAME), Runtime = i.GetMetadata(METADATA_LIBRARY_ASSET_RUNTIME) }).Where(i => i.Name != null))
                .Where(i => i.Name == name)
                .Distinct();
            foreach (var key in keys)
                yield return BuildRuntimeLibraryRuntimeAssemblyGroupWithAdditional(context, key.Name, key.Runtime);
        }

        RuntimeAssetGroup BuildRuntimeLibraryRuntimeAssemblyGroupWithAdditional(DependencyContext context, string name, string runtime)
        {
            var runtimeFiles = new List<RuntimeFile>();

            var i = context.RuntimeLibraries.Where(i => i.Name == name).SelectMany(i => i.RuntimeAssemblyGroups).FirstOrDefault(i => i.Runtime == runtime);
            if (i != null)
            {
                runtimeFiles.AddRange(i.RuntimeFiles);
            }

            // merge in new items
            foreach (var addl in AdditionalRuntimeLibraryAssets.Where(i => i.GetMetadata(METADATA_LIBRARY_NAME) == name && i.GetMetadata(METADATA_LIBRARY_ASSET_RUNTIME) == runtime))
            {
                var path = addl.GetMetadata(METADATA_LIBRARY_ASSET_PATH);
                if (string.IsNullOrEmpty(path))
                    path = addl.ItemSpec;
                if (string.IsNullOrEmpty(path))
                    path = null;

                // find existing runtime file and remove
                var file = runtimeFiles.FirstOrDefault(i => i.Path == path);
                if (file != null)
                    runtimeFiles.Remove(file);

                var assemblyVersion = addl.GetMetadata(METADATA_LIBRARY_ASSET_ASSEMBLYVERSION) ?? file?.AssemblyVersion;
                if (string.IsNullOrEmpty(assemblyVersion))
                    assemblyVersion = null;

                var fileVersion = addl.GetMetadata(METADATA_LIBRARY_ASSET_FILEVERSION) ?? file?.FileVersion;
                if (string.IsNullOrEmpty(fileVersion))
                    fileVersion = null;

                runtimeFiles.Add(new RuntimeFile(path, assemblyVersion, fileVersion));
            }

            return new RuntimeAssetGroup(runtime, runtimeFiles);
        }

        IEnumerable<RuntimeAssetGroup> BuildRuntimeLibraryNativeAssetGroupsWithAdditional(DependencyContext context, string name)
        {
            var keys = context.RuntimeLibraries
                .SelectMany(i => i.NativeLibraryGroups.Select(j => new { i.Name, j.Runtime }))
                .Concat(AdditionalRuntimeNativeAssets.Select(i => new { Name = i.GetMetadata(METADATA_LIBRARY_NAME), Runtime = i.GetMetadata(METADATA_LIBRARY_ASSET_RUNTIME) }).Where(i => i.Name != null))
                .Where(i => i.Name == name)
                .Distinct();
            foreach (var key in keys)
                yield return BuildRuntimeLibraryNativeAssetGroupWithAdditional(context, key.Name, key.Runtime);
        }

        RuntimeAssetGroup BuildRuntimeLibraryNativeAssetGroupWithAdditional(DependencyContext context, string name, string runtime)
        {
            var runtimeFiles = new List<RuntimeFile>();

            var i = context.RuntimeLibraries.Where(i => i.Name == name).SelectMany(i => i.NativeLibraryGroups).FirstOrDefault(i => i.Runtime == runtime);
            if (i != null)
            {
                runtimeFiles.AddRange(i.RuntimeFiles);
            }

            // merge in new items
            foreach (var addl in AdditionalRuntimeNativeAssets.Where(i => i.GetMetadata(METADATA_LIBRARY_NAME) == name && i.GetMetadata(METADATA_LIBRARY_ASSET_RUNTIME) == runtime))
            {
                var path = addl.GetMetadata(METADATA_LIBRARY_ASSET_PATH);
                if (string.IsNullOrEmpty(path))
                    path = addl.ItemSpec;
                if (string.IsNullOrEmpty(path))
                    path = null;

                // find existing runtime file and remove
                var file = runtimeFiles.FirstOrDefault(i => i.Path == path);
                if (file != null)
                    runtimeFiles.Remove(file);

                var assemblyVersion = addl.GetMetadata(METADATA_LIBRARY_ASSET_ASSEMBLYVERSION) ?? file?.AssemblyVersion;
                if (string.IsNullOrEmpty(assemblyVersion))
                    assemblyVersion = null;

                var fileVersion = addl.GetMetadata(METADATA_LIBRARY_ASSET_FILEVERSION) ?? file?.FileVersion;
                if (string.IsNullOrEmpty(fileVersion))
                    fileVersion = null;

                runtimeFiles.Add(new RuntimeFile(path, assemblyVersion, fileVersion));
            }

            return new RuntimeAssetGroup(runtime, runtimeFiles);
        }

        IEnumerable<RuntimeFallbacks> BuildRuntimeGraph(DependencyContext context)
        {
            return context.RuntimeGraph;
        }

    }

}

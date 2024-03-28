namespace IKVM.Core.MSBuild.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection.Metadata;

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

        /// <summary>
        /// Path to existing .deps.json file to rewrite.
        /// </summary>
        [Required]
        public string DepsFilePath { get; set; }

        /// <summary>
        /// Additional library assets.
        /// </summary>
        public ITaskItem[] AdditionalRuntimeLibraryAssets { get; set; } = Array.Empty<ITaskItem>();

        /// <summary>
        /// Additional native assets.
        /// </summary>
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
            using var wrt = File.Open(DepsFilePath, FileMode.Create);
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
                .Select(i => i.Name ?? "")
                .Concat(AdditionalRuntimeLibraryAssets.Select(i => i.GetMetadata(METADATA_LIBRARY_NAME) ?? "").Where(i => i != ""))
                .Concat(AdditionalRuntimeNativeAssets.Select(i => i.GetMetadata(METADATA_LIBRARY_NAME) ?? "").Where(i => i != ""))
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
            var i = context.RuntimeLibraries.FirstOrDefault(i => (i.Name ?? "") == name);
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
            foreach (var addl in AdditionalRuntimeLibraryAssets.Where(i => (i.GetMetadata(METADATA_LIBRARY_NAME) ?? "") == name))
            {
                if (addl.GetMetadata(METADATA_LIBRARY_VERSION) is string _version and not null)
                    version ??= _version;
                if (addl.GetMetadata(METADATA_LIBRARY_TYPE) is string _type and not null)
                    type ??= _type;
            }

            // merge in new items
            foreach (var addl in AdditionalRuntimeNativeAssets.Where(i => (i.GetMetadata(METADATA_LIBRARY_NAME) ?? "") == name))
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
                .SelectMany(i => i.RuntimeAssemblyGroups.Select(j => new { i.Name, Runtime = j.Runtime ?? "" }))
                .Concat(AdditionalRuntimeLibraryAssets.Select(i => new { Name = i.GetMetadata(METADATA_LIBRARY_NAME) ?? "", Runtime = i.GetMetadata(METADATA_LIBRARY_ASSET_RUNTIME) ?? "" }).Where(i => i.Name != ""))
                .Where(i => i.Name == name)
                .Distinct();
            foreach (var key in keys)
                yield return BuildRuntimeLibraryRuntimeAssemblyGroupWithAdditional(context, key.Name, key.Runtime);
        }

        RuntimeAssetGroup BuildRuntimeLibraryRuntimeAssemblyGroupWithAdditional(DependencyContext context, string name, string runtime)
        {
            var runtimeFiles = new List<RuntimeFile>();

            var i = context.RuntimeLibraries.Where(i => (i.Name ?? "") == name).SelectMany(i => i.RuntimeAssemblyGroups).FirstOrDefault(i => (i.Runtime ?? "") == runtime);
            if (i != null)
            {
                runtimeFiles.AddRange(i.RuntimeFiles);
            }

            // merge in new items
            foreach (var addl in AdditionalRuntimeLibraryAssets.Where(i => (i.GetMetadata(METADATA_LIBRARY_NAME) ?? "") == name && (i.GetMetadata(METADATA_LIBRARY_ASSET_RUNTIME) ?? "") == runtime))
            {
                var path = addl.GetMetadata(METADATA_LIBRARY_ASSET_PATH) ?? "";
                if (string.IsNullOrEmpty(path))
                    path = addl.ItemSpec;
                if (string.IsNullOrEmpty(path))
                    path = "";

                // normalize Windows paths
                path = path.Replace('\\', '/');

                // find existing runtime file and remove
                var file = runtimeFiles.FirstOrDefault(i => (i.Path ?? "") == path);
                if (file != null)
                    runtimeFiles.Remove(file);

                var assemblyVersion = addl.GetMetadata(METADATA_LIBRARY_ASSET_ASSEMBLYVERSION) ?? file?.AssemblyVersion;
                if (string.IsNullOrEmpty(assemblyVersion))
                    if (TryLoadAssemblyVersion(addl, out var v))
                        assemblyVersion = v;

                var fileVersion = addl.GetMetadata(METADATA_LIBRARY_ASSET_FILEVERSION) ?? file?.FileVersion;
                if (string.IsNullOrEmpty(fileVersion))
                    if (TryLoadFileVersion(addl, out var v))
                        fileVersion = v;

                runtimeFiles.Add(new RuntimeFile(path, assemblyVersion, fileVersion));
            }

            return new RuntimeAssetGroup(runtime, runtimeFiles);
        }

        IEnumerable<RuntimeAssetGroup> BuildRuntimeLibraryNativeAssetGroupsWithAdditional(DependencyContext context, string name)
        {
            var keys = context.RuntimeLibraries
                .SelectMany(i => i.NativeLibraryGroups.Select(j => new { Name = i.Name ?? "", Runtime = j.Runtime ?? "" }))
                .Concat(AdditionalRuntimeNativeAssets.Select(i => new { Name = i.GetMetadata(METADATA_LIBRARY_NAME) ?? "", Runtime = i.GetMetadata(METADATA_LIBRARY_ASSET_RUNTIME) ?? "" }).Where(i => i.Name != ""))
                .Where(i => i.Name == name)
                .Distinct();
            foreach (var key in keys)
                yield return BuildRuntimeLibraryNativeAssetGroupWithAdditional(context, key.Name, key.Runtime);
        }

        RuntimeAssetGroup BuildRuntimeLibraryNativeAssetGroupWithAdditional(DependencyContext context, string name, string runtime)
        {
            var runtimeFiles = new List<RuntimeFile>();

            var i = context.RuntimeLibraries.Where(i => (i.Name ?? "") == name).SelectMany(i => i.NativeLibraryGroups).FirstOrDefault(i => (i.Runtime ?? "") == runtime);
            if (i != null)
            {
                runtimeFiles.AddRange(i.RuntimeFiles);
            }

            // merge in new items
            foreach (var addl in AdditionalRuntimeNativeAssets.Where(i => (i.GetMetadata(METADATA_LIBRARY_NAME) ?? "") == name && (i.GetMetadata(METADATA_LIBRARY_ASSET_RUNTIME) ?? "") == runtime))
            {
                var path = addl.GetMetadata(METADATA_LIBRARY_ASSET_PATH) ?? "";
                if (string.IsNullOrEmpty(path))
                    path = addl.ItemSpec;
                if (string.IsNullOrEmpty(path))
                    path = "";

                // normalize Windows paths
                path = path.Replace('\\', '/');

                // find existing runtime file and remove
                var file = runtimeFiles.FirstOrDefault(i => (i.Path ?? "") == path);
                if (file != null)
                    runtimeFiles.Remove(file);

                var assemblyVersion = addl.GetMetadata(METADATA_LIBRARY_ASSET_ASSEMBLYVERSION) ?? file?.AssemblyVersion;
                if (string.IsNullOrEmpty(assemblyVersion))
                    if (TryLoadAssemblyVersion(addl, out var v))
                        assemblyVersion = v;

                var fileVersion = addl.GetMetadata(METADATA_LIBRARY_ASSET_FILEVERSION) ?? file?.FileVersion;
                if (string.IsNullOrEmpty(fileVersion))
                    if (TryLoadFileVersion(addl, out var v))
                        fileVersion = v;

                runtimeFiles.Add(new RuntimeFile(path, assemblyVersion, fileVersion));
            }

            return new RuntimeAssetGroup(runtime, runtimeFiles);
        }

        /// <summary>
        /// Attempts to get the assembly version of an item.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="assemblyVersion"></param>
        /// <returns></returns>
        bool TryLoadAssemblyVersion(ITaskItem item, out string assemblyVersion)
        {
            if (File.Exists(item.ItemSpec))
                return TryLoadAssemblyVersion(item.ItemSpec, out assemblyVersion);

            if (item.GetMetadata("FullPath") is string fullPath && File.Exists(fullPath))
                return TryLoadAssemblyVersion(fullPath, out assemblyVersion);

            assemblyVersion = null;
            return false;
        }

        /// <summary>
        /// Attempts to get the assembly version of a file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="assemblyVersion"></param>
        /// <returns></returns>
        bool TryLoadAssemblyVersion(string path, out string assemblyVersion)
        {
            try
            {
                assemblyVersion = MetadataReader.GetAssemblyName(path)?.Version?.ToString();
                return true;
            }
            catch
            {
                assemblyVersion = null;
                return false;
            }
        }


        /// <summary>
        /// Attempts to get the file version of an item.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="assemblyVersion"></param>
        /// <returns></returns>
        bool TryLoadFileVersion(ITaskItem item, out string fileVersion)
        {
            if (File.Exists(item.ItemSpec))
                return TryLoadFileVersion(item.ItemSpec, out fileVersion);

            if (item.GetMetadata("FullPath") is string fullPath && File.Exists(fullPath))
                return TryLoadFileVersion(fullPath, out fileVersion);

            fileVersion = null;
            return false;
        }

        /// <summary>
        /// Tries to load the file version of a file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileVersion"></param>
        /// <returns></returns>
        bool TryLoadFileVersion(string path, out string fileVersion)
        {
            fileVersion = null;
            return false;
        }

        IEnumerable<RuntimeFallbacks> BuildRuntimeGraph(DependencyContext context)
        {
            return context.RuntimeGraph;
        }

    }

}

namespace IKVM.Core.MSBuild.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    /// <summary>
    /// For a grouping of items by key, each with a specified RuntimeIdentifier, choose the nearest matching item from the group.
    /// </summary>
    public class MatchCompatibleRuntimeIdentifierItems : Task
    {

        class RuntimesElement
        {

            [JsonPropertyName("runtimes")]
            public Dictionary<string, RuntimeElement> Runtimes { get; set; }

        }

        class RuntimeElement
        {

            [JsonPropertyName("#import")]
            public string[] Imports { get; set; }

            [JsonIgnore]
            public ImmutableHashSet<string> ImportedBy { get; set; } = ImmutableHashSet<string>.Empty;

        }

        static Dictionary<string, RuntimeElement> runtimeJsonElement;

        /// <summary>
        /// Gets the 'runtime.json' file.
        /// </summary>
        static Dictionary<string, RuntimeElement> RuntimeJson => runtimeJsonElement ??= GetRuntimeJson();

        /// <summary>
        /// Loads the 'runtime.json' file.
        /// </summary>
        /// <returns></returns>
        static Dictionary<string, RuntimeElement> GetRuntimeJson()
        {
            using var stream = typeof(MatchCompatibleRuntimeIdentifierItems).Assembly.GetManifestResourceStream("runtime.json");
            var g = JsonSerializer.Deserialize<RuntimesElement>(stream);
            return g.Runtimes;
        }

        /// <summary>
        /// Recurses into the runtimes graph and attempts to resolve the RID and all RIDs it imports.
        /// </summary>
        /// <param name="rid"></param>
        /// <returns></returns>
        static IEnumerable<string> GetImportsRecursive(string rid)
        {
            if (RuntimeJson.TryGetValue(rid, out var node) == false)
                yield break;

            // return self
            yield return rid;
            
            // return each imported identifier
            foreach (var i in node.Imports)
                foreach (var j in GetImportsRecursive(i))
                    yield return j;
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="importRid"/> is imported directly or indirectly by <paramref name="rid"/>.
        /// </summary>
        /// <param name="rid"></param>
        /// <param name="importRid"></param>
        /// <returns></returns>
        static bool IsImport(string rid, string importRid)
        {
            return GetImportsRecursive(rid).Contains(importRid);
        }

        /// <summary>
        /// Specified target runtime identifiers.
        /// </summary>
        [Required]
        public string TargetRuntimeIdentifiers { get; set; }

        /// <summary>
        /// Name of the metadata property on the items by which to group.
        /// </summary>
        public string GroupKeyMetadataName { get; set; }

        /// <summary>
        /// Name of the metadata property on the items that deterine their compatible RID.
        /// </summary>
        public string RuntimeIdentifierMetadataName { get; set; } = "RuntimeIdentifier";

        /// <summary>
        /// Name of the metadata item to set to "true" when compatible with the specified target runtime identifiers.
        /// </summary>
        public string CompatibleMetadataName { get; set; } = "_Compatible";

        /// <summary>
        /// Name of the metadata item to set when multiple items in a group are compatible with the specified target runtime identifiers.
        /// </summary>
        public string ManyCompatibleMetadataName { get; set; } = "_ManyCompatible";

        /// <summary>
        /// Items to group and match by nearest runtime identifier.
        /// </summary>
        [Required]
        [Output]
        public ITaskItem[] Items { get; set; }

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            MatchCompatibleItems(TargetRuntimeIdentifiers.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            return true;
        }

        /// <summary>
        /// For each group of items defined by the GroupKey, append metadata
        /// </summary>
        /// <param name="targetRids"></param>
        /// <exception cref="NotImplementedException"></exception>
        void MatchCompatibleItems(ICollection<string> targetRids)
        {
            if (targetRids is null)
                throw new ArgumentNullException(nameof(targetRids));
            if (targetRids.Count == 0)
                throw new ArgumentOutOfRangeException(nameof(targetRids));

            // group items and match group
            foreach (var group in Items.GroupBy(i => GroupKeyMetadataName != null ? (i.GetMetadata(GroupKeyMetadataName) ?? "") : ""))
                MatchCompatibleItems(group, targetRids);
        }

        /// <summary>
        /// Marks the matching nearest RID containing item.
        /// </summary>
        /// <param name="items"></param>
        /// <param name="targetRids"></param>
        /// <exception cref="NotImplementedException"></exception>
        void MatchCompatibleItems(IEnumerable<ITaskItem> items, IEnumerable<string> targetRids)
        {
            foreach (var targetRid in targetRids)
                MatchCompatibleItems(items, targetRid);

            // if multiple items in the group were matched, set the unique match value to false
            var matchedItems = items.Where(i => i.GetMetadata(CompatibleMetadataName) == "true").ToList();
            foreach (var matchedItem in matchedItems)
                matchedItem.SetMetadata(ManyCompatibleMetadataName, matchedItems.Count == 1 ? "false" : "true");
        }

        /// <summary>
        /// Marks the nearest matching item(s) within the group as matched.
        /// </summary>
        /// <param name="items"></param>
        /// <param name="targetRid"></param>
        void MatchCompatibleItems(IEnumerable<ITaskItem> items, string targetRid)
        {
            foreach (var item in items)
                if (item.GetMetadata(CompatibleMetadataName) != "true")
                    item.SetMetadata(CompatibleMetadataName, IsCompatible(item, targetRid) ? "true" : "false");
        }

        /// <summary>
        /// Returns whether the given item is a potential match for the given RID.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="targetRid"></param>
        /// <returns></returns>
        bool IsCompatible(ITaskItem item, string targetRid)
        {
            return IsImport(item.GetMetadata(RuntimeIdentifierMetadataName), targetRid) || IsImport(targetRid, item.GetMetadata(RuntimeIdentifierMetadataName));
        }

    }

}

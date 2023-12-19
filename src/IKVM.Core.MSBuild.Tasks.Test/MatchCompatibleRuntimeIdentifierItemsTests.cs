using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IKVM.Core.MSBuild.Tasks.Test
{

    [TestClass]
    public class MatchCompatibleRuntimeIdentifierItemsTests
    {

        [TestMethod]
        public void ShouldMatchSingleItemWithExactRid()
        {
            var t = new MatchCompatibleRuntimeIdentifierItems();
            t.TargetRuntimeIdentifiers = "win-x86";
            t.Items = new[]
            {
                new TaskItem("itemA", new Dictionary<string,string>()
                {
                    ["RuntimeIdentifier"] = "win-x86",
                }),
                new TaskItem("itemB", new Dictionary<string,string>()
                {
                    ["RuntimeIdentifier"] = "win-x64",
                }),
            };
            t.Execute();

            t.Items.FirstOrDefault(i => i.ItemSpec == "itemA").GetMetadata("_Compatible").Should().Be("true");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemA").GetMetadata("_ManyCompatible").Should().Be("false");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemB").GetMetadata("_Compatible").Should().Be("false");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemB").GetMetadata("_ManyCompatible").Should().Be("");
        }

        [TestMethod]
        public void ShouldMatchManyItemsWithMultipleRids()
        {
            var t = new MatchCompatibleRuntimeIdentifierItems();
            t.TargetRuntimeIdentifiers = "win-x86;win-x64";
            t.Items = new[]
            {
                new TaskItem("itemA", new Dictionary<string,string>()
                {
                    ["RuntimeIdentifier"] = "win-x86",
                }),
                new TaskItem("itemB", new Dictionary<string,string>()
                {
                    ["RuntimeIdentifier"] = "win-x64",
                }),
                new TaskItem("itemC", new Dictionary<string,string>()
                {
                    ["RuntimeIdentifier"] = "linux-x86",
                }),
                new TaskItem("itemD", new Dictionary<string,string>()
                {
                    ["RuntimeIdentifier"] = "linux-x64",
                }),
            };
            t.Execute();

            t.Items.FirstOrDefault(i => i.ItemSpec == "itemA").GetMetadata("_Compatible").Should().Be("true");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemA").GetMetadata("_ManyCompatible").Should().Be("true");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemB").GetMetadata("_Compatible").Should().Be("true");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemB").GetMetadata("_ManyCompatible").Should().Be("true");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemC").GetMetadata("_Compatible").Should().Be("false");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemC").GetMetadata("_ManyCompatible").Should().Be("");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemD").GetMetadata("_Compatible").Should().Be("false");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemD").GetMetadata("_ManyCompatible").Should().Be("");
        }

        [TestMethod]
        public void ShouldMatchManyItemsWithCompatibleParent()
        {
            var t = new MatchCompatibleRuntimeIdentifierItems();
            t.TargetRuntimeIdentifiers = "win";
            t.Items = new[]
            {
                new TaskItem("itemA", new Dictionary<string,string>()
                {
                    ["RuntimeIdentifier"] = "win-x86",
                }),
                new TaskItem("itemB", new Dictionary<string,string>()
                {
                    ["RuntimeIdentifier"] = "win-x64",
                }),
                new TaskItem("itemC", new Dictionary<string,string>()
                {
                    ["RuntimeIdentifier"] = "linux-x86",
                }),
                new TaskItem("itemD", new Dictionary<string,string>()
                {
                    ["RuntimeIdentifier"] = "linux-x64",
                }),
            };
            t.Execute();

            t.Items.FirstOrDefault(i => i.ItemSpec == "itemA").GetMetadata("_Compatible").Should().Be("true");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemA").GetMetadata("_ManyCompatible").Should().Be("true");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemB").GetMetadata("_Compatible").Should().Be("true");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemB").GetMetadata("_ManyCompatible").Should().Be("true");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemC").GetMetadata("_Compatible").Should().Be("false");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemC").GetMetadata("_ManyCompatible").Should().Be("");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemD").GetMetadata("_Compatible").Should().Be("false");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemD").GetMetadata("_ManyCompatible").Should().Be("");
        }

        [TestMethod]
        public void ShouldMatchSingleItemWithCompatibleChild()
        {
            var t = new MatchCompatibleRuntimeIdentifierItems();
            t.TargetRuntimeIdentifiers = "win-x86";
            t.Items = new[]
            {
                new TaskItem("itemA", new Dictionary<string,string>()
                {
                    ["RuntimeIdentifier"] = "win",
                }),
                new TaskItem("itemB", new Dictionary<string,string>()
                {
                    ["RuntimeIdentifier"] = "linux",
                }),
            };
            t.Execute();

            t.Items.FirstOrDefault(i => i.ItemSpec == "itemA").GetMetadata("_Compatible").Should().Be("true");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemA").GetMetadata("_ManyCompatible").Should().Be("false");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemB").GetMetadata("_Compatible").Should().Be("false");
            t.Items.FirstOrDefault(i => i.ItemSpec == "itemB").GetMetadata("_ManyCompatible").Should().Be("");
        }

    }

}

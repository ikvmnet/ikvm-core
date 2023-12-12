using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Microsoft.Build.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IKVM.Core.MSBuild.Tasks.Test
{

    [TestClass]
    public class GenerateDepsFileExtensionsTests
    {

        [TestMethod]
        public void Foo()
        {
            var t = new GenerateDepsFileExtensions();
            t.DepsFilePath = Path.Combine(Path.GetDirectoryName(typeof(GenerateDepsFileExtensionsTests).Assembly.Location), "Sample.deps.json");
            t.AdditionalRuntimeAssets = new[]
            {
                new TaskItem("runtimes/magicrid/native/foo.bar", new Dictionary<string,string>()
                {
                    ["LibraryName"] = "TestPackage",
                    ["LibraryVersion"] = "1.2.3",
                    ["LibraryType"] = "project",
                    ["LibraryPath"] = "testpackage/1.2.3",
                    ["Runtime"] = "magicrid",
                })
            };
            t.Execute();

            var j = JsonDocument.Parse(File.OpenRead(t.DepsFilePath));
            var z = j.RootElement.ToString();
        }

    }

}

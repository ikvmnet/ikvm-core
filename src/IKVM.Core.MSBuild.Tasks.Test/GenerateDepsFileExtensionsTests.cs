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
        public void CanGenerateSampleDeps()
        {
            var s = Path.Combine(Path.GetDirectoryName(typeof(GenerateDepsFileExtensionsTests).Assembly.Location), "Sample.deps.json");
            var d = Path.GetTempFileName();
            File.Copy(s, d, true);

            var t = new GenerateDepsFileExtensions();
            t.DepsFilePath = d;
            t.AdditionalRuntimeNativeAssets = new[]
            {
                new TaskItem("runtimes/win-x64/native/foo.bar.dll", new Dictionary<string,string>()
                {
                    ["AssetPath"] = "runtimes/win-x64/native/foo.bar.dll",
                    ["LibraryName"] = "TestPackage",
                    ["LibraryVersion"] = "1.2.3",
                    ["LibraryType"] = "project",
                    ["Runtime"] = "",
                }),
            };
            t.AdditionalRuntimeLibraryAssets = new[]
            {
                new TaskItem("runtimes/win-x64/lib/net6.0/foo.bar.dll", new Dictionary<string,string>()
                {
                    ["AssetPath"] = "runtimes/win-x64/lib/net6.0/foo.bar.dll",
                    ["LibraryName"] = "TestPackage",
                    ["LibraryVersion"] = "1.2.3",
                    ["LibraryType"] = "project",
                    ["Runtime"] = "",
                })
            };
            t.Execute();

            var j = JsonDocument.Parse(File.OpenRead(d));
            var z = j.RootElement.ToString();
        }

    }

}

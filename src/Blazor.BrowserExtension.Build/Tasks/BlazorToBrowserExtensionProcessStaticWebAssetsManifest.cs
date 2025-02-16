﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blazor.BrowserExtension.Build.Tasks.StaticWebAssets;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Blazor.BrowserExtension.Build.Tasks
{
    public class BlazorToBrowserExtensionProcessStaticWebAssetsManifest : Task
    {
        [Required]
        public ITaskItem Input { get; set; }
        public ITaskItem[] Exclude { get; set; }
        [Required]
        public string OutputPath { get; set; }

        [Output]
        public ITaskItem[] Output { get; set; }

        public override bool Execute()
        {
            var excludePaths = Exclude?.Select(exclude => exclude.ItemSpec);
            Output = Process(Input.ItemSpec, excludePaths, OutputPath);
            return true;
        }

        private static ITaskItem[] Process(string filePath, IEnumerable<string> excludePaths, string outputPath)
        {
            BaseManifestProcessor processor;
            if (".xml".Equals(Path.GetExtension(filePath), StringComparison.OrdinalIgnoreCase))
            {
                processor = new XmlManifestProcessor(excludePaths);
            }
            else
            {
                processor = new JsonManifestProcessor(excludePaths);
            }
            processor.ReadFromFile(filePath);
            processor.Process(outputPath);
            processor.WriteToFile(filePath);
            var output = processor.GetOutput()
                .Select(staticWebAssetFile =>
                    new TaskItem(staticWebAssetFile.FilePath, new Dictionary<string, string>()
                    {
                        { "ContentRelativeDirectory", staticWebAssetFile.RelativePath }
                    })
                ).ToArray();
            return output;
        }
    }
}

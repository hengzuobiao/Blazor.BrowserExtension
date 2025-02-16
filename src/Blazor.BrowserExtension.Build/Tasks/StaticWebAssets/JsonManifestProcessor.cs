﻿#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Blazor.BrowserExtension.Build.Tasks.StaticWebAssets
{
    public class JsonManifestProcessor : BaseManifestProcessor
    {
        private List<string> contentRoots = new();
        private StaticWebAssetManifest? staticWebAssetManifest;
        private bool isUpdated;

        public JsonManifestProcessor(IEnumerable<string> excludePaths) : base(excludePaths)
        {
        }

        public override void ReadFromFile(string filePath)
        {
            var json = File.ReadAllText(filePath);
            staticWebAssetManifest = StaticWebAssetManifest.Parse(json);
            if (staticWebAssetManifest?.ContentRoots is null || !staticWebAssetManifest.ContentRoots.Any())
            {
                return;
            }

            contentRoots = staticWebAssetManifest.ContentRoots.ToList();
            VisitNode(staticWebAssetManifest.Root, "/");
        }

        public override void Process(string outputPath)
        {
            if (staticWebAssetManifest?.ContentRoots is null || !staticWebAssetManifest.ContentRoots.Any() || staticWebAssetManifest.Root?.Children is null)
            {
                return;
            }

            if (staticWebAssetManifest.Root.Children.ContainsKey("_framework"))
            {
                MoveNode(staticWebAssetManifest.Root.Children, "_framework", "framework");
                isUpdated = true;
            }

            if (staticWebAssetManifest.Root.Children.ContainsKey("_content"))
            {
                MoveNode(staticWebAssetManifest.Root.Children, "_content", "content");
                isUpdated = true;
            }

            outputPath = Path.GetFullPath(outputPath);
            if (!contentRoots.Contains(outputPath))
            {
                contentRoots.Add(outputPath);
                var outputPathIndex = contentRoots.Count - 1;
                staticWebAssetManifest.ContentRoots = contentRoots.ToArray();
                var frameworkChildren = staticWebAssetManifest.Root.Children["framework"].Children;
                if (frameworkChildren is not null)
                {
                    var replacedFiles = frameworkChildren.Keys
                        .Where(name =>
                            name.Equals("blazor.webassembly.js", StringComparison.OrdinalIgnoreCase) ||
                            (name.StartsWith("dotnet.", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                        ) ?? Enumerable.Empty<string>();
                    foreach (var replacedFile in replacedFiles)
                    {
                        frameworkChildren[replacedFile].Match!.Path = frameworkChildren[replacedFile].Match!.Path.Replace("_", "");
                        frameworkChildren[replacedFile].Match!.ContentRoot = outputPathIndex;
                    }
                }

                isUpdated = true;
            }
        }

        private static void MoveNode(Dictionary<string, StaticWebAssetNode> nodes, string fromKey, string toKey)
        {
            var nodeToMove = nodes[fromKey];
            if (nodes.TryGetValue(toKey, out var existingNode) && existingNode.Children is not null)
            {
                if (nodeToMove.Children is null)
                {
                    nodeToMove.Children = existingNode.Children;
                }
                else
                {
                    foreach (var existingNodePair in existingNode.Children)
                    {
                        nodeToMove.Children.Add(existingNodePair.Key, existingNodePair.Value);
                    }
                }
            }
            nodes[toKey] = nodeToMove;
            nodes.Remove(fromKey);
        }

        public override void WriteToFile(string filePath)
        {
            if (!isUpdated)
            {
                return;
            }

            var json = JsonSerializer.Serialize(staticWebAssetManifest);
            File.WriteAllText(filePath, json);
        }

        private void VisitNode(StaticWebAssetNode content, string contentRelativePath)
        {
            if (content is null)
            {
                return;
            }

            if (content.HasChildren())
            {
                foreach (var child in content.Children!)
                {
                    VisitNode(child.Value, $"{contentRelativePath.TrimEnd('/')}/{child.Key}");
                }
            }

            if (content.HasPatterns())
            {
                ProcessPatterns(content.Patterns!, contentRelativePath);
            }

            if (content.Match is not null)
            {
                ProcessMatch(content.Match!, contentRelativePath);
            }
        }

        private void ProcessPatterns(IEnumerable<StaticWebAssetPattern> patterns, string contentRelativePath)
        {
            foreach (var pattern in patterns)
            {
                if (pattern.Pattern == "**")
                {
                    var contentRootPath = NormalizePath(contentRoots[pattern.ContentRoot]);
                    if (ShouldExcludePath(contentRootPath))
                    {
                        continue;
                    }

                    var contentFiles = Directory.GetFiles(contentRootPath, "*", SearchOption.AllDirectories);
                    foreach (var contentFile in contentFiles)
                    {
                        var contentRelativeDirectory = contentRelativePath + Path.GetDirectoryName(contentFile)?.Replace(contentRootPath, string.Empty);
                        AddOutput(contentFile, contentRelativeDirectory);
                    }
                }
                else
                {
                    throw new NotSupportedException($"Static web assets manifest pattern '{pattern.Pattern}' is not supported.");
                }
            }
        }

        private void ProcessMatch(StaticWebAssetMatch match, string contentRelativePath)
        {
            var contentRootPath = NormalizePath(contentRoots[match.ContentRoot]);
            if (ShouldExcludePath(contentRootPath))
            {
                return;
            }

            if (match.Path.Replace('\\', '/').StartsWith(@"_framework/dotnet.", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var contentFile = Path.Combine(contentRootPath, match.Path);
            var contentRelativeDirectory = Path.GetDirectoryName(contentRelativePath);
            AddOutput(contentFile, contentRelativeDirectory);
        }
    }
}

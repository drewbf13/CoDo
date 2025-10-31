using System.Text.RegularExpressions;

namespace CodeDocs;

public static class SectionExtractor
{
    public static Task<List<SectionHit>> ExtractAsync(Config cfg, List<string> files)
    {
        var hits = new List<SectionHit>();

        foreach (var file in files)
        {
            var code = File.ReadAllText(file);
            var lang = InferLang(file);

            // Find source: for local sources, match by root path; for repo sources, match by include pattern
            Source? source = null;
            foreach (var s in cfg.Sources)
            {
                if (string.IsNullOrWhiteSpace(s.Repo))
                {
                    // Local source: match by root path
                    if (Path.GetFullPath(file).StartsWith(Path.GetFullPath(s.Root), StringComparison.OrdinalIgnoreCase))
                    {
                        source = s;
                        break;
                    }
                }
                else
                {
                    // Repo source: match by checking if file path matches any include pattern
                    if (s.Include != null && s.Include.Count > 0)
                    {
                        var matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher(StringComparison.OrdinalIgnoreCase);
                        foreach (var pattern in s.Include)
                            matcher.AddInclude(pattern);
                        if (s.Exclude != null)
                            foreach (var exclude in s.Exclude)
                                matcher.AddExclude(exclude);
                        
                        // Extract the part of the path that might match (look for repo structure)
                        var normalizedFile = file.Replace('\\', '/');
                        // Try to find a portion of the path that matches the include pattern
                        // For repo files in temp, find where the repo content starts
                        foreach (var pattern in s.Include)
                        {
                            // Simple check: if the filename or a segment matches the pattern
                            var fileName = Path.GetFileName(file);
                            if (pattern.Contains(fileName) || normalizedFile.Contains(Path.GetFileNameWithoutExtension(pattern)))
                            {
                                source = s;
                                break;
                            }
                        }
                        if (source != null) break;
                    }
                    else
                    {
                        // No include pattern, assume all files from repo sources match
                        source = s;
                        break;
                    }
                }
            }
            source ??= cfg.Sources.FirstOrDefault();

            // For repo sources, extract repo-relative path from the file path
            // File paths from repo sources are in temp like: .../repos/HASH/Claims.Application/...
            // We need to extract just the Claims.Application/... part
            var rel = MakeRelativePath(file);
            if (source != null && !string.IsNullOrWhiteSpace(source.Repo) && source.Include != null)
            {
                // Try to find the repo-relative path by matching against include patterns
                var normalizedFile = file.Replace('\\', '/');
                foreach (var pattern in source.Include)
                {
                    // Find where the pattern appears in the path
                    var patternParts = pattern.Split('/');
                    var lastPart = patternParts[^1];
                    var lastIndex = normalizedFile.LastIndexOf(lastPart, StringComparison.OrdinalIgnoreCase);
                    if (lastIndex >= 0)
                    {
                        // Extract from the start of the matching segment
                        var segments = normalizedFile.Split('/');
                        for (int i = 0; i < segments.Length; i++)
                        {
                            if (segments[i].Equals(patternParts[0], StringComparison.OrdinalIgnoreCase))
                            {
                                rel = string.Join("/", segments.Skip(i));
                                break;
                            }
                        }
                        break;
                    }
                }
            }
            if (source?.Sections is not { Count: > 0 })
            {
                hits.Add(new SectionHit(source?.Name ?? "default", rel, lang, "Full File", code));
                continue;
            }

            foreach (var rule in source.Sections)
            {
                switch (rule.Type)
                {
                    case "region":
                        {
                            var anchor = string.IsNullOrWhiteSpace(rule.Match) ? "^#region " : rule.Match!;
                            var rx = new Regex(@$"(?ms)^(?:{anchor}).*?\n(.*?)^\#endregion\s*$", RegexOptions.Multiline);
                            foreach (Match m in rx.Matches(code))
                            {
                                var body = m.Groups[1].Value.Trim();
                                if (!string.IsNullOrWhiteSpace(body))
                                    hits.Add(new SectionHit(source.Name, rel, lang, rule.Name ?? "Region", body, rule.Context));
                            }
                            break;
                        }
                    case "regex":
                        {
                            if (string.IsNullOrWhiteSpace(rule.Pattern)) break;
                            var rx = new Regex(rule.Pattern, RegexOptions.Singleline);
                            foreach (Match m in rx.Matches(code))
                            {
                                var body = m.Value.Trim();
                                if (!string.IsNullOrWhiteSpace(body))
                                    hits.Add(new SectionHit(source.Name, rel, lang, rule.Name ?? "Regex Section", body, rule.Context));
                            }
                            break;
                        }
                    case "ast":
                        {
                            // TODO: Use Roslyn to locate symbol rule.Symbol and extract text
                            break;
                        }
                }
            }
        }

        return Task.FromResult(hits);
    }

    private static string InferLang(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".cs" => "csharp",
        ".ts" => "typescript",
        ".js" => "javascript",
        ".sql" => "sql",
        ".vue" => "vue",
        _ => "text"
    };

    private static string MakeRelativePath(string fullPath)
        => Path.GetRelativePath(Directory.GetCurrentDirectory(), fullPath).Replace('\\', '/');
}

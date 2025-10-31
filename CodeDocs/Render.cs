namespace CodeDocs;

public sealed class Render
{
    private readonly Config _cfg;
    public Render(Config cfg) => _cfg = cfg;

    public GeneratedDoc ToMarkdown(SectionHit hit, string llmMd)
    {
        var safeRel = hit.FilePath.TrimStart('.', '/');
        var displayTitle = GetDisplayTitle(hit, safeRel);
        var name = $"{safeRel}__{Slug(hit.SectionName)}.md";

        var md = (llmMd ?? string.Empty).Trim();
        if (md.StartsWith("# "))
        {
            // Replace the first line (title) with our display title
            var lines = md.Split('\n');
            if (lines.Length > 0)
            {
                lines[0] = $"# {displayTitle}";
                md = string.Join('\n', lines);
            }
        }
        else
        {
            md = $"# {displayTitle}\n\n## {hit.SectionName}\n\n{md}";
        }

        return new GeneratedDoc(RelPath: name, Markdown: md);
    }

    private static string Slug(string s)
    {
        var cleaned = new string(s.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());
        while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");
        return cleaned.Trim('-');
    }

    private string GetDisplayTitle(SectionHit hit, string fallback)
    {
        var source = _cfg.Sources.FirstOrDefault(s => s.Name.Equals(hit.SourceName, StringComparison.OrdinalIgnoreCase));
        if (source?.Aliases is { Count: > 0 })
        {
            var normPath = hit.FilePath.Replace('\\', '/').TrimStart('.', '/');
            
            foreach (var kv in source.Aliases)
            {
                var key = (kv.Key ?? string.Empty).TrimStart('.', '/').Replace('\\', '/');
                if (key.Length == 0) continue;
                
                // Try exact match first
                if (normPath.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
                
                // Try ends-with match
                if (normPath.EndsWith(key, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
                
                // For repo sources, the file path might include temp directory path
                // Extract just the repo-relative portion by finding where the key pattern appears
                var keyIndex = normPath.IndexOf(key, StringComparison.OrdinalIgnoreCase);
                if (keyIndex >= 0)
                {
                    // Verify it's a proper match (at word boundary or after path separator)
                    var beforeChar = keyIndex > 0 ? normPath[keyIndex - 1] : '/';
                    if (beforeChar == '/' || beforeChar == '\\' || keyIndex == 0)
                        return kv.Value;
                }
            }
        }
        return fallback;
    }
}

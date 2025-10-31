using CodeDocs;

var cfgPath = args.FirstOrDefault(a => a.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
           ?? "codedocs.yaml";

Config cfg;
try
{
    cfg = File.Exists(cfgPath) ? Config.Load(cfgPath) : Config.Load("codedocs.sample.yaml");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load config: {ex.Message}");
    return 1;
}

var git = new GitService();

Console.WriteLine("Discovering files...");
var files = await FileDiscovery.GetFilesAsync(cfg, git);
Console.WriteLine($"Found {files.Count} candidate file(s).");

Console.WriteLine("Extracting sections...");
var hits = await SectionExtractor.ExtractAsync(cfg, files);
Console.WriteLine($"Extracted {hits.Count} section(s).");

var prompter = new Prompter(cfg);
var render = new Render(cfg);
var docs = new List<GeneratedDoc>();

await Parallel.ForEachAsync(hits, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, cfg.Run.Parallelism) }, async (hit, _) =>
{
    try
    {
        var md = await prompter.GenerateAsync(hit);
        var outDoc = render.ToMarkdown(hit, md);
        lock (docs) docs.Add(outDoc);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Generation failed for {hit.FilePath}::{hit.SectionName}: {ex.Message}");
        if (cfg.Run.FailOnUnmatchedSections) throw;
    }
});

Console.WriteLine($"Writing {docs.Count} doc(s) to {cfg.Output.Path} ...");
Directory.CreateDirectory(cfg.Output.Path);
foreach (var d in docs.OrderBy(d => d.RelPath, StringComparer.OrdinalIgnoreCase))
{
    var dest = Path.Combine(cfg.Output.Path, d.RelPath);
    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
    await File.WriteAllTextAsync(dest, d.Markdown);
}

Console.WriteLine("Done.");
return 0;

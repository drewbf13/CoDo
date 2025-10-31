using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace CodeDocs;

public static class FileDiscovery
{
    public static Task<List<string>> GetFilesAsync(Config cfg, GitService git)
    {
        var all = new List<string>();

        foreach (var s in cfg.Sources)
        {
            string effectiveRoot;
            string repoRootForDiff;

            if (!string.IsNullOrWhiteSpace(s.Repo))
            {
                // Materialize from git repo
                effectiveRoot = git.MaterializeSource(s.Repo!, s.Ref, s.SubDir);
                repoRootForDiff = effectiveRoot; // run incremental diffs within this repo
            }
            else
            {
                effectiveRoot = Path.GetFullPath(s.Root);
                repoRootForDiff = Directory.GetCurrentDirectory();
            }

            if (!Directory.Exists(effectiveRoot)) continue;

            var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            if (s.Include is { Count: > 0 }) foreach (var i in s.Include) matcher.AddInclude(i);
            else matcher.AddInclude("**/*.*");

            if (s.Exclude is { Count: > 0 }) foreach (var e in s.Exclude) matcher.AddExclude(e);

            var res = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(effectiveRoot)));
            var files = res.Files
                .Select(f => Path.GetFullPath(Path.Combine(effectiveRoot, f.Path)))
                .ToList();

            if (cfg.Run.Incremental)
                files = git.FilterChanged(files, repoRoot: repoRootForDiff);

            all.AddRange(files);
        }

        return Task.FromResult(all.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }
}

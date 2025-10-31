using System.Diagnostics;

namespace CodeDocs;

public sealed class GitService
{
    public string MaterializeSource(string repoUrl, string? @ref, string? subDir)
    {
        // Use a stable cache folder per repo+ref to avoid recloning every run
        var cacheRoot = Path.Combine(Path.GetTempPath(), "codedocs", "repos");
        Directory.CreateDirectory(cacheRoot);

        var key = $"{repoUrl}|{@ref ?? "_default"}";
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))).Substring(0, 16);
        var targetDir = Path.Combine(cacheRoot, hash);

        if (!Directory.Exists(targetDir) || !Directory.Exists(Path.Combine(targetDir, ".git")))
        {
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, recursive: true);
            Directory.CreateDirectory(targetDir);

            if (!string.IsNullOrWhiteSpace(@ref))
            {
                // Shallow clone specific ref/branch
                Exec("git", $"clone --depth 1 --branch {@ref} --no-tags {EscapeArg(repoUrl)} {EscapeArg(targetDir)}");
            }
            else
            {
                Exec("git", $"clone --depth 1 --no-tags {EscapeArg(repoUrl)} {EscapeArg(targetDir)}");
            }
        }
        else
        {
            try
            {
                Exec("git", "fetch --depth 1 --prune --no-tags", targetDir);
                if (!string.IsNullOrWhiteSpace(@ref))
                {
                    Exec("git", $"checkout {@ref}", targetDir);
                    Exec("git", $"reset --hard origin/{@ref}", targetDir);
                }
                else
                {
                    // Update current checked-out branch
                    Exec("git", "pull --ff-only", targetDir);
                }
            }
            catch
            {
                // If anything goes wrong, reclone cleanly
                try { Directory.Delete(targetDir, recursive: true); } catch { /* ignore */ }
                Directory.CreateDirectory(targetDir);
                if (!string.IsNullOrWhiteSpace(@ref))
                    Exec("git", $"clone --depth 1 --branch {@ref} --no-tags {EscapeArg(repoUrl)} {EscapeArg(targetDir)}");
                else
                    Exec("git", $"clone --depth 1 --no-tags {EscapeArg(repoUrl)} {EscapeArg(targetDir)}");
            }
        }

        var root = string.IsNullOrWhiteSpace(subDir) ? targetDir : Path.GetFullPath(Path.Combine(targetDir, subDir!));
        return root;
    }

    public List<string> FilterChanged(List<string> files, string repoRoot)
    {
        try
        {
            var changed = Exec("git", "diff --name-only HEAD~1..HEAD", repoRoot)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => Path.GetFullPath(Path.Combine(repoRoot, p)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var filtered = files.Where(f => changed.Contains(Path.GetFullPath(f))).ToList();
            return filtered.Count == 0 ? files : filtered;
        }
        catch
        {
            return files; // if git not available
        }
    }

    private static string Exec(string file, string args, string? workDir = null)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workDir ?? Directory.GetCurrentDirectory()
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        return p.StandardOutput.ReadToEnd();
    }

    private static string EscapeArg(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        // Basic quoting for spaces/characters
        if (arg.Contains(' ') || arg.Contains('"'))
        {
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }
        return arg;
    }
}

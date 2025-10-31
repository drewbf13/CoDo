using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CodeDocs;

public sealed class Config
{
    public required string Version { get; init; }
    public required List<Source> Sources { get; init; }
    public required Style Style { get; init; }
    public required Llm Llm { get; init; }
    public required Run Run { get; init; }
    public required Output Output { get; init; }

    public static Config Load(string path)
    {
        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<Config>(yaml);
    }
}

public sealed class Source
{
    public required string Name { get; init; }
    public required string Root { get; init; }
    // Optional git-backed source. When set, files will be discovered from this repo instead of local Root.
    public string? Repo { get; init; }          // e.g., https://github.com/org/repo.git or git@github.com:org/repo.git
    public string? Ref { get; init; }           // branch, tag, or commit (optional; defaults to default branch)
    public string? SubDir { get; init; }        // optional subdirectory inside the repo to treat as root
    public List<string>? Include { get; init; }
    public List<string>? Exclude { get; init; }
    // Optional mapping of relative file paths (within source/repo) to display titles
    public Dictionary<string, string>? Aliases { get; init; }
    public List<SectionRule>? Sections { get; init; }
}

public sealed class SectionRule
{
    public required string Type { get; init; } // region | regex | ast
    public string? Name { get; init; }
    public string? Match { get; init; }        // for region
    public string? Pattern { get; init; }      // for regex
    public string? Symbol { get; init; }       // for ast (TODO)
    public string? Context { get; init; }      // optional extra prompt context for this section
}

public sealed class Style
{
    public required string Preset { get; init; }
    public required string Tone { get; init; }
    public required string Audience { get; init; }
    public List<string>? Include { get; init; }
}

public sealed class Llm
{
    public required string Provider { get; init; } // "openai" only for now
    public required string Model { get; init; }
    public int MaxTokens { get; init; } = 4000;
    public double Temperature { get; init; } = 0.2;
    public string? SystemPrompt { get; init; }
    public string? PromptTemplate { get; init; }
}

public sealed class Run
{
    public bool Incremental { get; init; } = true;
    public int Parallelism { get; init; } = 4;
    public bool FailOnUnmatchedSections { get; init; } = false;
}

public sealed class Output
{
    public required string Path { get; init; } // e.g., "out/docs"
}

public sealed record SectionHit(string SourceName, string FilePath, string Language, string SectionName, string Code, string? Context = null);
public sealed record GeneratedDoc(string RelPath, string Markdown);

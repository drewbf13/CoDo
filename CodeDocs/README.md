# CodeDocs (local docs generator)

CLI that reads `codedocs.yaml`, extracts code sections, calls an LLM, and writes Markdown to `out/docs`.

## Prereqs
- .NET 9 SDK
- Git (for incremental mode)
- OpenAI API key: set `OPENAI_API_KEY`

## Build & Run
```bash
dotnet build
OPENAI_API_KEY=sk-... dotnet run --project CodeDocs -- codedocs.yaml
```

If `codedocs.yaml` is missing, the tool uses `codedocs.sample.yaml`.

## Config notes
- `run.incremental: true` filters to changed files per `git diff HEAD~1..HEAD`.
- `output.path` controls where docs are written (default sample uses `out/docs`).

## Future work
- Roslyn AST extraction for `SectionRule.Type == "ast"`
- Azure DevOps publisher (wiki/repo)
- CI pipeline

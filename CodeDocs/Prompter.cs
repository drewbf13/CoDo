using System.Text;
using System.Text.Json;

namespace CodeDocs;

public sealed class Prompter
{
    private readonly Config _cfg;
    private readonly HttpClient _http = new();

    public Prompter(Config cfg) { _cfg = cfg; }

    public async Task<string> GenerateAsync(SectionHit hit)
    {
        if (_cfg.Llm.Provider is not "openai")
            throw new NotSupportedException($"LLM provider '{_cfg.Llm.Provider}' not supported in this step.");

        var includes = string.Join(",", _cfg.Style.Include ?? new());
        var tpl = _cfg.Llm.PromptTemplate ?? "Document:\nFILE={{file_path}}\nSECTION={{section_name}}\nCODE:\n```{{language}}\n{{code}}\n```";

        var prompt = tpl
            .Replace("{{file_path}}", hit.FilePath)
            .Replace("{{section_name}}", hit.SectionName)
            .Replace("{{language}}", hit.Language)
            .Replace("{{code}}", hit.Code)
            .Replace("{{context}}", hit.Context ?? string.Empty)
            .Replace("{{preset}}", _cfg.Style.Preset)
            .Replace("{{tone}}", _cfg.Style.Tone)
            .Replace("{{audience}}", _cfg.Style.Audience)
            .Replace("{{includes}}", includes);

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                     ?? throw new InvalidOperationException("OPENAI_API_KEY not set");

        _http.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);

        var body = new
        {
            model = _cfg.Llm.Model,
            messages = new object[]
            {
                new { role = "system", content = _cfg.Llm.SystemPrompt ?? string.Empty },
                new { role = "user", content = prompt }
            },
            temperature = _cfg.Llm.Temperature,
            max_tokens = _cfg.Llm.MaxTokens
        };

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var resp = await _http.PostAsync("https://api.openai.com/v1/chat/completions",
                    new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
                resp.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                return json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            }
            catch when (attempt < 3)
            {
                await Task.Delay(500 * attempt);
            }
        }

        throw new Exception("LLM request failed after retries.");
    }
}

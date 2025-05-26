using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AISubtitleTranslator.Models;

namespace AISubtitleTranslator.Services;

public class MistralTranslator : IMistralTranslator
{
    private readonly HttpClient _client;
    private readonly ISrtParser _parser;

    public MistralTranslator(HttpClient client, ISrtParser parser)
    {
        _client = client;
        _parser = parser;
    }

    public async Task<TranslationResponse> TranslateBatch(
        List<SrtBlock> batch,
        List<SrtBlock> contextBefore,
        List<SrtBlock> contextAfter,
        Dictionary<int, string> existingTranslations,
        TranslationConfig config,
        Dictionary<string, string> termTranslations)
    {
        if (batch == null || !batch.Any())
            throw new ArgumentException("Batch cannot be null or empty", nameof(batch));
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrEmpty(config.Language))
            throw new ArgumentException("Language cannot be empty", nameof(config.Language));

        var userPrompt = BuildTranslationPrompt(
            batch, contextBefore, contextAfter, existingTranslations, config.Language, termTranslations);

        var request = new
        {
            model = "mistral-large-2411",
            messages = new[]
            {
                new { role = "system", content = config.SystemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = config.Temperature,
            top_p = config.TopP,
            random_seed = config.LlmSeed
        };

        var response = await _client.PostAsJsonAsync("chat/completions", request);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        // Десериализуем ответ API
        var result = JsonSerializer.Deserialize<MistralResponse>(responseString);
        if (result == null || result.Choices == null || !result.Choices.Any())
        {
            throw new InvalidOperationException("Failed to receive a valid response from the API.");
        }

        var translatedJson = result.Choices[0].Message.Content;

        // Извлекаем чистый JSON из Markdown-блока
        var jsonContent = ExtractJsonFromMarkdown(translatedJson);

        // Десериализуем извлеченный JSON в TranslationResponse
        var translationResponse = JsonSerializer.Deserialize<TranslationResponse>(jsonContent);
        if (translationResponse == null)
        {
            throw new InvalidOperationException("Failed to deserialize translation response.");
        }

        // Обновляем термины
        if (translationResponse.Terms.Count != 0)
        {
            foreach (var term in translationResponse.Terms)
            {
                if (!termTranslations.ContainsKey(term.Key))
                    termTranslations[term.Key] = term.Value;
            }
        }

        return translationResponse;
    }

    private string BuildTranslationPrompt(
        List<SrtBlock> batch,
        List<SrtBlock> contextBefore,
        List<SrtBlock> contextAfter,
        Dictionary<int, string> translations,
        string language,
        Dictionary<string, string> termTranslations)
    {
        var knownTerms = termTranslations.Any()
            ? string.Join("\n", termTranslations.Select(t => $"- {t.Key} -> {t.Value}"))
            : "No known terms yet.";

        return $$"""
                KNOWN TERMS:
                {{knownTerms}}

                CONTEXT BEFORE:
                {{string.Join("\n\n", contextBefore.Select(b => $"BLOCK {b.Number}:\n{translations.GetValueOrDefault(b.Number, b.Text)}"))}}

                TRANSLATE ONLY THESE BLOCKS {{batch.First().Number}}-{{batch.Last().Number}} TO THE {{language.ToUpper()}} LANGUAGE:
                {{string.Join("\n\n", batch.Select(b => $"BLOCK {b.Number}:\n{b.Text}"))}}

                CONTEXT AFTER:
                {{string.Join("\n\n", contextAfter.Select(b => $"BLOCK {b.Number}:\n{b.Text}"))}}

                INSTRUCTIONS:
                - Translate only specified blocks exactly to the {{language}} literary style.
                - Use the known terms for consistency.
                - If new specific terms are found, include them in the translation output as part of the JSON response.
                - Maintain consistent terminology across the entire translation.
                - Preserve all formatting, symbols, and technical markers (e.g. [MUSIC], ♪).
                - Output the response in JSON format wrapped in a markdown code block with the following structure:
                ```json
                {
                  "translations": [
                    {
                      "number": int,
                      "time": "string",
                      "text": "string"
                    }
                  ],
                  "terms": {
                    "originalTerm": "translatedTerm"
                  }
                }
                ```
                """;
    }

    private string ExtractJsonFromMarkdown(string markdownContent)
    {
        var match = Regex.Match(markdownContent, @"```json\s*([\s\S]*?)\s*```");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        throw new InvalidOperationException("Failed to extract JSON from API response.");
    }
}

// Вспомогательные классы для десериализации ответа от API
public class MistralResponse
{
    [JsonPropertyName("choices")]
    public List<Choice> Choices { get; set; }
}

public class Choice
{
    [JsonPropertyName("message")]
    public Message Message { get; set; }
}

public class Message
{
    [JsonPropertyName("content")]
    public string Content { get; set; }
}
using AISubtitleTranslator.Models;

namespace AISubtitleTranslator.Services;

/// <summary>
/// Сервис перевода с использованием API Mistral
/// </summary>
public class MistralTranslator : IMistralTranslator
{
    private readonly HttpClient _client;
    private readonly ISrtParser _parser;

    public MistralTranslator(HttpClient client, ISrtParser parser)
    {
        _client = client;
        _parser = parser;
    }

    /// <summary>
    /// Перевод группы блоков
    /// </summary>
    public async Task<Dictionary<int, string>> TranslateBatch(
        List<SrtBlock> batch,
        List<SrtBlock> contextBefore,
        List<SrtBlock> contextAfter,
        Dictionary<int, string> existingTranslations,
        TranslationConfig config)
    {
        var userPrompt = BuildTranslationPrompt(
            batch, contextBefore, contextAfter, existingTranslations, config.Language);

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
        var result = await response.Content.ReadFromJsonAsync<MistralResponse>();
        var translatedText = result?.Choices.FirstOrDefault()?.Message.Content ?? "";

        await Task.Delay(1000);

        var translationsDict = _parser.ParseMistralResponse(translatedText, batch);
        var cleanTranslations = new Dictionary<int, string>();

        foreach (var (blockNumber, translation) in translationsDict)
            cleanTranslations[blockNumber] = _parser.CleanTranslation(translation);

        return cleanTranslations;
    }

    /// <summary>
    /// Построение промпта для перевода
    /// </summary>
    private string BuildTranslationPrompt(
        List<SrtBlock> batch,
        List<SrtBlock> contextBefore,
        List<SrtBlock> contextAfter,
        Dictionary<int, string> translations,
        string language)
    {
        return $"""
                CONTEXT BEFORE:
                {string.Join("\n\n", contextBefore.Select(b => $"BLOCK {b.Number}:\n{translations.GetValueOrDefault(b.Number, b.Text)}"))}

                TRANSLATE ONLY THESE BLOCKS {batch.First().Number}-{batch.Last().Number} TO THE {language.ToUpper()} LANGUAGE:
                {string.Join("\n\n", batch.Select(b => $"BLOCK {b.Number}:\n{b.Text}"))}

                CONTEXT AFTER:
                {string.Join("\n\n", contextAfter.Select(b => $"BLOCK {b.Number}:\n{b.Text}"))}

                INSTRUCTIONS:
                - Translate only specified blocks exactly to the {language} literary style.
                - Maintain consistent terminology across the entire translation.
                - **IMPORTANT:** When translating the current blocks, take into account the subsequent context (CONTEXT AFTER) to ensure a smooth and natural flow in the final text.
                - Preserve all formatting, symbols, and technical markers (e.g. [MUSIC], ♪).
                - Output ONLY the translated blocks in the following format:
                {string.Join("\n", batch.Select(b => $"BLOCK {b.Number}\n<translation>"))}

                Note that these are the subtitles. 
                Each translated block should not be very long and should be really close to the original block by meaning.
                """;
    }
}
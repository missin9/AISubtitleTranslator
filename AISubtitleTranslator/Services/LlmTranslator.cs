using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using AISubtitleTranslator.Models;

namespace AISubtitleTranslator.Services;

public class LlmTranslator : ILlmTranslator
{
    private readonly OpenAIClient _client;
    private readonly string _defaultModel;

    public LlmTranslator(OpenAIClient client, string defaultModel)
    {
        _client = client;
        _defaultModel = defaultModel;
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

        var messages = new List<Message>
        {
            new(Role.System, config.SystemPrompt),
            new(Role.User, userPrompt)
        };

        var chatRequest = new ChatRequest(
            messages: messages,
            temperature: config.Temperature,
            topP: config.TopP,
            model: _defaultModel,
            seed: config.LlmSeed
        );

        try
        {
            var response = await _client.ChatEndpoint.GetCompletionAsync(chatRequest);
            
            if (response == null || response.Choices == null || !response.Choices.Any())
            {
                throw new InvalidOperationException("Failed to receive a valid response from the API.");
            }

            var translatedJson = response.Choices[0].Message.Content?.ToString();
            if (string.IsNullOrEmpty(translatedJson))
            {
                throw new InvalidOperationException("Received empty response from the API.");
            }

            // Десериализуем извлеченный JSON в TranslationResponse
            translatedJson = CleanJsonResponse(translatedJson);
            var translationResponse = JsonSerializer.Deserialize<TranslationResponse>(translatedJson);
            if (translationResponse == null)
            {
                throw new InvalidOperationException("Failed to deserialize translation response.");
            }

            foreach (var translation in translationResponse.Translations)
            {
                translation.Time = batch.FirstOrDefault(b => b.Number == translation.Number)?.Time ?? translation.Time;
            }

            // Обновляем термины
            if (translationResponse.Terms?.Count > 0)
            {
                foreach (var term in translationResponse.Terms)
                {
                    if (!termTranslations.ContainsKey(term.Key))
                        termTranslations[term.Key] = term.Value;
                }
            }

            return translationResponse;
        }
        catch (Exception ex) when (!(ex is ArgumentException || ex is ArgumentNullException))
        {
            throw new InvalidOperationException($"Translation API call failed: {ex.Message}", ex);
        }
    }
    
    private static string CleanJsonResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;
        
        response = response.Trim();
        
        if (response.StartsWith("```json"))
        {
            response = response.Substring(7).Trim();
        }
        else if (response.StartsWith("```"))
        {
            response = response.Substring(3).Trim();
        }
        
        if (response.EndsWith("```"))
        {
            response = response.Substring(0, response.Length - 3).Trim();
        }
        
        var startIndex = response.IndexOf('{');
        var lastIndex = response.LastIndexOf('}');
    
        if (startIndex >= 0 && lastIndex > startIndex)
        {
            response = response.Substring(startIndex, lastIndex - startIndex + 1);
        }
    
        return response;
    }

    /// <summary>
    /// Пользовательский промпт для перевода
    /// </summary>
    public static string BuildTranslationPrompt(
        List<SrtBlock> batch,
        List<SrtBlock> contextBefore,
        List<SrtBlock> contextAfter,
        Dictionary<int, string> translations,
        string language,
        Dictionary<string, string> termTranslations)
    {
        var knownTermsSection = BuildKnownTermsSection(termTranslations);
        var contextBeforeSection = BuildContextBeforeSection(contextBefore, translations);
        var contextAfterSection = BuildContextAfterSection(contextAfter, batch);
        var blocksToTranslateSection = BuildBlocksToTranslateSection(batch, language);

        return $"{knownTermsSection}\n\n{contextBeforeSection}{blocksToTranslateSection}\n\n{contextAfterSection}".Trim();
    }
    
    /// <summary>
    /// Секция с известными терминами
    /// </summary>
    private static string BuildKnownTermsSection(Dictionary<string, string> termTranslations)
    {
        if (!termTranslations.Any())
        {
            return "KNOWN TERMS:\nNo known terms yet.";
        }

        var termsList = string.Join("\n", termTranslations.Select(t => $"- {t.Key} -> {t.Value}"));
        return $"KNOWN TERMS:\n{termsList}";
    }

    /// <summary>
    /// Секция с контекстом до переводимых блоков
    /// </summary>
    private static string BuildContextBeforeSection(List<SrtBlock> contextBefore, Dictionary<int, string> translations)
    {
        if (!contextBefore.Any() || contextBefore.First().Number <= 1)
        {
            return string.Empty;
        }

        var contextBlocks = string.Join("\n\n", contextBefore.Select(b => 
            $"BLOCK {b.Number}:\n{translations.GetValueOrDefault(b.Number, b.Text)}"));
        
        return $"CONTEXT BEFORE:\n{contextBlocks}\n\n";
    }

    /// <summary>
    /// Секция с контекстом после переводимых блоков
    /// </summary>
    private static string BuildContextAfterSection(List<SrtBlock> contextAfter, List<SrtBlock> batch)
    {
        if (!contextAfter.Any() || contextAfter.Last().Number == batch.Last().Number)
        {
            return string.Empty;
        }

        var contextBlocks = string.Join("\n\n", contextAfter.Select(b => 
            $"BLOCK {b.Number}:\n{b.Text}"));
        
        return $"CONTEXT AFTER:\n{contextBlocks}";
    }

    /// <summary>
    /// Секция с блоками для перевода
    /// </summary>
    private static string BuildBlocksToTranslateSection(List<SrtBlock> batch, string language)
    {
        var blocksText = string.Join("\n\n", batch.Select(b => $"BLOCK {b.Number}:\n{b.Text}"));
        var rangeText = batch.Count == 1 
            ? batch.First().Number.ToString() 
            : $"{batch.First().Number}-{batch.Last().Number}";
        
        return $"TRANSLATE ONLY THESE BLOCKS {rangeText} TO {language.ToUpper()}:\n{blocksText}";
    }
}
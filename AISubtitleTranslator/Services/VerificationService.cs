using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AISubtitleTranslator.Models;

namespace AISubtitleTranslator.Services;

/// <summary>
/// Сервис для проверки качества перевода и исправления ошибок
/// </summary>
public class VerificationService : IVerificationService
{
    private const int DefaultContextSize = 10;
    private const int DefaultOverlap = 3;
    private readonly HttpClient _client;
    private readonly IHubCommunicationService _hubService;

    public VerificationService(HttpClient client, IHubCommunicationService hubService)
    {
        _client = client;
        _hubService = hubService;
    }

    /// <summary>
    /// Идентификация проблем перевода
    /// </summary>
    public async IAsyncEnumerable<TranslationIssue> IdentifyIssues(
        List<SrtBlock> originalBlocks,
        List<SrtBlock> translatedBlocks,
        string language)
    {
        await _hubService.SendVerificationStatusUpdate(
            "verification",
            "verification",
            "Инициализация проверки перевода...",
            5);
        
        var totalBatches = (int)Math.Ceiling(originalBlocks.Count / (double)(DefaultContextSize - DefaultOverlap));
        var currentBatch = 0;

        for (var i = 0; i < originalBlocks.Count; i += DefaultContextSize - DefaultOverlap)
        {
            currentBatch++;
            var progress = 5 + (int)(currentBatch / (double)totalBatches * 90);
            
            await _hubService.SendVerificationStatusUpdate(
                "verification",
                "verification",
                $"Анализ качества перевода... Обработано {currentBatch} из {totalBatches} групп",
                progress);

            var batchOriginal = originalBlocks.Skip(i).Take(DefaultContextSize).ToList();
            var batchTranslated = translatedBlocks.Skip(i).Take(DefaultContextSize).ToList();

            var verificationPrompt = BuildVerificationPrompt(batchOriginal, batchTranslated);

            var request = new
            {
                model = "mistral-large-2411",
                messages = new[]
                {
                    new { role = "system", content = Prompts.VerificationSystemPrompt(language) },
                    new { role = "user", content = verificationPrompt }
                },
                temperature = 0.1,
                top_p = 0.9
            };

            var response = await _client.PostAsJsonAsync("chat/completions", request);
            response.EnsureSuccessStatusCode();

            await Task.Delay(1000);

            var result = await response.Content.ReadFromJsonAsync<MistralResponse>();
            var responseText = result?.Choices.FirstOrDefault()?.Message.Content ?? "";

            var batchIssues = ParseVerificationResponse(responseText, batchOriginal, batchTranslated);
            foreach (var issue in batchIssues) yield return issue;
        }
        
        await _hubService.SendVerificationStatusUpdate(
            "verification",
            "verification",
            "Анализ качества перевода завершен",
            95);
    }

    /// <summary>
    /// Перевод проблемных блоков заново
    /// </summary>
    public async Task<List<SrtBlock>> RetranslateBlocks(
        List<TranslationIssue> issues,
        List<SrtBlock> allBlocks,
        string language,
        int contextBeforeSize,
        int contextAfterSize)
    {
        var result = new List<SrtBlock>(allBlocks);

        // Группируем проблемы по последовательным блокам
        var issueGroups = GroupConsecutiveIssues(issues);

        foreach (var group in issueGroups)
        {
            var minBlockNumber = group.Min(i => i.BlockNumber);
            var maxBlockNumber = group.Max(i => i.BlockNumber);

            // Получаем контекстные блоки
            var contextBefore = GetContextBlocks(allBlocks, minBlockNumber - 1, contextBeforeSize);
            var contextAfter = GetContextBlocks(allBlocks, maxBlockNumber + 1, contextAfterSize);

            // Формируем промпт для перевода
            var prompt = BuildRetranslationPrompt(
                group,
                contextBefore,
                contextAfter,
                language
            );

            // Отправляем запрос к API
            var response = await SendVerificationRequest(prompt);

            // Парсим ответ и обновляем блоки
            var translations = ParseVerificationResponse(response);

            // Обновляем только те блоки, которые были успешно переведены
            foreach (var translation in translations)
            {
                var blockIndex = result.FindIndex(b => b.Number == translation.Key);
                if (blockIndex != -1)
                    result[blockIndex] = new SrtBlock(
                        result[blockIndex].Number,
                        result[blockIndex].Time,
                        translation.Value
                    );
            }

            // Отправляем статус о прогрессе
            await _hubService.SendVerificationStatusUpdate(
                "retranslation",
                "retranslation",
                $"Перевод блоков {minBlockNumber}-{maxBlockNumber}",
                (int)((issueGroups.IndexOf(group) + 1) / (double)issueGroups.Count * 100));
        }

        return result;
    }

    /// <summary>
    /// Построение промпта для верификации перевода
    /// </summary>
    private string BuildVerificationPrompt(List<SrtBlock> originalBlocks, List<SrtBlock> translatedBlocks)
    {
        return $"""
                ORIGINAL BLOCKS:
                {string.Join("\n\n", originalBlocks.Select(b => $"BLOCK {b.Number}:\n{b.Text}"))}

                TRANSLATED BLOCKS:
                {string.Join("\n\n", translatedBlocks.Select(b => $"BLOCK {b.Number}:\n{b.Text}"))}
                """;
    }

    /// <summary>
    /// Парсинг ответа верификации перевода в формате JSON
    /// </summary>
    private static List<TranslationIssue> ParseVerificationResponse(
        string response,
        List<SrtBlock> originalBlocks,
        List<SrtBlock> translatedBlocks)
    {
        var issues = new List<TranslationIssue>();

        try
        {
            var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}");
            if (jsonMatch.Success)
            {
                var jsonText = jsonMatch.Value;
                var verificationResult = JsonSerializer.Deserialize<VerificationResult>(jsonText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (verificationResult?.Blocks != null)
                    foreach (var block in verificationResult.Blocks)
                    {
                        var originalBlock = originalBlocks.FirstOrDefault(b => b.Number == block.BlockNumber);
                        var translatedBlock = translatedBlocks.FirstOrDefault(b => b.Number == block.BlockNumber);

                        if (originalBlock != null && translatedBlock != null)
                            issues.Add(new TranslationIssue
                            {
                                BlockNumber = block.BlockNumber,
                                ProblemTypes = block.ProblemTypes,
                                OriginalText = originalBlock.Text,
                                CurrentTranslation = translatedBlock.Text,
                                QualityScore = block.QualityScore,
                                Recommendations = block.Recommendations
                            });
                    }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing verification response: {ex.Message}");
        }

        return issues;
    }

    /// <summary>
    /// Группировка последовательных проблем
    /// </summary>
    private List<List<TranslationIssue>> GroupConsecutiveIssues(List<TranslationIssue> issues)
    {
        if (!issues.Any()) return new List<List<TranslationIssue>>();
        
        var sortedIssues = issues.OrderBy(i => i.BlockNumber).ToList();
        var result = new List<List<TranslationIssue>>();
        var currentGroup = new List<TranslationIssue> { sortedIssues[0] };

        for (var i = 1; i < sortedIssues.Count; i++)
        {
            var prevIssue = sortedIssues[i - 1];
            var currentIssue = sortedIssues[i];
            
            if (currentIssue.BlockNumber - prevIssue.BlockNumber <= 2)
            {
                currentGroup.Add(currentIssue);
            }
            else
            {
                result.Add(currentGroup);
                currentGroup = new List<TranslationIssue> { currentIssue };
            }
        }

        result.Add(currentGroup);
        return result;
    }

    private Dictionary<int, string> ParseVerificationResponse(string response)
    {
        var result = new Dictionary<int, string>();
        var lines = response.Split('\n');
        var currentBlock = -1;
        var currentTranslation = new StringBuilder();

        foreach (var line in lines)
            if (line.StartsWith("BLOCK "))
            {
                // Если есть предыдущий блок, сохраняем его
                if (currentBlock != -1)
                {
                    var translation = currentTranslation.ToString().Trim();
                    // Проверяем, не является ли перевод пометкой UNCHANGED
                    if (translation != "UNCHANGED") result[currentBlock] = translation;
                    currentTranslation.Clear();
                }

                // Парсим номер нового блока
                if (int.TryParse(line.AsSpan(6), out var blockNumber)) currentBlock = blockNumber;
            }
            else if (currentBlock != -1)
            {
                currentTranslation.AppendLine(line);
            }

        // Сохраняем последний блок
        if (currentBlock != -1)
        {
            var translation = currentTranslation.ToString().Trim();
            if (translation != "UNCHANGED") result[currentBlock] = translation;
        }

        return result;
    }

    private List<SrtBlock> GetContextBlocks(List<SrtBlock> blocks, int startIndex, int count)
    {
        return blocks
            .Skip(Math.Max(0, startIndex))
            .Take(count)
            .ToList();
    }

    private async Task<string> SendVerificationRequest(string prompt)
    {
        var request = new
        {
            model = "mistral-large-2411",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content =
                        "You are a professional translator. Your task is to improve the translation of subtitle blocks."
                },
                new { role = "user", content = prompt }
            },
            temperature = 0.1,
            top_p = 0.9
        };

        var response = await _client.PostAsJsonAsync("chat/completions", request);
        response.EnsureSuccessStatusCode();

        await Task.Delay(1000);

        var result = await response.Content.ReadFromJsonAsync<MistralResponse>();
        return result?.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;
    }

    private static string BuildRetranslationPrompt(
        List<TranslationIssue> issues,
        List<SrtBlock> contextBefore,
        List<SrtBlock> contextAfter,
        string language)
    {
        var problemBlocks = issues.Select(i => new
        {
            i.BlockNumber,
            i.OriginalText,
            i.CurrentTranslation,
            ProblemTypes = string.Join(", ", i.ProblemTypes)
        }).ToList();

        var contextBeforeText = string.Join("\n", contextBefore.Select(b => $"BLOCK {b.Number}\n{b.Text}"));
        var contextAfterText = string.Join("\n", contextAfter.Select(b => $"BLOCK {b.Number}\n{b.Text}"));
        var problemBlocksText = string.Join("\n", problemBlocks.Select(b =>
            $"BLOCK {b.BlockNumber}\nOriginal: {b.OriginalText}\nCurrent: {b.CurrentTranslation}\nProblems: {b.ProblemTypes}"));

        return $"""
                You are a professional translator. Your task is to improve the translation of the following subtitle blocks.
                The current translation has some issues that need to be addressed.

                CONTEXT BEFORE:
                {contextBeforeText}

                PROBLEMATIC BLOCKS:
                {problemBlocksText}

                CONTEXT AFTER:
                {contextAfterText}

                INSTRUCTIONS:
                - Provide better translations for blocks {issues.Min(i => i.BlockNumber)}-{issues.Max(i => i.BlockNumber)} that address all identified issues
                - The new translation MUST be different from the current one
                - Each block should be translated only once
                - Maintain consistency with the context
                - Keep the translation concise and natural in {language}
                - Preserve all formatting and technical markers
                - If you cannot provide a better translation, mark the block as 'UNCHANGED'
                - Output ONLY the translated blocks in the following format:
                {string.Join("\n", problemBlocks.Select(b => $"BLOCK {b.BlockNumber}\n<translation>"))}
                - For blocks that cannot be improved, output:
                BLOCK {problemBlocks.Select(b => b.BlockNumber)}
                UNCHANGED
                """;
    }
}
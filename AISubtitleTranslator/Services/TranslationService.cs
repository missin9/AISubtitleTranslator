using AISubtitleTranslator.Hubs;
using AISubtitleTranslator.Models;
using Microsoft.AspNetCore.SignalR;

namespace AISubtitleTranslator.Services;

/// <summary>
/// Основной сервис для работы с переводом субтитров
/// </summary>
public class TranslationService : ITranslationService
{
    private const int DefaultDelayMs = 1000;
    private const int StatusCheckDelayMs = 500;
    private const int DefaultBlocksToTranslate = 4;
    private const int DefaultContextBeforeSize = 2;
    private const int DefaultContextAfterSize = 3;

    private readonly IHubCommunicationService _hubService;
    private readonly ISrtParser _srtParser;
    private readonly IMistralTranslator _translator;
    private readonly IVerificationService _verificationService;

    public TranslationService(
        IHubContext<TranslationHub> hubContext,
        ISrtParser srtParser,
        IMistralTranslator translator,
        IVerificationService verificationService,
        IHubCommunicationService hubService)
    {
        HubContext = hubContext;
        _srtParser = srtParser;
        _translator = translator;
        _verificationService = verificationService;
        _hubService = hubService;
    }

    public IHubContext<TranslationHub> HubContext { get; }

    /// <summary>
    /// Перевод содержимого SRT файла
    /// </summary>
    public async Task<List<SrtBlock>> TranslateSrt(string srtContent, string language, string translationId,
        int? llmSeed,
        TranslationStyle style = TranslationStyle.Natural,
        int blocksToTranslate = DefaultBlocksToTranslate,
        int contextBeforeSize = DefaultContextBeforeSize,
        int contextAfterSize = DefaultContextAfterSize)
    {
        if (string.IsNullOrEmpty(srtContent))
            throw new ArgumentException("SRT content cannot be empty", nameof(srtContent));
        if (string.IsNullOrEmpty(language))
            throw new ArgumentException("Language cannot be empty", nameof(language));
        if (string.IsNullOrEmpty(translationId))
            throw new ArgumentException("Translation ID cannot be empty", nameof(translationId));

        var (temperature, topP) = Prompts.GetStyleParameters(style);
        var systemPrompt = Prompts.TranslationSystemPrompt(language, style);

        var translationConfig = new TranslationConfig
        {
            Language = language,
            TranslationId = translationId,
            LlmSeed = llmSeed ?? new Random().Next(),
            BlocksToTranslate = blocksToTranslate,
            ContextBeforeSize = contextBeforeSize,
            ContextAfterSize = contextAfterSize,
            Temperature = temperature,
            TopP = topP,
            SystemPrompt = systemPrompt
        };

        var blocks = _srtParser.ParseSrt(srtContent);
        if (!blocks.Any())
            throw new InvalidOperationException("No valid SRT blocks found in the content");

        var translations = new Dictionary<int, string>();
        var totalBlocks = blocks.Count;
        var currentProgress = 0;

        await _hubService.UpdateTranslationProgress(translationId, 0, totalBlocks);

        for (var i = 0; i < blocks.Count; i += blocksToTranslate)
        {
            var batch = blocks.Skip(i).Take(blocksToTranslate).ToList();
            if (!batch.Any()) break;

            try
            {
                await CheckTranslationStatus(translationId);

                var contextBefore = GetContextBlocks(blocks, i - contextBeforeSize, contextBeforeSize);
                var contextAfter = GetContextBlocks(blocks, i + blocksToTranslate, contextAfterSize);

                var batchTranslationResult = await _translator.TranslateBatch(
                    batch, contextBefore, contextAfter, translations, translationConfig);

                foreach (var (blockNumber, translation) in batchTranslationResult)
                {
                    var block = batch.First(b => b.Number == blockNumber);
                    translations[blockNumber] = translation;

                    await _hubService.SendTranslationUpdate(blockNumber, translation);
                    await _hubService.SendOriginalUpdate(blockNumber, block.Text);

                    currentProgress++;
                    await _hubService.UpdateTranslationProgress(translationId, currentProgress, totalBlocks);
                }

                await Task.Delay(DefaultDelayMs);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await HandleTranslationError(batch, translations);
                // Логируем ошибку, но продолжаем работу
                Console.WriteLine($"Translation error occurred: {ex.Message}");
            }
        }

        await _hubService.UpdateTranslationProgress(translationId, totalBlocks, totalBlocks);

        return CreateResultBlocks(blocks, translations);
    }

    /// <summary>
    /// Парсинг SRT файла
    /// </summary>
    public List<SrtBlock> ParseSrt(string content)
    {
        return _srtParser.ParseSrt(content);
    }

    /// <summary>
    /// Создание SRT строки из блоков
    /// </summary>
    public string BuildTranslatedSrt(List<SrtBlock> blocks)
    {
        return _srtParser.BuildSrt(blocks);
    }

    /// <summary>
    /// Идентификация проблем в переводе
    /// </summary>
    public async IAsyncEnumerable<TranslationIssue> IdentifyTranslationIssues(
        List<SrtBlock> originalBlocks,
        List<SrtBlock> translatedBlocks,
        string language)
    {
        await foreach (var issue in _verificationService.IdentifyIssues(originalBlocks, translatedBlocks, language))
            yield return issue;
    }

    /// <summary>
    /// Повторный перевод проблемных блоков
    /// </summary>
    public async Task<List<SrtBlock>> RetranslateBlocks(
        List<TranslationIssue> issues,
        List<SrtBlock> allBlocks,
        string language,
        int contextBeforeSize,
        int contextAfterSize)
    {
        return await _verificationService.RetranslateBlocks(issues, allBlocks, language, contextBeforeSize,
            contextAfterSize);
    }

    /// <summary>
    /// Отправка перевода на утверждение
    /// </summary>
    public async Task SendTranslationForApproval(
        TranslationIssue issue,
        List<SrtBlock> contextBefore,
        List<SrtBlock> contextAfter)
    {
        await _hubService.SendTranslationForApproval(issue, contextBefore, contextAfter);
    }

    /// <summary>
    /// Отправка утвержденного перевода
    /// </summary>
    public async Task SendTranslationApproved(int blockNumber, string approvedTranslation,
        TranslationIssueStatus status)
    {
        await _hubService.SendTranslationApproved(blockNumber, approvedTranslation, status);
    }

    /// <summary>
    /// Обработка статуса перевода (пауза/отмена)
    /// </summary>
    private async Task CheckTranslationStatus(string translationId)
    {
        if (string.IsNullOrEmpty(translationId)) return;

        var status = TranslationHub.GetTranslationStatus(translationId);
        if (status.IsCancelled) throw new OperationCanceledException("Translation cancelled by user");

        while (status.IsPaused && !status.IsCancelled)
        {
            await Task.Delay(StatusCheckDelayMs);
            status = TranslationHub.GetTranslationStatus(translationId);
        }

        if (status.IsCancelled) throw new OperationCanceledException("Translation cancelled by user");
    }

    /// <summary>
    /// Получение блоков контекста
    /// </summary>
    private static List<SrtBlock> GetContextBlocks(List<SrtBlock> blocks, int startIndex, int count)
    {
        return blocks
            .Skip(Math.Max(0, startIndex))
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Создание итоговых блоков с переводом
    /// </summary>
    private static List<SrtBlock> CreateResultBlocks(List<SrtBlock> originalBlocks, Dictionary<int, string> translations)
    {
        return originalBlocks.Select(b => new SrtBlock(
            b.Number,
            b.Time,
            translations.TryGetValue(b.Number, out var t) ? t : b.Text)
        ).ToList();
    }

    /// <summary>
    /// Обработка ошибок перевода
    /// </summary>
    private async Task HandleTranslationError(List<SrtBlock> batch, Dictionary<int, string> translations)
    {
        foreach (var block in batch)
        {
            translations[block.Number] = block.Text;
            await _hubService.SendTranslationUpdate(block.Number, block.Text);
        }
    }
}
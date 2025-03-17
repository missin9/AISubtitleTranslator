using AISubtitleTranslator.Hubs;
using AISubtitleTranslator.Models;
using Microsoft.AspNetCore.SignalR;

namespace AISubtitleTranslator.Services;

/// <summary>
/// Интерфейс сервиса для работы с переводом субтитров
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Контекст SignalR хаба для коммуникации с клиентом
    /// </summary>
    IHubContext<TranslationHub> HubContext { get; }

    /// <summary>
    /// Переводит содержимое SRT файла
    /// </summary>
    /// <param name="srtContent">Содержимое SRT файла</param>
    /// <param name="language">Целевой язык перевода</param>
    /// <param name="translationId">Уникальный идентификатор перевода</param>
    /// <param name="llmSeed">Сид для генерации перевода (опционально)</param>
    /// <param name="style">Стиль перевода</param>
    /// <param name="blocksToTranslate">Количество блоков для одновременного перевода</param>
    /// <param name="contextBeforeSize">Размер контекста до переводимого блока</param>
    /// <param name="contextAfterSize">Размер контекста после переводимого блока</param>
    /// <returns>Список переведенных блоков субтитров</returns>
    Task<List<SrtBlock>> TranslateSrt(string srtContent, string language, string translationId, int? llmSeed,
        TranslationStyle style = TranslationStyle.Natural, int blocksToTranslate = 4, int contextBeforeSize = 2,
        int contextAfterSize = 3);

    /// <summary>
    /// Идентифицирует проблемы в переводе
    /// </summary>
    IAsyncEnumerable<TranslationIssue> IdentifyTranslationIssues(List<SrtBlock> originalBlocks,
        List<SrtBlock> translatedBlocks, string language);

    /// <summary>
    /// Повторно переводит проблемные блоки
    /// </summary>
    Task<List<SrtBlock>> RetranslateBlocks(List<TranslationIssue> issues, List<SrtBlock> allBlocks, string language,
        int contextBeforeSize, int contextAfterSize);

    /// <summary>
    /// Отправляет перевод на утверждение
    /// </summary>
    Task SendTranslationForApproval(TranslationIssue issue, List<SrtBlock> contextBefore = null,
        List<SrtBlock> contextAfter = null);

    /// <summary>
    /// Отправляет утвержденный перевод
    /// </summary>
    Task SendTranslationApproved(int blockNumber, string approvedTranslation, TranslationIssueStatus status);

    /// <summary>
    /// Парсит содержимое SRT файла
    /// </summary>
    List<SrtBlock> ParseSrt(string content);

    /// <summary>
    /// Создает строку SRT из блоков
    /// </summary>
    string BuildTranslatedSrt(List<SrtBlock> blocks);
}
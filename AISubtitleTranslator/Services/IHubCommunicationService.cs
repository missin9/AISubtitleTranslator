using AISubtitleTranslator.Models;

namespace AISubtitleTranslator.Services;

public interface IHubCommunicationService
{
    public Task UpdateTranslationProgress(string translationId, int currentProgress, int totalBlocks);

    public Task SendTranslationUpdate(int blockNumber, string translation);

    public Task SendOriginalUpdate(int blockNumber, string originalText);

    public Task SendTranslationForApproval(
        TranslationIssue issue,
        List<SrtBlock> contextBefore,
        List<SrtBlock> contextAfter);

    public Task SendTranslationApproved(int blockNumber, string approvedTranslation,
        TranslationIssueStatus status);

    /// <summary>
    /// Отправляет информацию о текущем шаге процесса проверки перевода
    /// </summary>
    /// <param name="translationId">Идентификатор перевода</param>
    /// <param name="stepName">Название текущего шага проверки</param>
    /// <param name="stepDescription">Описание текущего шага</param>
    /// <param name="stepPercentage">Процент выполнения текущего шага (0-100)</param>
    /// <returns>Task</returns>
    public Task SendVerificationStatusUpdate(string translationId, string stepName, string stepDescription,
        int stepPercentage);
}
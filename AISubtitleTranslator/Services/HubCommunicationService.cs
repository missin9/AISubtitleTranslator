using AISubtitleTranslator.Hubs;
using AISubtitleTranslator.Models;
using Microsoft.AspNetCore.SignalR;

namespace AISubtitleTranslator.Services;

public class HubCommunicationService : IHubCommunicationService
{
    private readonly IHubContext<TranslationHub> _hubContext;

    public HubCommunicationService(IHubContext<TranslationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task UpdateTranslationProgress(string translationId, int currentProgress, int totalBlocks)
    {
        await _hubContext.Clients.All.SendAsync("UpdateProgress", translationId, currentProgress, totalBlocks);
    }

    public async Task SendTranslationUpdate(int blockNumber, string translation)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveTranslation", blockNumber, translation);
    }

    public async Task SendOriginalUpdate(int blockNumber, string originalText)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveOriginal", blockNumber, originalText);
    }

    public async Task SendTranslationForApproval(TranslationIssue issue, List<SrtBlock> contextBefore,
        List<SrtBlock> contextAfter)
    {
        TranslationHub.RegisterIssue(issue);
        await _hubContext.Clients.All.SendAsync("TranslationForApproval", new
        {
            issue,
            contextBefore,
            contextAfter
        });
    }

    public async Task SendTranslationApproved(int blockNumber, string approvedTranslation,
        TranslationIssueStatus status)
    {
        await _hubContext.Clients.All.SendAsync("TranslationApproved", blockNumber, approvedTranslation, status);
    }

    /// <summary>
    /// Отправляет информацию о текущем шаге процесса проверки перевода
    /// </summary>
    /// <param name="translationId">Идентификатор перевода</param>
    /// <param name="stepName">Название текущего шага проверки</param>
    /// <param name="stepDescription">Описание текущего шага</param>
    /// <param name="stepPercentage">Процент выполнения текущего шага (0-100)</param>
    /// <returns>Task</returns>
    public async Task SendVerificationStatusUpdate(string translationId, string stepName, string stepDescription,
        int stepPercentage)
    {
        await _hubContext.Clients.All.SendAsync("VerificationStatusUpdate", translationId, stepName, stepDescription,
            stepPercentage);
    }
}
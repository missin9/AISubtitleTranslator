using System.Collections.Concurrent;
using AISubtitleTranslator.Models;
using Microsoft.AspNetCore.SignalR;

namespace AISubtitleTranslator.Hubs;

public class TranslationHub : Hub
{
    // Статический словарь для хранения состояний переводов
    private static readonly ConcurrentDictionary<string, TranslationStatus> TranslationStatuses = new();

    // Статический словарь для хранения задач ожидания подтверждения
    private static readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> ApprovalTasks = new();

    // Статический словарь для хранения текущей проблемы перевода
    private static readonly ConcurrentDictionary<int, TranslationIssue> CurrentIssues = new();

    // Метод для регистрации текущей проблемы
    public static void RegisterIssue(TranslationIssue issue)
    {
        CurrentIssues.AddOrUpdate(issue.BlockNumber, issue, (key, oldValue) => issue);
    }

    // Метод для отправки перевода на утверждение
    public async Task SendTranslationForApproval(TranslationIssue issue, List<SrtBlock> contextBefore,
        List<SrtBlock> contextAfter)
    {
        // Регистрируем проблему
        RegisterIssue(issue);

        // Отправляем данные на клиент
        await Clients.All.SendAsync("TranslationForApproval", new
        {
            issue,
            contextBefore,
            contextAfter
        });
    }

    // Метод для обновления статуса перевода
    public async Task UpdateTranslationStatus(string translationId, bool isPaused, bool isCancelled)
    {
        var status = new TranslationStatus { IsPaused = isPaused, IsCancelled = isCancelled };
        TranslationStatuses.AddOrUpdate(translationId, status, (key, oldValue) => status);

        // Оповещаем всех клиентов об изменении статуса
        await Clients.All.SendAsync("TranslationStatusChanged", translationId, isPaused, isCancelled);
    }

    // Метод для проверки состояния перевода
    public static TranslationStatus GetTranslationStatus(string translationId)
    {
        if (TranslationStatuses.TryGetValue(translationId, out var status)) return status;

        return new TranslationStatus { IsPaused = false, IsCancelled = false };
    }

    // Метод для очистки статуса перевода
    public static void ClearTranslationStatus(string translationId)
    {
        TranslationStatuses.TryRemove(translationId, out _);
    }

    // Метод для установки задачи ожидания
    public static void SetApprovalTask(string translationId, TaskCompletionSource<bool> tcs)
    {
        ApprovalTasks.AddOrUpdate(translationId, tcs, (key, oldValue) => tcs);
    }

    // Метод для удаления задачи ожидания
    public static void RemoveApprovalTask(string translationId)
    {
        ApprovalTasks.TryRemove(translationId, out _);
    }

    // Метод для обработки решения пользователя
    public async Task ApproveTranslation(int blockNumber, string approvedTranslation, TranslationIssueStatus status)
    {
        // Обновляем статус проблемы если она найдена
        if (CurrentIssues.TryGetValue(blockNumber, out var issue))
        {
            issue.Status = status;

            // Обновляем текст в зависимости от статуса
            if (status == TranslationIssueStatus.ManuallyEdited)
                issue.ManualTranslation = approvedTranslation;
            else if (status == TranslationIssueStatus.Approved) issue.ImprovedTranslation = approvedTranslation;
        }

        // Передаем статус в событие TranslationApproved
        await Clients.All.SendAsync("TranslationApproved", blockNumber, approvedTranslation, status);

        // Разрешаем продолжение перевода для всех ожидающих задач
        foreach (var entry in ApprovalTasks) entry.Value.TrySetResult(true);
    }

    /// <summary>
    /// Отправляет информацию о текущем шаге процесса проверки перевода
    /// </summary>
    /// <param name="translationId">Идентификатор перевода</param>
    /// <param name="stepName">Название шага проверки</param>
    /// <param name="stepDescription">Описание шага</param>
    /// <param name="stepPercentage">Процент выполнения (0-100)</param>
    public async Task SendVerificationStatusUpdate(string translationId, string stepName, string stepDescription,
        int stepPercentage)
    {
        await Clients.All.SendAsync("VerificationStatusUpdate", translationId, stepName, stepDescription,
            stepPercentage);
    }

    /// <summary>
    /// Получает статус утверждения для указанного блока
    /// </summary>
    /// <param name="blockNumber">Номер блока</param>
    /// <returns>Статус или null, если блок не найден</returns>
    public static TranslationIssueStatus? GetApprovalStatus(int blockNumber)
    {
        if (CurrentIssues.TryGetValue(blockNumber, out var issue)) return issue.Status;

        return null;
    }

    /// <summary>
    /// Получает утвержденный текст перевода для указанного блока
    /// </summary>
    /// <param name="blockNumber">Номер блока</param>
    /// <returns>Текст перевода или null, если блок не найден</returns>
    public static string GetApprovedTranslation(int blockNumber)
    {
        if (CurrentIssues.TryGetValue(blockNumber, out var issue))
            return issue.Status switch
            {
                TranslationIssueStatus.Approved => issue.ImprovedTranslation,
                TranslationIssueStatus.ManuallyEdited => issue.ManualTranslation,
                _ => issue.CurrentTranslation
            } ?? string.Empty;

        return string.Empty;
    }
}
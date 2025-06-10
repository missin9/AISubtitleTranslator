using System.Text;
using AISubtitleTranslator.Hubs;
using AISubtitleTranslator.Models;
using AISubtitleTranslator.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;

namespace AISubtitleTranslator.Pages;

public class IndexModel : PageModel
{
    private readonly IFileService _fileService;
    private readonly ITranslationService _translationService;

    public IndexModel(IFileService fileService, ITranslationService translationService)
    {
        _fileService = fileService;
        _translationService = translationService;
    }

    [BindProperty] public IFormFile? UploadedFile { get; set; }
    [BindProperty] public int? LlmSeed { get; set; }
    [BindProperty] public string TargetLanguage { get; set; } = "Russian";
    [BindProperty] public required string TranslationId { get; set; }
    [BindProperty] public string ContextSize { get; set; } = "medium";
    [BindProperty] public TranslationStyle TranslationStyle { get; set; } = TranslationStyle.Natural;

    private (int ContextBefore, int ContextAfter, int BlocksToTranslate) GetTranslationParameters()
    {
        return ContextSize switch
        {
            "small" => (15, 15, 30),
            "large" => (30, 30, 80),
            _ => (20, 20, 50) // medium по умолчанию
        };
    }

    private (int ContextBefore, int ContextAfter) GetVerificationContextParameters()
    {
        return (2, 2); // Фиксированный размер контекста для перепроверки
    }

    public void OnGet()
    {
        TranslationId = $"translation-{Guid.NewGuid()}";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ValidateUploadedFile()) return Page();

        try
        {
            if (UploadedFile == null)
            {
                ModelState.AddModelError(string.Empty, "Failed to process the file");
                return Page();
            }

            // Читаем содержимое SRT файла напрямую
            string srtContent;
            using (var reader = new StreamReader(UploadedFile.OpenReadStream()))
            {
                srtContent = await reader.ReadToEndAsync();
            }

            var (contextBefore, contextAfter, blocksToTranslate) = GetTranslationParameters();

            var translatedContent = await _translationService.TranslateSrt(
                srtContent,
                TargetLanguage,
                TranslationId,
                LlmSeed,
                TranslationStyle,
                blocksToTranslate,
                contextBefore,
                contextAfter);

            // Перепроверка будет выполняться только по запросу пользователя через JavaScript
            return await CreateResponseFileAsync(translatedContent);
        }
        catch (OperationCanceledException)
        {
            ModelState.AddModelError(string.Empty, "Translation was cancelled by user");
            return Page();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"An error occurred: {ex.Message}");
            return Page();
        }
        finally
        {
            CleanupTranslation();
        }
    }

    private bool ValidateUploadedFile()
    {
        if (UploadedFile?.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Please upload a file");
            return false;
        }

        if (UploadedFile != null && !_fileService.IsValidFile(UploadedFile))
        {
            ModelState.AddModelError(string.Empty, "Invalid file format");
            return false;
        }

        return true;
    }

    private async Task<IActionResult> CreateResponseFileAsync(List<SrtBlock> translatedContent)
    {
        var translatedString = _translationService.BuildTranslatedSrt(translatedContent);
        var translatedSrtPath = Path.Combine(Path.GetTempPath(), "translated.srt");

        await System.IO.File.WriteAllTextAsync(translatedSrtPath, translatedString);

        // Сохраняем оригинальный файл для возможной последующей проверки
        if (UploadedFile != null)
        {
            var originalSrtPath = Path.Combine(Path.GetTempPath(), "original.srt");
            using (var stream = System.IO.File.Create(originalSrtPath))
            {
                await UploadedFile.CopyToAsync(stream);
            }
        }

        return File(Encoding.UTF8.GetBytes(translatedString), "text/plain", "translated.srt");
    }

    private void CleanupTranslation()
    {
        if (!string.IsNullOrEmpty(TranslationId)) TranslationHub.ClearTranslationStatus(TranslationId);
    }

    public async Task<IActionResult> OnPostVerifyAsync()
    {
        try
        {
            // Проверяем, что это запрос на проверку только что переведенного текста
            if (string.IsNullOrEmpty(TranslationId) || !TranslationId.StartsWith("translation-"))
            {
                ModelState.AddModelError(string.Empty, "Неверный запрос на проверку перевода");
                return Page();
            }

            // Получаем оригинальный путь к файлу из сессии или кэша
            var originalSrtPath = Path.Combine(Path.GetTempPath(), "original.srt");
            var translatedSrtPath = Path.Combine(Path.GetTempPath(), "translated.srt");

            if (!System.IO.File.Exists(translatedSrtPath))
            {
                ModelState.AddModelError(string.Empty,
                    "Не удалось найти файл перевода. Пожалуйста, переведите файл заново.");
                return Page();
            }

            var originalContent = System.IO.File.Exists(originalSrtPath)
                ? await System.IO.File.ReadAllTextAsync(originalSrtPath)
                : ""; // Если оригинал не найден, используем пустую строку

            var translatedContent = await System.IO.File.ReadAllTextAsync(translatedSrtPath);

            // Парсим субтитры в блоки
            var originalBlocks = !string.IsNullOrEmpty(originalContent)
                ? _translationService.ParseSrt(originalContent)
                : new List<SrtBlock>(); // Если оригинал не найден, создаем пустой список

            var translatedBlocks = _translationService.ParseSrt(translatedContent);

            // Запускаем анализ перевода и получаем обновленные блоки
            var updatedBlocks = await ProcessVerificationIssuesAsync(originalBlocks, translatedBlocks);

            // По окончании проверки обновляем файл с исправленными субтитрами
            var verifiedContent = _translationService.BuildTranslatedSrt(updatedBlocks);
            return File(Encoding.UTF8.GetBytes(verifiedContent), "text/plain", "verified.srt");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Произошла ошибка: {ex.Message}");
            return Page();
        }
        finally
        {
            CleanupVerification();
        }
    }

    private async Task<List<SrtBlock>> ProcessVerificationIssuesAsync(
        List<SrtBlock> originalBlocks,
        List<SrtBlock> translatedBlocks)
    {
        var allIssues = new List<TranslationIssue>();
        var approvalTaskHolder = new ApprovalTaskHolder();
        var updatedBlocks = new List<SrtBlock>(translatedBlocks);

        // Сохраняем TCS в словаре для доступа из хаба
        TranslationHub.SetApprovalTask(TranslationId, approvalTaskHolder.Task);

        try
        {
            // Словарь для отслеживания обновленных переводов
            var updatedTranslations = new Dictionary<int, string>();

            await foreach (var issue in _translationService.IdentifyTranslationIssues(
                               originalBlocks, translatedBlocks, TargetLanguage))
            {
                allIssues.Add(issue);

                // Регистрируем проблему в хабе
                TranslationHub.RegisterIssue(issue);

                // Отправляем сигнал о новой проблеме
                await _translationService.HubContext.Clients.All.SendAsync("NewTranslationIssue", issue);

                if (ShouldProcessVerificationIssueGroup(allIssues, issue, translatedBlocks))
                {
                    // Получаем улучшенные блоки
                    var improvedBlocks = await ProcessVerificationIssueGroupAsync(
                        allIssues,
                        updatedBlocks,
                        approvalTaskHolder);

                    // Собираем решения пользователя для всех блоков
                    foreach (var processedIssue in allIssues)
                    {
                        var blockNumber = processedIssue.BlockNumber;
                        var status = TranslationHub.GetApprovalStatus(blockNumber);

                        if (status.HasValue)
                        {
                            var approvedText = TranslationHub.GetApprovedTranslation(blockNumber);

                            // Обновляем текст в словаре в зависимости от решения
                            if (status == TranslationIssueStatus.Approved ||
                                status == TranslationIssueStatus.ManuallyEdited)
                            {
                                if (!string.IsNullOrEmpty(approvedText))
                                    updatedTranslations[blockNumber] = approvedText;
                            }
                            // Для пропущенных блоков сохраняем оригинальный текст
                            else if (status == TranslationIssueStatus.Skipped)
                            {
                                var originalBlock = translatedBlocks.FirstOrDefault(b => b.Number == blockNumber);
                                if (originalBlock != null) updatedTranslations[blockNumber] = originalBlock.Text;
                            }
                        }
                    }

                    allIssues.Clear();
                }
            }

            // Применяем все сохраненные переводы к финальному списку блоков
            for (var i = 0; i < updatedBlocks.Count; i++)
            {
                var blockNumber = updatedBlocks[i].Number;
                if (updatedTranslations.TryGetValue(blockNumber, out var updatedText))
                    updatedBlocks[i] = new SrtBlock(
                        updatedBlocks[i].Number,
                        updatedBlocks[i].Time,
                        updatedText
                    );
            }
        }
        catch (Exception ex)
        {
            // Отправляем сигнал об ошибке
            await _translationService.HubContext.Clients.All.SendAsync("VerificationError", ex.Message);
            throw;
        }
        finally
        {
            // Если есть активная задача, отменяем ее
            if (!approvalTaskHolder.Task.Task.IsCompleted) approvalTaskHolder.Task.TrySetResult(true);

            // Очищаем задачи ожидания
            TranslationHub.RemoveApprovalTask(TranslationId);
        }

        return updatedBlocks;
    }

    private bool ShouldProcessVerificationIssueGroup(
        List<TranslationIssue> issues,
        TranslationIssue currentIssue,
        List<SrtBlock> translatedBlocks)
    {
        return issues.Count >= 3 || currentIssue.BlockNumber == translatedBlocks.Last().Number;
    }

    private async Task<List<SrtBlock>> ProcessVerificationIssueGroupAsync(
        List<TranslationIssue> issues,
        List<SrtBlock> allBlocks,
        ApprovalTaskHolder approvalTaskHolder)
    {
        try
        {
            // Получаем улучшенные переводы для проблемных блоков
            var improvedBlocks = await _translationService.RetranslateBlocks(
                issues,
                allBlocks,
                TargetLanguage,
                GetVerificationContextParameters().ContextBefore,
                GetVerificationContextParameters().ContextAfter);

            // Если нет улучшенных блоков, просто продолжаем
            if (!improvedBlocks.Any())
            {
                // Разблокируем задачу, чтобы продолжить обработку
                approvalTaskHolder.Task.TrySetResult(true);
                return new List<SrtBlock>();
            }

            // Отправляем каждый проблемный блок на утверждение
            foreach (var issue in issues)
            {
                // Находим улучшенный перевод для текущего блока
                var improvedBlock = improvedBlocks.FirstOrDefault(b => b.Number == issue.BlockNumber);
                if (improvedBlock != null)
                    issue.ImprovedTranslation = improvedBlock.Text;
                else
                    // Если улучшенный перевод не найден, используем текущий перевод
                    issue.ImprovedTranslation = issue.CurrentTranslation;

                // Получаем контекст для текущего блока
                var contextBefore = allBlocks
                    .Where(b => b.Number < issue.BlockNumber)
                    .OrderByDescending(b => b.Number)
                    .Take(GetVerificationContextParameters().ContextBefore)
                    .OrderBy(b => b.Number)
                    .ToList();

                var contextAfter = allBlocks
                    .Where(b => b.Number > issue.BlockNumber)
                    .OrderBy(b => b.Number)
                    .Take(GetVerificationContextParameters().ContextAfter)
                    .ToList();

                // Отправляем блок на утверждение
                await _translationService.SendTranslationForApproval(issue, contextBefore, contextAfter);

                // Ждем решения пользователя
                await approvalTaskHolder.Task.Task;

                // Создаем новую задачу для следующего блока
                approvalTaskHolder.Task = new TaskCompletionSource<bool>();
                TranslationHub.SetApprovalTask(TranslationId, approvalTaskHolder.Task);
            }

            // Возвращаем улучшенные блоки, решение о применении будет принято в вызывающем методе
            return improvedBlocks;
        }
        catch (Exception ex)
        {
            await _translationService.HubContext.Clients.All.SendAsync("VerificationError", ex.Message);
            throw;
        }
    }

    private void CleanupVerification()
    {
        if (!string.IsNullOrEmpty(TranslationId))
        {
            TranslationHub.ClearTranslationStatus(TranslationId);
            TranslationHub.RemoveApprovalTask(TranslationId);
        }
    }

    // Вспомогательный класс для хранения TaskCompletionSource
    private class ApprovalTaskHolder
    {
        public TaskCompletionSource<bool> Task { get; set; } = new();
    }
}
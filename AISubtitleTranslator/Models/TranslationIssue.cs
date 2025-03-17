namespace AISubtitleTranslator.Models;

public class TranslationIssue
{
    public int BlockNumber { get; set; }
    public List<string> ProblemTypes { get; set; } = new();
    public string OriginalText { get; set; } = string.Empty;
    public string CurrentTranslation { get; set; } = string.Empty;
    public string? ImprovedTranslation { get; set; }
    public TranslationIssueStatus Status { get; set; } = TranslationIssueStatus.Pending;
    public string? ManualTranslation { get; set; }

    /// <summary>
    /// Оценка качества перевода от 1 до 10
    /// </summary>
    public int QualityScore { get; set; } = 0;

    /// <summary>
    /// Рекомендации для улучшения перевода
    /// </summary>
    public string? Recommendations { get; set; }
}

public enum TranslationIssueStatus
{
    Pending,
    Approved,
    Rejected,
    ManuallyEdited,
    Skipped
}
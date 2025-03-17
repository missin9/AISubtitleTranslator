namespace AISubtitleTranslator.Models;

/// <summary>
/// Класс для представления блока с проблемами
/// </summary>
public class VerificationBlock
{
    public int BlockNumber { get; set; }
    public List<string> ProblemTypes { get; set; } = new();
    public int QualityScore { get; set; }
    public string Recommendations { get; set; } = string.Empty;
}
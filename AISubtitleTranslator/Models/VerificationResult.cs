namespace AISubtitleTranslator.Models;

/// <summary>
/// Класс для десериализации JSON ответа верификации
/// </summary>
public class VerificationResult
{
    public List<VerificationBlock> Blocks { get; set; } = new();
}
namespace AISubtitleTranslator.Models;

/// <summary>
///     Класс конфигурации для перевода
/// </summary>
public class TranslationConfig
{
    public string Language { get; set; }
    public string TranslationId { get; set; }
    public int LlmSeed { get; set; }
    public int BlocksToTranslate { get; set; }
    public int ContextBeforeSize { get; set; }
    public int ContextAfterSize { get; set; }
    public double Temperature { get; set; } = 0.3;
    public double TopP { get; set; } = 0.9;
    public string SystemPrompt { get; set; }
}
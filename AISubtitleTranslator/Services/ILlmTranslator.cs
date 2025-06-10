using AISubtitleTranslator.Models;

namespace AISubtitleTranslator.Services;

public interface ILlmTranslator
{
    Task<TranslationResponse> TranslateBatch(
        List<SrtBlock> batch,
        List<SrtBlock> contextBefore,
        List<SrtBlock> contextAfter,
        Dictionary<int, string> existingTranslations,
        TranslationConfig config,
        Dictionary<string, string> termTranslations);
}
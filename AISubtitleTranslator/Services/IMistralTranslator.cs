using AISubtitleTranslator.Models;

namespace AISubtitleTranslator.Services;

public interface IMistralTranslator
{
    public Task<Dictionary<int, string>> TranslateBatch(
        List<SrtBlock> batch,
        List<SrtBlock> contextBefore,
        List<SrtBlock> contextAfter,
        Dictionary<int, string> existingTranslations,
        TranslationConfig config);
}
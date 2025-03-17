using AISubtitleTranslator.Models;

namespace AISubtitleTranslator.Services;

public interface IVerificationService
{
    public IAsyncEnumerable<TranslationIssue> IdentifyIssues(
        List<SrtBlock> originalBlocks,
        List<SrtBlock> translatedBlocks,
        string language);

    public Task<List<SrtBlock>> RetranslateBlocks(
        List<TranslationIssue> issues,
        List<SrtBlock> allBlocks,
        string language,
        int contextBeforeSize,
        int contextAfterSize);
}
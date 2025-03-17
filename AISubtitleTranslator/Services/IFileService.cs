namespace AISubtitleTranslator.Services;

public interface IFileService
{
    bool IsValidFile(IFormFile file);
}
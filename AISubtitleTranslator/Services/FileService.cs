namespace AISubtitleTranslator.Services;

public class FileService : IFileService
{ 
    public bool IsValidFile(IFormFile file)
    {
        var allowedExtensions = new[] { ".srt" };
        var extension = Path.GetExtension(file.FileName).ToLower();
        return allowedExtensions.Contains(extension);
    }
}
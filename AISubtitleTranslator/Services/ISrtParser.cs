using AISubtitleTranslator.Models;

namespace AISubtitleTranslator.Services;

public interface ISrtParser
{
    public List<SrtBlock> ParseSrt(string content);

    public string BuildSrt(List<SrtBlock> blocks);

    public Dictionary<int, string> ParseMistralResponse(string response, List<SrtBlock> expectedBlocks);

    public string CleanTranslation(string response);
}
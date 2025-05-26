using System.Text.Json.Serialization;

namespace AISubtitleTranslator.Models;

public class TranslationResponse
{
    [JsonPropertyName("translations")]
    public List<SrtBlock> Translations { get; set; }

    [JsonPropertyName("terms")]
    public Dictionary<string, string> Terms { get; set; }
}
using System.Text.Json.Serialization;

namespace AISubtitleTranslator.Models;

public class SrtBlock
{
    public SrtBlock(int number, string time, string text)
    {
        Number = number;
        Time = time;
        Text = text;
    }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("time")]
    public string Time { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    public void Deconstruct(out int blocknumber, out string translation)
    {
        blocknumber = Number;
        
        translation = Text;
    }
}
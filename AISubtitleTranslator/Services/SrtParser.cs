using System.Text;
using System.Text.RegularExpressions;
using AISubtitleTranslator.Models;

namespace AISubtitleTranslator.Services;

/// <summary>
/// Парсер SRT файлов
/// </summary>
public partial class SrtParser : ISrtParser
{
    /// <summary>
    /// Парсинг SRT файла
    /// </summary>
    public List<SrtBlock> ParseSrt(string content)
    {
        var blocks = new List<SrtBlock>();
        var blocksText = ParseSrtRegex().Split(content.Trim());

        foreach (var blockText in blocksText)
        {
            var lines = blockText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToArray();

            if (lines.Length < 3) continue;
            
            if (int.TryParse(lines[0], out var number) && lines[1].Contains("-->"))
            {
                var time = lines[1];
                var text = string.Join("\n", lines.Skip(2));
                blocks.Add(new SrtBlock(number, time, text));
            }
        }
        
        return blocks.OrderBy(b => b.Number).ToList();
    }

    /// <summary>
    /// Построение SRT файла из блоков
    /// </summary>
    public string BuildSrt(List<SrtBlock> blocks)
    {
        var sb = new StringBuilder();
        foreach (var block in blocks)
        {
            sb.AppendLine(block.Number.ToString());
            sb.AppendLine(block.Time);
            sb.AppendLine(block.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Парсинг ответа API Mistral
    /// </summary>
    public Dictionary<int, string> ParseMistralResponse(string response, List<SrtBlock> expectedBlocks)
    {
        var result = new Dictionary<int, string>();
        var blockRegex = ParseMistralResponseRegex();

        var matches = blockRegex.Matches(response);
        foreach (Match match in matches)
            if (int.TryParse(match.Groups[1].Value, out var blockNumber))
                result[blockNumber] = match.Groups[2].Value.Trim();

        foreach (var block in expectedBlocks)
            if (!result.ContainsKey(block.Number))
                result[block.Number] = block.Text;

        return result;
    }

    /// <summary>
    /// Очистка перевода от тегов и форматирования
    /// </summary>
    public string CleanTranslation(string response)
    {
        var start = response.IndexOf("[[TRANSLATION]]", StringComparison.Ordinal);
        if (start >= 0) response = response[(start + "[[TRANSLATION]]".Length)..];

        var filteredResponse = string.Join("\n", response.Split('\n')
            .Where(line => line.Trim() != ":"));

        return Regex.Replace(filteredResponse.Trim(),
                @"(\[\d+\]|\([^)]+\)|\b\d+\b\s*$)|\[\[.*?\]\]", "")
            .Trim();
    }

    [GeneratedRegex(@"BLOCK (\d+)\s*(.*?)(?=\nBLOCK \d+|$)", RegexOptions.Singleline)]
    private static partial Regex ParseMistralResponseRegex();

    [GeneratedRegex(@"\r?\n\r?\n", RegexOptions.Multiline)]
    private static partial Regex ParseSrtRegex();
}
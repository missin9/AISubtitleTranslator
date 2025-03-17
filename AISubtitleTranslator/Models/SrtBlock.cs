namespace AISubtitleTranslator.Models;

public class SrtBlock
{
    public SrtBlock(int number, string time, string text)
    {
        Number = number;
        Time = time;
        Text = text;
    }

    public int Number { get; set; }
    public string Time { get; set; }
    public string Text { get; set; }
}
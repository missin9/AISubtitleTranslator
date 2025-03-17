namespace AISubtitleTranslator.Models;

public enum TranslationStyle
{
    /// <summary>
    /// Точный перевод с сохранением всех деталей
    /// </summary>
    Precise,

    /// <summary>
    /// Естественный перевод с адаптацией под русский язык
    /// </summary>
    Natural,

    /// <summary>
    /// Креативный перевод с сохранением стиля оригинала
    /// </summary>
    Creative
}
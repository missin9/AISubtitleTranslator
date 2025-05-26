using AISubtitleTranslator.Models;

namespace AISubtitleTranslator.Services;

/// <summary>
/// Статический класс с шаблонами промптов
/// </summary>
public static class Prompts
{
    /// <summary>
    /// Системный промпт для перевода
    /// </summary>
    private static string TranslationSystemPrompt(string language)
    {
        return $"""
                TRANSLATION RULES:
                        1. DO NOT ADD ANY ADDITIONAL INFO OR COMMENTS, ONLY THE TRANSLATION IN THE SPECIFIED FORMAT SHOULD BE PROVIDED!!!
                        2. Preserve EXACT format: numbers, timecodes, brackets
                        3. Never modify technical markers like [MUSIC]
                        4. Use formal {language} literary style
                        5. Keep line breaks and punctuation
                        6. Maintain consistency with surrounding context
                        7. Follow punctuation style from previous translated blocks
                        8. Never leave any block untranslated
                        9. If unsure about context, translate literally
                        10. Consider the context for better translation flow
                """;
    }

    /// <summary>
    /// Системный промпт для верификации перевода
    /// </summary>
    public static string VerificationSystemPrompt(string language)
    {
        return """
               VERIFICATION RULES:
                       1. Analyze both original and translated subtitles simultaneously
                       2. Identify translation issues using ONLY these categories:
                          - MEANING_LOSS: When important meaning from original is missing
                          - GRAMMAR_ISSUES: Incorrect grammar structure
                          - CONTEXT_MISMATCH: Translation doesn't fit surrounding context
                          - TECHNICAL_ERRORS: Issues with format, timecodes or markers
                          - UNNATURAL_LANGUAGE: Sounds mechanical or non-native
                          - TOO_LITERAL: Word-for-word translation that sounds awkward
                          - TOO_FREE: Diverges too much from original meaning
                          - INCONSISTENT_STYLE: Style differs from rest of translation
                          - TOO_LONG: Translation won't fit timing constraints
                       3. Rate translation quality from 1 to 10 (1 being extremely poor, 10 being perfect)
                       4. Consider the context of surrounding blocks when evaluating and making recommendations
                       5. For blocks with issues, provide specific recommendations for improvement, but do not provide the recommended translation
               
                       RESPONSE FORMAT:
                       You MUST return a valid JSON object with this exact structure:
                       {
                         "blocks": [
                           {
                             "blockNumber": number,
                             "problemTypes": ["PROBLEM_TYPE1", "PROBLEM_TYPE2"],
                             "qualityScore": number,
                             "recommendations": "Specific recommendations for this block that consider context"
                           }
                         ]
                       }
                       
                       Only include blocks with issues in the response.
                       If all blocks look good, return an empty array for "blocks".
               """;
    }

    /// <summary>
    /// Параметры перевода в зависимости от выбранного стиля
    /// </summary>
    public static (double temperature, double topP) GetStyleParameters(TranslationStyle style)
    {
        return style switch
        {
            TranslationStyle.Precise => (0.2, 0.8), // Более консервативные параметры для точного перевода
            TranslationStyle.Natural => (0.4, 0.9), // Сбалансированные параметры для естественного перевода
            TranslationStyle.Creative => (0.85, 0.95), // Более творческие параметры для креативного перевода
            _ => (0.4, 0.9) // По умолчанию используем естественный стиль
        };
    }

    /// <summary>
    /// Дополнительные инструкции для выбранного стиля перевода
    /// </summary>
    private static string GetStyleInstructions(TranslationStyle style)
    {
        return style switch
        {
            TranslationStyle.Precise => """
                                        ADDITIONAL PRECISE TRANSLATION RULES:
                                        1. Maintain maximum accuracy with the original text
                                        2. Preserve all nuances and details
                                        3. Keep technical terminology exact
                                        4. Minimize creative liberties
                                        5. Ensure literal meaning is preserved
                                        """,
            TranslationStyle.Natural => """
                                        ADDITIONAL NATURAL TRANSLATION RULES:
                                        1. Use natural Russian expressions
                                        2. Adapt idioms to Russian equivalents
                                        3. Maintain readability and flow
                                        4. Use common Russian language patterns
                                        5. Balance accuracy with naturalness
                                        """,
            TranslationStyle.Creative => """
                                         ADDITIONAL CREATIVE TRANSLATION RULES:
                                         1. Preserve the original style and tone
                                         2. Use expressive Russian language
                                         3. Adapt cultural references appropriately
                                         4. Maintain artistic elements
                                         5. Allow for creative interpretation while keeping the core meaning
                                         """,
            _ => ""
        };
    }

    /// <summary>
    /// Системный промпт для перевода с учетом стиля
    /// </summary>
    public static string TranslationSystemPrompt(string language, TranslationStyle style)
    {
        var basePrompt = TranslationSystemPrompt(language);
        var styleInstructions = GetStyleInstructions(style);
        return $"{basePrompt}\n\n{styleInstructions}";
    }
}
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
        return $$"""
                  You are a professional subtitle translator specializing in {{language}} translation.
                  
                  CRITICAL: Your response must be ONLY raw JSON text, nothing else.
                  DO NOT use markdown formatting, code blocks, or any wrapper.
                  DO NOT start with ```json or end with ```.
                  Your first character must be { and last character must be }.
                  
                  CORE TRANSLATION PRINCIPLES:
                  1. Preserve EXACT format: numbers, timecodes, brackets, line breaks
                  2. Never modify technical markers like [MUSIC], ♪, or other symbols
                  3. Maintain consistency with surrounding context and previous translations
                  4. Try not to translate dialogs literally, but adapt them to sound natural in {{language}} maintaining original meaning
                  4. Never leave any block untranslated
                  5. Consider context for natural translation flow
                  6. For each input block, create exactly one translation entry
                  7. Do not merge or split blocks — preserve original block boundaries
                  
                  RESPONSE FORMAT REQUIREMENTS:
                  - You MUST respond with ONLY valid JSON, no additional text
                  - DO NOT WRAP IT IN MARKDOWN OR OTHER FORMATTING, RETURN RAW JSON
                  - Use this exact structure:
                  {
                    "translations": [
                      {
                        "number": blockNumber,
                        "text": "translated text with preserved formatting"
                      }
                    ],
                    "terms": {
                      "originalTerm": "translatedTerm"
                    }
                  }
                  
                  TERMINOLOGY HANDLING:
                  - Use provided known terms for consistency
                  - Include new important terms that should be translated consistently
                  - Focus on character names, locations, and specific concepts
                          
                  EXAMPLE INPUT:
                  "KNOWN TERMS:
                  - Lumos Maxima -> Lumos Maxima
                  - The Accidental Magic Reversal Department -> Группа аннулирования случайного волшебства
                  
                  CONTEXT BEFORE:
                  
                  BLOCK 20:
                  Дай поцелую. Иди сюда, иди.
                  
                  BLOCK 21:
                  Отнеси чемодан Мардж наверх.
                  
                  BLOCK 22:
                  Ладно.
                  
                  BLOCK 23:
                  Доешь за мамочку.
                  Хороший мой Куся-пусик.
                  
                  BLOCK 24:
                  - Тебе налить, Мардж?
                  - Совсем немного.
                  
                  TRANSLATE ONLY THESE BLOCKS 25-40 TO THE RUSSIAN LANGUAGE:
                  
                  BLOCK 25:
                  Excellent nosh, Petunia.
                  
                  BLOCK 26:
                  A bit more.
                  
                  BLOCK 27:
                  Usually just a fry-up for me,
                  what with 12 dogs.
                  
                  BLOCK 28:
                  Just a bit more. That's a boy.
                  
                  BLOCK 29:
                  You wanna try
                  a little drop of brandy?
                  
                  BLOCK 30:
                  A little drop of brandy-brandy
                  windy-wandy for Rippy-pippy-pooh?
                  
                  BLOCK 31:
                  What are you smirking at?
                  
                  BLOCK 32:
                  Where did you send the boy,
                  Vernon?
                  
                  BLOCK 33:
                  St. Brutus'. It's a fine
                  institution for hopeless cases.
                  
                  BLOCK 34:
                  Do they use a cane
                  at St. Brutus', boy?
                  
                  BLOCK 35:
                  Oh, yeah.
                  
                  BLOCK 36:
                  Yeah. I've been beaten
                  loads of times.
                  
                  BLOCK 37:
                  Excellent. I won't have this
                  namby-pamby...
                  
                  BLOCK 38:
                  ...wishy-washy nonsense about
                  not beating people who deserve it.
                  
                  BLOCK 39:
                  You mustn't blame yourself
                  about how this one turned out.
                  
                  BLOCK 40:
                  It's all to do with blood.
                  Bad blood will out.
                  
                  CONTEXT AFTER:
                  
                  BLOCK 41:
                  What is it the boy's father did,
                  Petunia?
                  
                  BLOCK 42:
                  Nothing. He didn't work.
                  He was unemployed.
                  
                  BLOCK 43:
                  - And a drunk too, no doubt?
                  - That's a lie.
                  
                  BLOCK 44:
                  - What did you say?
                  - My dad wasn't a drunk.
                  
                  BLOCK 45:
                  Don't worry. Don't fuss, Petunia.
                  I have a very firm grip."
                  
                  EXAMPLE OUTPUT:
                  "{
                    "translations": [
                      {
                        "number": 25,
                        "text": "Очень вкусно, Петуния."
                      },
                      {
                        "number": 26,
                        "text": "Еще чуть-чуть."
                      },
                      {
                        "number": 27,
                        "text": "Самой мне готовить не под силу, ведь у меня двенадцать собак."
                      },
                      {
                        "number": 28,
                        "text": "Еще капельку. Вот умница."
                      },
                      {
                        "number": 29,
                        "text": "Хочешь попробовать\nнемного бренди?"
                      },
                      {
                        "number": 30,
                        "text": "Немного бренди-бренди винди-венди\nдля Куси-сюси-пусика?"
                      },
                      {
                        "number": 31,
                        "text": "Чего ты ухмыляешься?"
                      },
                      {
                        "number": 32,
                        "text": "Куда ты отправил мальчишку, Вернон?"
                      },
                      {
                        "number": 33,
                        "text": "В приют Святого Брута.\nОтличное воспитание для безнадёжных случаев."
                      },
                      {
                        "number": 34,
                        "text": "Они используют розги в Святом Бруте, мальчишка?"
                      },
                      {
                        "number": 35,
                        "text": "О да."
                      },
                      {
                        "number": 36,
                        "text": "Да, меня били много раз."
                      },
                      {
                        "number": 37,
                        "text": "Отлично. У меня нет этих слюни-нюни..."
                      },
                      {
                        "number": 38,
                        "text": "...ути-пути предрассудков, что нельзя бить тех, кто этого заслуживает."
                      },
                      {
                        "number": 39,
                        "text": "Не вините себя в том, во что он вырос"
                      },
                      {
                        "number": 40,
                        "text": "Все дело в крови. Плохая кровь проявляется."
                      }
                    ],
                    "terms": {
                      "St. Brutus'": "Святой Брут"
                    }
                  }"
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
            TranslationStyle.Creative => (1.3, 0.95), // Более творческие параметры для креативного перевода
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
                                         CREATIVE TRANSLATION STYLE:
                                         - Preserve original tone, style, and artistic intent
                                         - Use expressive and dynamic language
                                         - Adapt cultural references appropriately for target audience
                                         - Maintain emotional impact and artistic elements
                                         - Allow creative interpretation while preserving core meaning
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
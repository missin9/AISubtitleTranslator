using System.Text.Json.Serialization;

namespace AISubtitleTranslator.Models;

public record MistralResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("object")] string ObjectType,
    [property: JsonPropertyName("created")]
    long Created,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("choices")]
    List<MistralChoice> Choices,
    [property: JsonPropertyName("usage")] MistralUsage Usage
);

public record MistralChoice(
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("message")]
    MistralMessage Message,
    [property: JsonPropertyName("finish_reason")]
    string FinishReason
);

public record MistralMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")]
    string Content
);

public record MistralUsage(
    [property: JsonPropertyName("prompt_tokens")]
    int PromptTokens,
    [property: JsonPropertyName("completion_tokens")]
    int CompletionTokens,
    [property: JsonPropertyName("total_tokens")]
    int TotalTokens
);
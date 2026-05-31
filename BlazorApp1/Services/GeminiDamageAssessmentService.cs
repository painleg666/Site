using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using MyBlazorSite.Data;

namespace MyBlazorSite.Services;

public class GeminiDamageAssessmentService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeminiDamageAssessmentService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<AiDamageResult> AnalyzeAsync(
        byte[] imageBytes,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API ключ не найден. Добавьте его через User Secrets или переменную окружения GEMINI_API_KEY.");
        }

        if (imageBytes.Length == 0)
            throw new InvalidOperationException("Фото пустое.");

        if (string.IsNullOrWhiteSpace(contentType))
            contentType = "image/jpeg";

        var model = _configuration["Gemini:Model"];

        if (string.IsNullOrWhiteSpace(model))
            model = "gemini-2.0-flash";

        var base64 = Convert.ToBase64String(imageBytes);

        var requestBody = BuildRequestBody(base64, contentType);
        var requestJson = JsonSerializer.Serialize(requestBody);

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gemini API вернул ошибку {(int)response.StatusCode}: {responseText}");
        }

        var outputText = ExtractGeminiText(responseText);

        if (string.IsNullOrWhiteSpace(outputText))
            throw new InvalidOperationException("Gemini API не вернул текстовый JSON-результат.");

        outputText = CleanJsonText(outputText);

        GeminiDamageModelResult? modelResult;

        try
        {
            modelResult = JsonSerializer.Deserialize<GeminiDamageModelResult>(outputText, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Gemini вернул неполный или невалидный JSON. " +
                "Чаще всего это происходит из-за обрезанного ответа. " +
                $"Ответ модели: {outputText}", ex);
        }

        if (modelResult is null)
            throw new InvalidOperationException("Gemini вернул пустой результат.");

        var items = modelResult.Damages
            .Where(x => !string.IsNullOrWhiteSpace(x.DetectedPart))
            .Select(x => new AiDamageItem
            {
                DetectedPart = NormalizeText(x.DetectedPart, "Не определено"),
                DamageType = NormalizeText(x.DamageType, "Не определено"),
                RepairType = NormalizeText(x.RepairType, "Не определено"),
                DamageLevel = NormalizeText(x.DamageLevel, "Не определено"),
                Confidence = Math.Clamp(x.Confidence, 0, 1),
                NeedsHumanReview = x.NeedsHumanReview,
                Comment = string.IsNullOrWhiteSpace(x.Comment)
                    ? "Комментарий по позиции не указан."
                    : x.Comment.Trim()
            })
            .Select(x => x.WithCalculatedCost())
            .ToList();

        if (items.Count == 0)
        {
            items.Add(new AiDamageItem
            {
                DetectedPart = "Не определено",
                DamageType = "Не определено",
                RepairType = "Не определено",
                DamageLevel = "Не определено",
                Confidence = 0.2,
                EstimatedCost = 0,
                NeedsHumanReview = true,
                Comment = "Gemini не смог уверенно определить повреждённые зоны."
            });
        }

        var primary = items
            .OrderByDescending(x => GetSeverityScore(x.DamageLevel))
            .ThenByDescending(x => x.EstimatedCost)
            .ThenByDescending(x => x.Confidence)
            .First();

        var totalCost = items.Sum(x => x.EstimatedCost);
        var averageConfidence = items.Count == 0 ? 0 : items.Average(x => x.Confidence);

        return new AiDamageResult
        {
            DetectedPart = primary.DetectedPart,
            DamageType = primary.DamageType,
            RepairType = primary.RepairType,
            DamageLevel = primary.DamageLevel,
            Confidence = Math.Clamp(averageConfidence, 0, 1),
            EstimatedCost = totalCost,
            IsCarDamagePhoto = modelResult.IsCarDamagePhoto,
            NeedsHumanReview = modelResult.NeedsHumanReview || items.Any(x => x.NeedsHumanReview),
            Comment = string.IsNullOrWhiteSpace(modelResult.OverallComment)
                ? "Gemini вернул несколько повреждённых зон. Результат требует проверки оценщиком."
                : modelResult.OverallComment.Trim(),
            Items = items
        };
    }

    private static object BuildRequestBody(string base64Image, string contentType)
    {
        return new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new
                        {
                            text = """
                            Проанализируй фото автомобиля после ДТП.

                            Важно: на фото может быть несколько повреждённых деталей.
                            Нужно вернуть НЕ одну деталь, а список всех явно видимых повреждённых зон.
                            Например: капот, переднее крыло, передний бампер, фара — отдельными позициями.

                            Верни повреждения в порядке важности: от самой серьёзной зоны к менее серьёзной.

                            Не выдумывай скрытые повреждения.
                            Не добавляй детали, которых не видно на фото.
                            Если деталь видна частично, но повреждение очевидно — добавь её.
                            Если фото плохое или повреждение не видно, верни одну позицию "Не определено".

                            Не оценивай финальную стоимость. Система рассчитает цену отдельно.
                            Сторона левая/правая определяется относительно автомобиля, а не относительно зрителя.
                            Если точно не видно сторону, выбери наиболее вероятную и поставь needsHumanReview=true.
                            """
                        },
                        new
                        {
                            inlineData = new
                            {
                                mimeType = contentType,
                                data = base64Image
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.0,
                maxOutputTokens = 2400,
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        isCarDamagePhoto = new
                        {
                            type = "boolean"
                        },
                        needsHumanReview = new
                        {
                            type = "boolean"
                        },
                        overallComment = new
                        {
                            type = "string"
                        },
                        damages = new
                        {
                            type = "array",
                            minItems = 1,
                            maxItems = 6,
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    detectedPart = new
                                    {
                                        type = "string",
                                        @enum = new[]
                                        {
                                            "Передний бампер",
                                            "Задний бампер",

                                            "Капот",

                                            "Переднее левое крыло",
                                            "Переднее правое крыло",
                                            "Заднее левое крыло",
                                            "Заднее правое крыло",

                                            "Передняя левая дверь",
                                            "Передняя правая дверь",
                                            "Задняя левая дверь",
                                            "Задняя правая дверь",

                                            "Передняя левая фара",
                                            "Передняя правая фара",
                                            "Задняя левая фара",
                                            "Задняя правая фара",

                                            "Порог",
                                            "Крышка багажника",
                                            "Не определено"
                                        }
                                    },
                                    damageType = new
                                    {
                                        type = "string",
                                        @enum = new[]
                                        {
                                            "Царапина",
                                            "Вмятина",
                                            "Скол",
                                            "Трещина",
                                            "Коррозия",
                                            "Замена детали",
                                            "Не определено"
                                        }
                                    },
                                    repairType = new
                                    {
                                        type = "string",
                                        @enum = new[]
                                        {
                                            "Ремонт",
                                            "Покраска",
                                            "Замена",
                                            "Смешанный",
                                            "Не определено"
                                        }
                                    },
                                    damageLevel = new
                                    {
                                        type = "string",
                                        @enum = new[]
                                        {
                                            "Лёгкое",
                                            "Среднее",
                                            "Сильное",
                                            "Смешанное",
                                            "Не определено"
                                        }
                                    },
                                    confidence = new
                                    {
                                        type = "number"
                                    },
                                    needsHumanReview = new
                                    {
                                        type = "boolean"
                                    },
                                    comment = new
                                    {
                                        type = "string"
                                    }
                                },
                                required = new[]
                                {
                                    "detectedPart",
                                    "damageType",
                                    "repairType",
                                    "damageLevel",
                                    "confidence",
                                    "needsHumanReview",
                                    "comment"
                                }
                            }
                        }
                    },
                    required = new[]
                    {
                        "isCarDamagePhoto",
                        "needsHumanReview",
                        "overallComment",
                        "damages"
                    }
                }
            }
        };
    }

    private static string ExtractGeminiText(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            return "";
        }

        var firstCandidate = candidates[0];

        if (!firstCandidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array ||
            parts.GetArrayLength() == 0)
        {
            return "";
        }

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textElement) &&
                textElement.ValueKind == JsonValueKind.String)
            {
                return textElement.GetString() ?? "";
            }
        }

        return "";
    }

    private static string CleanJsonText(string value)
    {
        var text = value.Trim();

        if (text.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            text = text[7..].Trim();
        else if (text.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            text = text[3..].Trim();

        if (text.EndsWith("```", StringComparison.OrdinalIgnoreCase))
            text = text[..^3].Trim();

        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');

        if (firstBrace >= 0 && lastBrace > firstBrace)
            text = text.Substring(firstBrace, lastBrace - firstBrace + 1);

        return text;
    }

    private static string NormalizeText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static int GetSeverityScore(string damageLevel)
    {
        return damageLevel switch
        {
            "Сильное" => 4,
            "Смешанное" => 3,
            "Среднее" => 2,
            "Лёгкое" => 1,
            _ => 0
        };
    }

    private sealed class GeminiDamageModelResult
    {
        [JsonPropertyName("isCarDamagePhoto")]
        public bool IsCarDamagePhoto { get; set; }

        [JsonPropertyName("needsHumanReview")]
        public bool NeedsHumanReview { get; set; }

        [JsonPropertyName("overallComment")]
        public string OverallComment { get; set; } = "";

        [JsonPropertyName("damages")]
        public List<GeminiDamageModelItem> Damages { get; set; } = new();
    }

    private sealed class GeminiDamageModelItem
    {
        [JsonPropertyName("detectedPart")]
        public string DetectedPart { get; set; } = "";

        [JsonPropertyName("damageType")]
        public string DamageType { get; set; } = "";

        [JsonPropertyName("repairType")]
        public string RepairType { get; set; } = "";

        [JsonPropertyName("damageLevel")]
        public string DamageLevel { get; set; } = "";

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("needsHumanReview")]
        public bool NeedsHumanReview { get; set; }

        [JsonPropertyName("comment")]
        public string Comment { get; set; } = "";
    }
}

internal static class GeminiAiDamageCostExtensions
{
    public static AiDamageItem WithCalculatedCost(this AiDamageItem result)
    {
        var basePrice = GetBasePrice(result.DetectedPart, result.RepairType);
        var multiplier = GetDamageMultiplier(result.DamageLevel);

        result.EstimatedCost = Math.Round(basePrice * multiplier, 0);
        return result;
    }

    private static decimal GetBasePrice(string partName, string repairType)
    {
        var normalizedPart = NormalizePricePartName(partName);

        return repairType switch
        {
            "Ремонт" => normalizedPart switch
            {
                "Передний бампер" => 8000,
                "Задний бампер" => 8000,
                "Капот" => 12000,
                "Дверь" => 10000,
                "Крыло" => 9000,
                "Фара" => 5000,
                "Крышка багажника" => 11000,
                "Порог" => 9000,
                _ => 0
            },
            "Покраска" => normalizedPart switch
            {
                "Передний бампер" => 10000,
                "Задний бампер" => 10000,
                "Капот" => 15000,
                "Дверь" => 12000,
                "Крыло" => 11000,
                "Крышка багажника" => 14000,
                "Порог" => 10000,
                _ => 0
            },
            "Замена" => normalizedPart switch
            {
                "Передний бампер" => 18000,
                "Задний бампер" => 18000,
                "Капот" => 30000,
                "Дверь" => 25000,
                "Крыло" => 20000,
                "Фара" => 15000,
                "Крышка багажника" => 28000,
                "Порог" => 22000,
                _ => 0
            },
            "Смешанный" => normalizedPart switch
            {
                "Передний бампер" => 14000,
                "Задний бампер" => 14000,
                "Капот" => 22000,
                "Дверь" => 18000,
                "Крыло" => 15000,
                "Фара" => 15000,
                "Крышка багажника" => 21000,
                "Порог" => 16000,
                _ => 0
            },
            _ => 0
        };
    }

    private static string NormalizePricePartName(string partName)
    {
        if (partName.Contains("двер", StringComparison.OrdinalIgnoreCase))
            return "Дверь";

        if (partName.Contains("крыл", StringComparison.OrdinalIgnoreCase))
            return "Крыло";

        if (partName.Contains("фара", StringComparison.OrdinalIgnoreCase))
            return "Фара";

        return partName;
    }

    private static decimal GetDamageMultiplier(string damageLevel)
    {
        return damageLevel switch
        {
            "Лёгкое" => 1.0m,
            "Среднее" => 1.5m,
            "Сильное" => 2.2m,
            "Смешанное" => 1.6m,
            _ => 1.0m
        };
    }
}

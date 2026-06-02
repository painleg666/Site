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

    private const string DefaultOllamaEndpoint = "http://localhost:11434/api/generate";

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
        if (imageBytes.Length == 0)
            throw new InvalidOperationException("Фото пустое.");

        if (string.IsNullOrWhiteSpace(contentType))
            contentType = "image/jpeg";

        Exception? geminiError = null;

        var apiKey = _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var geminiResult = await AnalyzeWithGeminiAsync(
                    apiKey,
                    imageBytes,
                    contentType,
                    cancellationToken);

                return BuildAiDamageResult(
                    geminiResult,
                    provider: "Gemini",
                    isFallback: false);
            }
            catch (Exception ex) when (ShouldFallbackToOllama(ex))
            {
                geminiError = ex;
            }
        }
        else
        {
            geminiError = new InvalidOperationException(
                "Gemini API ключ не найден. Будет использована локальная модель Ollama.");
        }

        try
        {
            var ollamaResult = await AnalyzeWithOllamaAsync(
                imageBytes,
                contentType,
                cancellationToken);

            return BuildAiDamageResult(
                ollamaResult,
                provider: "Ollama",
                isFallback: true);
        }
        catch (Exception ollamaEx) when (ollamaEx is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "Не удалось выполнить AI-анализ. " +
                $"Gemini: {geminiError?.Message ?? "не вызывался"}. " +
                $"Ollama: {ollamaEx.Message}",
                ollamaEx);
        }
    }

    private async Task<GeminiDamageModelResult> AnalyzeWithGeminiAsync(
        string apiKey,
        byte[] imageBytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        var model = _configuration["Gemini:Model"];

        if (string.IsNullOrWhiteSpace(model))
            model = "gemini-2.0-flash";

        var base64 = Convert.ToBase64String(imageBytes);

        var requestBody = BuildGeminiRequestBody(base64, contentType);
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

        try
        {
            var modelResult = JsonSerializer.Deserialize<GeminiDamageModelResult>(outputText, JsonOptions);
            return modelResult ?? throw new InvalidOperationException("Gemini вернул пустой результат.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Gemini вернул неполный или невалидный JSON. " +
                "Чаще всего это происходит из-за обрезанного ответа. " +
                $"Ответ модели: {outputText}",
                ex);
        }
    }

    private async Task<GeminiDamageModelResult> AnalyzeWithOllamaAsync(
        byte[] imageBytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        var model = _configuration["Ollama:Model"];

        if (string.IsNullOrWhiteSpace(model))
            model = "gemma3:latest";

        var endpoint = _configuration["Ollama:Endpoint"];

        if (string.IsNullOrWhiteSpace(endpoint))
            endpoint = DefaultOllamaEndpoint;

        var base64 = Convert.ToBase64String(imageBytes);

        var requestBody = new
        {
            model,
            prompt = BuildOllamaPrompt(),
            images = new[] { base64 },
            stream = false,
            format = BuildOllamaJsonSchema()
        };

        var requestJson = JsonSerializer.Serialize(requestBody);

        using var response = await _httpClient.PostAsync(
            endpoint,
            new StringContent(requestJson, Encoding.UTF8, "application/json"),
            cancellationToken);

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Ollama API вернул ошибку {(int)response.StatusCode}: {responseText}");
        }

        var outputText = ExtractOllamaText(responseText);

        if (string.IsNullOrWhiteSpace(outputText))
            throw new InvalidOperationException("Ollama не вернула JSON-результат.");

        outputText = CleanJsonText(outputText);

        try
        {
            var modelResult = JsonSerializer.Deserialize<GeminiDamageModelResult>(outputText, JsonOptions);
            return modelResult ?? throw new InvalidOperationException("Ollama вернула пустой результат.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Ollama вернула неполный или невалидный JSON. " +
                $"Ответ модели: {outputText}",
                ex);
        }
    }

    private static AiDamageResult BuildAiDamageResult(
        GeminiDamageModelResult modelResult,
        string provider,
        bool isFallback)
    {
        var items = (modelResult.Damages ?? new List<GeminiDamageModelItem>())
            .Where(x => !string.IsNullOrWhiteSpace(x.DetectedPart))
            .Where(x => !string.Equals(
                x.DetectedPart.Trim(),
                "Не определено",
                StringComparison.OrdinalIgnoreCase))
            .Where(x =>
                !string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase)
                || Math.Clamp(x.Confidence, 0, 1) >= 0.35)
            .Select(x =>
            {
                var damageType = NormalizeText(x.DamageType, "Не определено");
                var damageLevel = NormalizeText(x.DamageLevel, "Не определено");
                var repairType = NormalizeRepairType(
                    x.RepairType,
                    damageType,
                    damageLevel);

                return new AiDamageItem
                {
                    DetectedPart = NormalizeText(x.DetectedPart, "Не определено"),
                    DamageType = damageType,
                    RepairType = repairType,
                    DamageLevel = damageLevel,
                    Confidence = Math.Clamp(x.Confidence, 0, 1),
                    NeedsHumanReview = x.NeedsHumanReview,
                    Comment = string.IsNullOrWhiteSpace(x.Comment)
                        ? "Комментарий по позиции не указан."
                        : x.Comment.Trim()
                };
            })
            .Select(x => x.WithCalculatedCost())
            .GroupBy(x => x.DetectedPart)
            .Select(g => g
                .OrderByDescending(x => GetSeverityScore(x.DamageLevel))
                .ThenByDescending(x => x.Confidence)
                .ThenByDescending(x => x.EstimatedCost)
                .First())
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
                Comment = $"{provider} не смог уверенно определить повреждённые зоны."
            });
        }

        var primary = items
            .OrderByDescending(x => GetSeverityScore(x.DamageLevel))
            .ThenByDescending(x => x.EstimatedCost)
            .ThenByDescending(x => x.Confidence)
            .First();

        var totalCost = items.Sum(x => x.EstimatedCost);
        var averageConfidence = items.Count == 0 ? 0 : items.Average(x => x.Confidence);

        var comment = string.IsNullOrWhiteSpace(modelResult.OverallComment)
            ? $"{provider} вернул несколько повреждённых зон. Результат требует проверки оценщиком."
            : modelResult.OverallComment.Trim();

        if (isFallback)
            comment = "Gemini недоступен, использована локальная модель Ollama. " + comment;

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
            Comment = comment,
            Items = items
        };
    }

    private static object BuildGeminiRequestBody(string base64Image, string contentType)
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
                            text = BuildGeminiPrompt()
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
                responseSchema = BuildGeminiJsonSchema()
            }
        };
    }

    private static string BuildGeminiPrompt()
    {
        return """
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
        Если вид сбоку, обязательно учитывай заднюю и переднюю часть авто одновременно.
        """;
    }

    private static string BuildOllamaPrompt()
    {
        return """
    Проанализируй фото автомобиля после ДТП.

    Верни список всех явно видимых повреждённых зон автомобиля.
    Не выдумывай скрытые повреждения.
    Не добавляй детали, которых не видно на фото.
    Не дублируй одну и ту же деталь несколько раз.
    Если повреждение небольшое, лучше вернуть 1-3 реально видимые повреждённые детали.
    Если ДТП сильное и явно повреждено несколько передних элементов, верни все явно повреждённые зоны: бампер, фара, капот, крыло, дверь, если дефект на них виден.
    Не ограничивайся одной деталью при сильном фронтальном ударе.

    Добавляй деталь только если на ней явно виден отдельный дефект:
    царапина, вмятина, трещина, скол или деформация.
    Блик, грязь, тень, отражение, зазор между деталями или край детали не считаются повреждением.
        ПРАВИЛО ДЛЯ СИЛЬНОГО ФРОНТАЛЬНОГО УДАРА:
    Если передняя часть автомобиля сильно разбита, видны внутренние элементы, крепления, радиаторная зона или отсутствует наружная пластиковая часть, обязательно добавь "Передний бампер".

    Если передний бампер отсутствует, сорван, разрушен или вместо него видны внутренние элементы автомобиля, укажи:
    detectedPart="Передний бампер",
    damageType="Замена детали",
    repairType="Замена",
    damageLevel="Сильное".

    Если фара разбита, отсутствует, смещена или вместо неё видна пустая/тёмная зона, добавь соответствующую фару:
    "Передняя левая фара" или "Передняя правая фара",
    damageType="Замена детали",
    repairType="Замена",
    damageLevel="Сильное".

    Если на капоте видны заломы, складки, сильная вмятина или деформация передней кромки, добавь "Капот".
    Для капота при сильных заломах используй:
    damageType="Вмятина",
    repairType="Смешанный",
    damageLevel="Сильное".

    Если крыло деформировано, смято, разорвано или нарушен зазор с капотом/фарой/дверью, добавь соответствующее переднее крыло.
    При сильной деформации крыла используй:
    repairType="Смешанный",
    damageLevel="Сильное".
    ОПРЕДЕЛЕНИЕ ЛЕВОЙ И ПРАВОЙ СТОРОНЫ:
    Левая и правая сторона определяются относительно автомобиля, как если бы водитель сидел внутри автомобиля и смотрел вперёд.

    Если передняя часть автомобиля, фара или капот находятся справа на изображении,
    то на фото видна ПРАВАЯ сторона автомобиля.
    В этом случае нужно выбирать:
    "Переднее правое крыло",
    "Передняя правая дверь",
    "Передняя правая фара",
    "Задняя правая дверь",
    "Заднее правое крыло".

    Если передняя часть автомобиля, фара или капот находятся слева на изображении,
    то на фото видна ЛЕВАЯ сторона автомобиля.
    В этом случае нужно выбирать:
    "Переднее левое крыло",
    "Передняя левая дверь",
    "Передняя левая фара",
    "Задняя левая дверь",
    "Заднее левое крыло".

    Если на фото вид сбоку и передняя часть автомобиля находится справа в кадре,
    НЕ выбирай левую сторону для переднего крыла.

    ВАЖНО ДЛЯ ФОТО С КОЛЁСНОЙ АРКОЙ:
    Если повреждение расположено вокруг колеса, над колесом или на колёсной арке,
    это почти всегда крыло, а не бампер.

    Если видны вмятина, заломы или царапины вокруг переднего колеса,
    обязательно проверь варианты:
    "Переднее правое крыло" или "Переднее левое крыло"
    в зависимости от стороны автомобиля.

    Не называй повреждение "Передний бампер",
    если оно находится над колесом или вокруг арки колеса.

    Передний бампер указывай только если дефект находится
    на пластиковой передней части автомобиля,
    рядом с фарой или ниже линии крыла.

    ОСОБОЕ ПРАВИЛО ДЛЯ ПЕРЕДНЕГО БАМПЕРА:
    Если рядом с передним крылом видна отдельная царапина,
    потёртость или белая линия на пластиковой части переднего бампера,
    добавь "Передний бампер" отдельной позицией.

    Если повреждение слабое, но явно видимое,
    всё равно добавь его в damages с confidence от 0.35 до 0.6
    и needsHumanReview=true.

    Для каждой позиции обязательно выбери repairType из списка:
    "Ремонт", "Покраска", "Замена", "Смешанный", "Не определено".

    Не оставляй repairType="Не определено",
    если damageType уже определён.

    Не оценивай финальную стоимость.
    Система рассчитает цену отдельно.

    Используй только допустимые значения из схемы.
    Верни строго JSON по схеме.
    Никакого Markdown.
    Никакого текста до или после JSON.
    """;
    }

    private static object BuildGeminiJsonSchema()
    {
        return new
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
                    items = BuildDamageItemSchema()
                }
            },
            required = new[]
            {
                "isCarDamagePhoto",
                "needsHumanReview",
                "overallComment",
                "damages"
            }
        };
    }

    private static object BuildOllamaJsonSchema()
    {
        return new
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
                    items = BuildDamageItemSchema()
                }
            },
            required = new[]
            {
                "isCarDamagePhoto",
                "needsHumanReview",
                "overallComment",
                "damages"
            }
        };
    }

    private static object BuildDamageItemSchema()
    {
        return new
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

    private static string ExtractOllamaText(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        if (root.TryGetProperty("response", out var responseElement) &&
            responseElement.ValueKind == JsonValueKind.String)
        {
            return responseElement.GetString() ?? "";
        }

        if (root.TryGetProperty("error", out var errorElement) &&
            errorElement.ValueKind == JsonValueKind.String)
        {
            throw new InvalidOperationException(errorElement.GetString() ?? "Ollama вернула неизвестную ошибку.");
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

    private static string NormalizeRepairType(
        string repairType,
        string damageType,
        string damageLevel)
    {
        if (!string.IsNullOrWhiteSpace(repairType))
        {
            var normalizedRepairType = repairType.Trim();

            if (string.Equals(normalizedRepairType, "Ремонт", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedRepairType, "Покраска", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedRepairType, "Замена", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedRepairType, "Смешанный", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedRepairType;
            }
        }

        if (string.Equals(damageType, "Царапина", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(damageType, "Скол", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(damageType, "Коррозия", StringComparison.OrdinalIgnoreCase))
        {
            return "Покраска";
        }

        if (string.Equals(damageType, "Трещина", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(damageType, "Замена детали", StringComparison.OrdinalIgnoreCase))
        {
            return "Замена";
        }

        if (string.Equals(damageType, "Вмятина", StringComparison.OrdinalIgnoreCase))
        {
            return damageLevel switch
            {
                "Сильное" => "Смешанный",
                "Среднее" => "Ремонт",
                "Лёгкое" => "Ремонт",
                _ => "Ремонт"
            };
        }

        return "Не определено";
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

    private static bool ShouldFallbackToOllama(Exception ex)
    {
        return ex is not OperationCanceledException;
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

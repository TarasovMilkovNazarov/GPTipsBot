using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using GPTipsBot.Config;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Services.YandexPhotoAnimator;

public class YaPhotoAnimatorService
{
    private static HttpClient _httpClient;
    private static readonly CookieContainer _cookieContainer = new();
    private const int LoggedBodyLimit = 4000;
    const string UploadImageUrl = "https://masterpiecer.yandex.ru/yaart-web-alice-api/api/v1/wow/upload_image";
    private const string GenerateImageUrl = "https://rpc.alice.yandex.ru/gproxy/draw_picture_video_generate";
    private const string WaitForResultUrl = "https://rpc.alice.yandex.ru/gproxy/draw_picture_video_get";
    private const string EditImageUrl = "https://rpc.alice.yandex.ru/gproxy/draw_picture_editing_generate";
    private const string GetEditingUrl = "https://rpc.alice.yandex.ru/gproxy/draw_picture_editing_get";
    private const string CombineImagesUrl = "https://rpc.alice.yandex.ru/gproxy/draw_picture_combining_generate";
    private const string GetCombiningUrl = "https://rpc.alice.yandex.ru/gproxy/draw_picture_combining_get";
    private const string EditingGenerationProperty = "editingGeneration";
    private const string CombiningGenerationProperty = "imageCombiningGeneration";
    private const string AliceUuid = "5e95f47b-3fde-49fa-9439-f47d3B368C51";
    private const string AliceReferer = "https://alice.yandex.ru/";
    private const string AliceSupportedFeatures =
        "background_response_streaming_for_dialog_controls,background_response_streaming_in_read_dialog,background_response_streaming_anon,background_response_streaming,supports_bso_answer,open_link,server_action,show_promo,reminders_and_todos,div2_cards,player_pause_directive,can_open_dialogs_in_tabs,supports_streaming_response,supports_rich_json_cards,builtin_reaction,open_link_by_button,supports_origin_in_separate_card,supports_new_sources_cards,supports_markdown_response,supported_save_chathistory,supported_load_chathistory,supports_unlimited_dialogs_creation,supports_multi_model_dialogs,print_text_in_message_view,show_loader_directive,supports_stringbody_in_div2_card,supports_default_dialog_as_dedicated,whisper";
    private const string AliceImageExperiments =
        "[\"read_dialogs_for_unauthorized_users\",\"mm_allow_anonymous_request\",\"dont_skip_cancel_requests\",\"enable_parallel_requests_to_chats\",\"enable_external_skills_for_webdesktop_and_webtouch\",\"send_show_view_directive_on_supports_show_view_layer_content_interface\",\"standalone_alice_2_0\",\"mm_enable_protocol_scenario=WebAliceControls\",\"exp_flag_chat_dialog_history\",\"exp_flag_chat_dialog_history_main_context_save\",\"div2cards_in_external_skills_for_web_standalone\",\"enable_find_poi_standalone\",\"use_server_pings\",\"enable_onboarding_adaptive_size\",\"standalone_show_fullscreen_image_gallery_directive\",\"draw_picture_enable_controls\",\"alice_has_borders_div_paddings\",\"enable_new_colors_for_alice_chat\",\"erase_serialized_response_from_json_deferred_alice_response\",\"skills_standalone_use_div_render\",\"standalone_skill_card_cloud_ui\"]";
    private const string AliceVideoExperiments =
        "[\"dont_skip_cancel_requests\",\"enable_parallel_requests_to_chats\",\"read_dialogs_for_unauthorized_users\",\"mm_allow_anonymous_request\",\"enable_external_skills_for_webdesktop_and_webtouch\",\"send_show_view_directive_on_supports_show_view_layer_content_interface\",\"standalone_alice_2_0\",\"mm_enable_protocol_scenario=WebAliceControls\",\"exp_flag_chat_dialog_history\",\"exp_flag_chat_dialog_history_main_context_save\",\"div2cards_in_external_skills_for_web_standalone\",\"enable_find_poi_standalone\",\"use_server_pings\",\"enable_onboarding_adaptive_size\",\"standalone_show_fullscreen_image_gallery_directive\",\"draw_picture_enable_controls\",\"alice_has_borders_div_paddings\",\"enable_new_colors_for_alice_chat\",\"erase_serialized_response_from_json_deferred_alice_response\",\"skills_standalone_use_div_render\",\"standalone_skill_card_cloud_ui\",\"alice_enable_generate_video\",\"aliceapp_enable_generate_video\",\"alice_video_generation_soon\",\"new_input_bts\"]";
    private static readonly JsonSerializerOptions AliceJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly ILogger<YaPhotoAnimatorService> _logger;

    public YaPhotoAnimatorService(ILogger<YaPhotoAnimatorService> logger)
    {
        _logger = logger;
        ConfigureHttpClient();
    }

    public async Task<string> UploadImageFromBase64(string base64Image, string fileName = "image.jpg")
    {
        try
        {
            // Удаляем префикс data:image/...;base64, если он есть
            var base64Data = base64Image;
            if (base64Image.Contains(","))
            {
                base64Data = base64Image.Substring(base64Image.IndexOf(",") + 1);
            }

            // Конвертируем Base64 строку в массив байтов
            byte[] imageBytes = Convert.FromBase64String(base64Data);

            // Определяем Content-Type на основе первых байтов или расширения файла
            var contentType = GetImageContentType(imageBytes, fileName);

            using var formData = new MultipartFormDataContent();

            // Создаем контент из байтов
            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

            // Добавляем файл в form-data
            formData.Add(fileContent, "content", fileName);

            using var request = new HttpRequestMessage(HttpMethod.Post, UploadImageUrl);
            request.Content = formData;

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<UploadImageResponse>(json);

            return result?.UploadedImageInfo?.ImageUrl ??
                   throw new Exception("Не удалось получить URL загруженного изображения");
        }
        catch (FormatException ex)
        {
            throw new Exception("Некорректный формат Base64 строки", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка при загрузке изображения: {ex.Message}", ex);
        }
    }

    private string GetImageContentType(byte[] imageBytes, string fileName)
    {
        // Определяем по сигнатуре файла (магическим байтам)
        if (imageBytes.Length > 2)
        {
            // JPEG
            if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
                return "image/jpeg";

            // PNG
            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                return "image/png";

            // GIF
            if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46)
                return "image/gif";

            // BMP
            if (imageBytes[0] == 0x42 && imageBytes[1] == 0x4D)
                return "image/bmp";

            // WebP
            if (imageBytes.Length > 12 &&
                imageBytes[0] == 0x52 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46 && imageBytes[3] == 0x46 &&
                imageBytes[8] == 0x57 && imageBytes[9] == 0x45 && imageBytes[10] == 0x42 && imageBytes[11] == 0x50)
                return "image/webp";
        }

        // Если не удалось определить по сигнатуре, используем расширение файла
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "image/jpeg" // По умолчанию
        };
    }

    public async Task<VideoGenerationResponse?> GenerateVideo(string imageUrl, string prompt)
    {
        try
        {
            // Создаем запрос
            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://rpc.alice.yandex.ru/gproxy/draw_picture_video_generate");

            var body = new GenerateRequest
            {
                Prompt = prompt,
                Url = imageUrl
            };

            var (_, content) = CreateAliceJsonContent(body);
            request.Content = content;
            ApplyAliceRpcExperiments(request, includeVideoExperiments: true);

            // Отправляем запрос
            var response = await _httpClient.SendAsync(request);

            // Читаем ответ
            var responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Status Code: {response.StatusCode}");
            Console.WriteLine($"Response: {responseBody}");

            // Проверяем успешность
            VideoGenerationResponse? videoResponse = null;
            if (response.IsSuccessStatusCode)
            {
                // Обрабатываем JSON ответ
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                videoResponse = JsonSerializer.Deserialize<VideoGenerationResponse>(responseBody, options);

                if (videoResponse?.VideoGeneration != null)
                {
                    var videoData = videoResponse.VideoGeneration;
                    Console.WriteLine($"Video Generation Status: {videoData.Status}");
                    Console.WriteLine($"Video ID: {videoData.Id}");
                    Console.WriteLine($"Remaining Time: {videoData.RemainingTimeSec} seconds");
                }
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
            }

            return videoResponse;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task<VideoGenerationResponse> GetGenerationStatus(string generationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, WaitForResultUrl);

        var getRequest = new GetVideoRequest
        {
            GenerationId = generationId
        };

        var (_, content) = CreateAliceJsonContent(getRequest);
        request.Content = content;
        ApplyAliceRpcExperiments(request, includeVideoExperiments: true);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<VideoGenerationResponse>(responseJson, options)
               ?? throw new Exception("Не удалось получить статус генерации видео");
    }

    public async Task<VideoGenerationResponse> WaitForResult(string generationId)
    {
        const int maxAttempts = 10;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var result = await GetGenerationStatus(generationId);

            if (!string.IsNullOrEmpty(result.VideoGeneration.VideoURL))
            {
                return result;
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(result.VideoGeneration.RemainingTimeSec, 5)));
        }

        throw new Exception("Превышено время ожидания генерации видео");
    }

    public async Task<AliceImageGenerationResult?> EditImage(string imageUrl, string prompt)
    {
        var attempt = await StartImageGeneration(
            EditImageUrl,
            new EditImageGenerateRequest
            {
                ImageCount = 1,
                Prompt = prompt,
                Url = imageUrl,
                IsTemplate = "0"
            },
            EditingGenerationProperty);
        return attempt.Result;
    }

    public async Task<AliceImageGenerationResult?> CombineImages(string firstImageUrl, string secondImageUrl, string prompt)
    {
        var attempt = await StartImageGeneration(
            CombineImagesUrl,
            new CombineImagesGenerateRequest
            {
                Prompt = prompt,
                Urls = new[] { firstImageUrl, secondImageUrl },
                ImageCount = 1,
                Edit = false,
                IsTemplate = "0"
            },
            CombiningGenerationProperty);
        return attempt.Result;
    }

    public Task<AliceImageGenerationResult> GetEditingStatus(string generationId)
        => GetImageGenerationStatus(GetEditingUrl, generationId, EditingGenerationProperty);

    public Task<AliceImageGenerationResult> GetCombiningStatus(string generationId)
        => GetImageGenerationStatus(GetCombiningUrl, generationId, CombiningGenerationProperty);

    private async Task<AliceGenerateAttempt> StartImageGeneration(
        string url,
        object body,
        string generationProperty)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        var (jsonBody, content) = CreateAliceJsonContent(body);
        request.Content = content;
        ApplyAliceRpcExperiments(request, includeVideoExperiments: false);

        _logger.LogInformation(
            "Alice generate request {Url} property {GenerationProperty} body {RequestBody}",
            url,
            generationProperty,
            Truncate(jsonBody));

        HttpStatusCode statusCode;
        string responseBody;
        try
        {
            using var response = await _httpClient.SendAsync(request);
            statusCode = response.StatusCode;
            responseBody = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alice generate request failed {Url}", url);
            throw;
        }

        _logger.LogInformation(
            "Alice generate response {Url} status {StatusCode} ({Status}) keys [{JsonKeys}] body {ResponseBody}",
            url,
            (int)statusCode,
            statusCode,
            DescribeJsonRoot(responseBody),
            Truncate(responseBody));

        if (!IsSuccessStatusCode(statusCode))
        {
            _logger.LogWarning(
                "Alice generate HTTP {StatusCode} for {Url}. Expected property {GenerationProperty}",
                (int)statusCode,
                url,
                generationProperty);
            return new AliceGenerateAttempt(statusCode, responseBody, null);
        }

        var parsed = AliceImageGenerationResult.FromJson(responseBody, generationProperty);
        if (parsed == null)
        {
            _logger.LogWarning(
                "Alice generate JSON has no '{GenerationProperty}'. Root keys: [{JsonKeys}]",
                generationProperty,
                DescribeJsonRoot(responseBody));
            return new AliceGenerateAttempt(statusCode, responseBody, null);
        }

        if (string.IsNullOrEmpty(parsed.Id))
        {
            _logger.LogWarning(
                "Alice generate parsed without id. Status {Status}, remaining {RemainingTimeSec}s, imageUrl set={HasImageUrl}",
                parsed.Status,
                parsed.RemainingTimeSec,
                !string.IsNullOrEmpty(parsed.ImageUrl));
        }
        else
        {
            _logger.LogInformation(
                "Alice generate started id {GenerationId} status {Status} remaining {RemainingTimeSec}s",
                parsed.Id,
                parsed.Status,
                parsed.RemainingTimeSec);
        }

        return new AliceGenerateAttempt(statusCode, responseBody, parsed);
    }

    private readonly record struct AliceGenerateAttempt(
        HttpStatusCode StatusCode,
        string ResponseBody,
        AliceImageGenerationResult? Result);

    private async Task<AliceImageGenerationResult> GetImageGenerationStatus(
        string url,
        string generationId,
        string generationProperty)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        var (json, content) = CreateAliceJsonContent(new GetVideoRequest { GenerationId = generationId });
        request.Content = content;
        ApplyAliceRpcExperiments(request, includeVideoExperiments: false);

        _logger.LogInformation(
            "Alice status request {Url} generationId {GenerationId} body {RequestBody}",
            url,
            generationId,
            json);

        var response = await _httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();
        _logger.LogInformation(
            "Alice status response {Url} generationId {GenerationId} status {StatusCode} keys [{JsonKeys}] body {ResponseBody}",
            url,
            generationId,
            (int)response.StatusCode,
            DescribeJsonRoot(responseJson),
            Truncate(responseJson));

        response.EnsureSuccessStatusCode();

        return AliceImageGenerationResult.FromJson(responseJson, generationProperty)
               ?? throw new Exception("Не удалось получить статус генерации изображения");
    }

    private static bool IsSuccessStatusCode(HttpStatusCode statusCode)
        => (int)statusCode is >= 200 and <= 299;

    private static (string Json, StringContent Content) CreateAliceJsonContent(object body)
    {
        var json = JsonSerializer.Serialize(body, body.GetType(), AliceJsonOptions);
        var content = new StringContent(json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return (json, content);
    }

    private static void ApplyAliceRpcExperiments(HttpRequestMessage request, bool includeVideoExperiments)
    {
        request.Headers.TryAddWithoutValidation(
            "x-ya-experiments",
            includeVideoExperiments ? AliceVideoExperiments : AliceImageExperiments);
    }

    private static string Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "<empty>";
        }

        return text.Length <= LoggedBodyLimit
            ? text
            : text[..LoggedBodyLimit] + $"...(+{text.Length - LoggedBodyLimit} chars)";
    }

    private static string DescribeJsonRoot(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return doc.RootElement.ValueKind.ToString();
            }

            return string.Join(", ", doc.RootElement.EnumerateObject().Select(p => p.Name));
        }
        catch (Exception ex)
        {
            return $"invalid-json: {ex.Message}";
        }
    }

    private static void ConfigureHttpClient()
    {
        var cookies = YandexAliceConfig.GetAllCookies();

        foreach (var cookieStr in cookies)
        {
            var parts = cookieStr.Split('=', 2);
            if (parts.Length == 2)
            {
                var cookie = new Cookie(parts[0], parts[1]);
                cookie.Domain = ".yandex.ru";
                _cookieContainer.Add(cookie);
            }
        }

        var handler = new HttpClientHandler
        {
            CookieContainer = _cookieContainer,
            UseCookies = true
        };

        _httpClient = new HttpClient(handler);

        _httpClient.DefaultRequestHeaders.Add("accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua", "\"Chromium\";v=\"142\", \"YaBrowser\";v=\"25.12\", \"Not_A Brand\";v=\"99\", \"Yowser\";v=\"2.5\"");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-arch", "\"x86\"");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-bitness", "\"64\"");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-full-version-list", "\"Chromium\";v=\"142.0.7444.693\", \"YaBrowser\";v=\"25.12.5.693\", \"Not_A Brand\";v=\"99.0.0.0\", \"Yowser\";v=\"2.5\"");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-platform", "\"Windows\"");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-platform-version", "\"10.0.0\"");
        _httpClient.DefaultRequestHeaders.Add("sec-ch-ua-wow64", "?0");
        _httpClient.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
        _httpClient.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
        _httpClient.DefaultRequestHeaders.Add("sec-fetch-site", "same-site");
        _httpClient.DefaultRequestHeaders.Add("x-ya-app-id", "ru.yandex.webstandalone.desktop");
        _httpClient.DefaultRequestHeaders.Add("x-ya-application", $"{{\"app_id\":\"ru.yandex.webstandalone.desktop\",\"uuid\":\"{AliceUuid}\",\"device_id\":\"{AliceUuid}\",\"lang\":\"ru\",\"timezone\":\"UTC\"}}");
        _httpClient.DefaultRequestHeaders.Add("x-ya-device-id", AliceUuid);
        _httpClient.DefaultRequestHeaders.Add("x-ya-device-model", "");
        _httpClient.DefaultRequestHeaders.Add("x-ya-language", "ru");
        _httpClient.DefaultRequestHeaders.Add("x-ya-platform", "");
        _httpClient.DefaultRequestHeaders.Add("x-ya-supported-features", AliceSupportedFeatures);
        _httpClient.DefaultRequestHeaders.Add("x-ya-test-ids", "");
        _httpClient.DefaultRequestHeaders.Add("x-ya-uuid", AliceUuid);
        _httpClient.DefaultRequestHeaders.Add("y-browser-experiments", "MTUwNDgwMSwwLC0xOzE0OTcxMzcsMCw4NDsxMjc0OTA4LDAsLTE=");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Referer", AliceReferer);
    }
}

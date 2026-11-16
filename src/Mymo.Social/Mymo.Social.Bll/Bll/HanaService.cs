using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mymo.Shared;

namespace Mymo.Social.Bll;

public sealed class HanaService(HttpClient http, IConfiguration config, ILogger<HanaService> logger)
{
    private const string DefaultModel = "gemini-3.1-flash-lite";
    private const string Instructions = """
        Bạn là Hana, trợ lý của ứng dụng MyMo. Hãy trả lời ngắn gọn bằng ngôn ngữ người dùng sử dụng.
        Bạn có thể gợi ý cách khám phá địa điểm và sử dụng MyMo. Bạn không có dữ liệu trực tiếp từ bản đồ,
        vị trí bạn bè hay tài khoản của người dùng. Khi cần dữ liệu đó, hãy hướng dẫn họ xem trong MyMo.
        """;

    public async Task<ServiceResult> ReplyAsync(string? message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Reply("Mình là Hana. Bạn muốn tìm quán hay xem bạn bè đang ở đâu?");

        var key = config["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            return Error(StatusCodes.Status503ServiceUnavailable, "Hana chưa được cấu hình.");

        var model = config["Gemini:Model"]?.Trim();
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/interactions")
        {
            Content = JsonContent.Create(new
            {
                model = string.IsNullOrEmpty(model) ? DefaultModel : model,
                input = message,
                system_instruction = Instructions,
                store = false,
                generation_config = new { max_output_tokens = 512 }
            })
        };
        request.Headers.Add("x-goog-api-key", key.Trim());

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Gemini returned HTTP {StatusCode} for Hana", (int)response.StatusCode);
                return response.StatusCode == HttpStatusCode.TooManyRequests
                    ? Error(StatusCodes.Status429TooManyRequests, "Hana đã đạt giới hạn yêu cầu. Bạn thử lại sau nhé.")
                    : Error(StatusCodes.Status502BadGateway, "Hana chưa kết nối được với dịch vụ AI. Bạn thử lại sau nhé.");
            }

            var interaction = await response.Content.ReadFromJsonAsync<Interaction>(cancellationToken);
            var textParts = (interaction?.Steps ?? [])
                .Where(step => step.Type == "model_output")
                .SelectMany(step => step.Content ?? [])
                .Where(part => part.Type == "text" && !string.IsNullOrWhiteSpace(part.Text))
                .Select(part => part.Text);
            var reply = string.Join("\n", textParts).Trim();
            return reply.Length > 0
                ? Reply(reply)
                : Error(StatusCodes.Status502BadGateway, "Hana chưa nhận được câu trả lời. Bạn thử diễn đạt lại nhé.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Error(StatusCodes.Status504GatewayTimeout, "Hana trả lời quá lâu. Bạn thử lại nhé.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            logger.LogWarning("Gemini request for Hana failed with {ErrorType}", ex.GetType().Name);
            return Error(StatusCodes.Status502BadGateway, "Hana chưa kết nối được với dịch vụ AI. Bạn thử lại sau nhé.");
        }
    }

    private static ServiceResult Reply(string reply) => ServiceResult.Ok(ApiResponse<object>.Ok(new { reply }));

    private static ServiceResult Error(int status, string message) =>
        new() { Status = status, Body = ApiResponse<object>.Fail(message) };

    private sealed record Interaction(Step[]? Steps);
    private sealed record Step(string? Type, Content[]? Content);
    private sealed record Content(string? Type, string? Text);
}

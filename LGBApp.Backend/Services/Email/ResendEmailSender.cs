using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LGBApp.Backend.Services.Email;

public sealed class ResendEmailSender : IEmailSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(HttpClient http, IConfiguration config, ILogger<ResendEmailSender> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string textBody,
        string? htmlBody = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _config["Email:ResendApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Email:ResendApiKey is not configured.");

        var from = _config["Email:From"];
        if (string.IsNullOrWhiteSpace(from))
            from = "LGB Services <onboarding@resend.dev>";

        var payload = new Dictionary<string, object?>
        {
            ["from"] = from,
            ["to"] = new[] { toEmail },
            ["subject"] = subject,
            ["text"] = textBody,
        };
        if (!string.IsNullOrWhiteSpace(htmlBody))
            payload["html"] = htmlBody;

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Resend failed {Status}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Failed to send email ({(int)response.StatusCode}).");
        }

        _logger.LogInformation("Email sent via Resend to {To} subject={Subject}", toEmail, subject);
    }
}

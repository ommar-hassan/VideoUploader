using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VideoUploader.Services
{
    public interface IOpenAIService
    {
        Task<string> GenerateCaptionAsync(string videoDescription);
    }

    public class OpenAIService : IOpenAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenAIService> _logger;
        private const string GROQ_API_BASE = "https://api.groq.com/openai/v1/chat/completions";

        public OpenAIService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<OpenAIService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> GenerateCaptionAsync(string videoDescription)
        {
            try
            {
                var apiKey = _configuration["Groq:ApiKey"];
                var model = _configuration["Groq:Model"] ?? "llama3-8b-8192";

                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("Groq API key not configured, using fallback");
                    return GenerateFallbackCaption(videoDescription);
                }

                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = "You are a social media expert. Create engaging, short captions (under 150 characters) with relevant emojis and 2-3 hashtags."
                        },
                        new
                        {
                            role = "user",
                            content = $"Generate a catchy caption for this video: {videoDescription}"
                        }
                    },
                    temperature = 0.7,
                    max_tokens = 100,
                    top_p = 1.0,
                    stop = null as string[] // Groq accepts null for stop parameter
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                _logger.LogInformation("Sending request to Groq API with model: {Model}", model);

                var response = await _httpClient.PostAsync(GROQ_API_BASE, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JsonDocument.Parse(responseContent);
                    var caption = jsonResponse.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    _logger.LogInformation("Groq caption generated successfully");
                    return caption?.Trim() ?? GenerateFallbackCaption(videoDescription);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Groq rate limit hit (429): {Content}", responseContent);
                    return GenerateFallbackCaption(videoDescription) + " ⚠️[Rate limit - using fallback]";
                }
                else
                {
                    _logger.LogError("Groq API error: {StatusCode}, Response: {Content}",
                        response.StatusCode, responseContent);
                    return GenerateFallbackCaption(videoDescription);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Groq API exception");
                return GenerateFallbackCaption(videoDescription);
            }
        }

        private string GenerateFallbackCaption(string description)
        {
            if (string.IsNullOrEmpty(description) || description == "an interesting video")
            {
                return "✨ Check out this amazing video! 🎥 #video #content #trending";
            }

            return $"🎬 {description.Trim()} ✨ #video #amazing #content";
        }
    }
}
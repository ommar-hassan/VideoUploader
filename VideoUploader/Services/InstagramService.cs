using System.Text.Json;
using VideoUploader.Models;

namespace VideoUploader.Services
{
    public interface IInstagramService
    {
        Task<VideoPostResult> UploadVideoAsync(string caption, string accessToken, string videoUrl);
    }

    public class InstagramService : IInstagramService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<InstagramService> _logger;

        public InstagramService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<InstagramService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<VideoPostResult> UploadVideoAsync(string caption, string accessToken, string videoUrl)
        {
            try
            {
                // Use configured Instagram Business Account ID
                var igAccountId = _configuration["SocialMedia:InstagramBusinessAccountId"];
                if (string.IsNullOrEmpty(igAccountId))
                {
                    return new VideoPostResult
                    {
                        Platform = "Instagram",
                        Success = false,
                        Message = "Instagram Business Account ID not configured"
                    };
                }

                // Use provided token or fall back to configured token
                var tokenToUse = !string.IsNullOrEmpty(accessToken) 
                    ? accessToken 
                    : _configuration["SocialMedia:AccessToken"];

                if (string.IsNullOrEmpty(tokenToUse))
                {
                    return new VideoPostResult
                    {
                        Platform = "Instagram",
                        Success = false,
                        Message = "Access token not provided. Supply a valid token with instagram_basic and instagram_content_publish scopes."
                    };
                }

                _logger.LogInformation("Creating Instagram media container for IG user {IgId}", igAccountId);

                // Create media container (video)
                // media_type=REELS is common for reels; use VIDEO for feed video if desired
                var createUrl = $"https://graph.facebook.com/v24.0/{igAccountId}/media";
                var createParams = new Dictionary<string, string>
                {
                    ["video_url"] = videoUrl,
                    ["caption"] = caption ?? string.Empty,
                    ["media_type"] = "REELS",
                    ["access_token"] = tokenToUse
                };

                using var createContent = new FormUrlEncodedContent(createParams);
                var createResp = await _httpClient.PostAsync(createUrl, createContent);
                var createRespBody = await createResp.Content.ReadAsStringAsync();

                if (!createResp.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to create IG media container: {Response}", createRespBody);
                    return new VideoPostResult
                    {
                        Platform = "Instagram",
                        Success = false,
                        Message = "Failed to create Instagram media container",
                        ErrorDetails = createRespBody
                    };
                }

                using var createDoc = JsonDocument.Parse(createRespBody);
                if (!createDoc.RootElement.TryGetProperty("id", out var idElem))
                {
                    return new VideoPostResult
                    {
                        Platform = "Instagram",
                        Success = false,
                        Message = "Instagram did not return a creation id",
                        ErrorDetails = createRespBody
                    };
                }

                var containerId = idElem.GetString();

                // Poll the container status until it's finished or timeout
                _logger.LogInformation("Polling container {ContainerId} for processing", containerId);
                var maxAttempts = 20;
                var delayMs = 3000;
                bool ready = false;

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    // Query container status
                    var statusUrl = $"https://graph.facebook.com/v24.0/{containerId}?fields=status_code,ready_for_review,status&access_token={Uri.EscapeDataString(tokenToUse)}";
                    var statusResp = await _httpClient.GetAsync(statusUrl);
                    var statusBody = await statusResp.Content.ReadAsStringAsync();

                    if (!statusResp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Container status check failed (attempt {Attempt}): {Response}", attempt + 1, statusBody);
                        // continue polling in case of transient errors
                    }
                    else
                    {
                        try
                        {
                            using var statusDoc = JsonDocument.Parse(statusBody);
                            var root = statusDoc.RootElement;

                            // Check some potential fields - adapt to whatever the API returns.
                            // Keep robust: look for "status_code" or "status" or "ready_for_review"
                            string? statusValue = null;
                            if (root.TryGetProperty("status_code", out var sc))
                            {
                                statusValue = sc.GetString();
                            }
                            else if (root.TryGetProperty("status", out var s))
                            {
                                // status may be an object or string
                                if (s.ValueKind == JsonValueKind.String)
                                    statusValue = s.GetString();
                                else if (s.ValueKind == JsonValueKind.Object && s.TryGetProperty("status", out var so))
                                    statusValue = so.GetString();
                            }
                            else if (root.TryGetProperty("ready_for_review", out var rfr))
                            {
                                // ready_for_review true indicates done
                                if (rfr.ValueKind == JsonValueKind.True)
                                {
                                    ready = true;
                                    break;
                                }
                            }

                            if (!string.IsNullOrEmpty(statusValue))
                            {
                                // Common finished value is "FINISHED" or "finished" — check case-insensitive
                                if (statusValue.Equals("FINISHED", StringComparison.OrdinalIgnoreCase) ||
                                    statusValue.Equals("finished", StringComparison.OrdinalIgnoreCase) ||
                                    statusValue.Equals("success", StringComparison.OrdinalIgnoreCase))
                                {
                                    ready = true;
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed parsing container status JSON");
                        }
                    }

                    await Task.Delay(delayMs);
                }

                if (!ready)
                {
                    _logger.LogWarning("Container processing timed out for {ContainerId}", containerId);
                    return new VideoPostResult
                    {
                        Platform = "Instagram",
                        Success = false,
                        Message = "Instagram media processing timed out",
                        ErrorDetails = "Processing did not complete within timeout"
                    };
                }

                // Publish the container
                _logger.LogInformation("Publishing container {ContainerId}", containerId);
                var publishUrl = $"https://graph.facebook.com/v24.0/{igAccountId}/media_publish";
                var publishParams = new Dictionary<string, string>
                {
                    ["creation_id"] = containerId ?? string.Empty,
                    ["access_token"] = tokenToUse
                };
                using var publishContent = new FormUrlEncodedContent(publishParams);
                var publishResp = await _httpClient.PostAsync(publishUrl, publishContent);
                var publishBody = await publishResp.Content.ReadAsStringAsync();

                if (!publishResp.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to publish IG container: {Response}", publishBody);
                    return new VideoPostResult
                    {
                        Platform = "Instagram",
                        Success = false,
                        Message = "Failed to publish to Instagram",
                        ErrorDetails = publishBody
                    };
                }

                // Success
                return new VideoPostResult
                {
                    Platform = "Instagram",
                    Success = true,
                    Message = "Video uploaded successfully to Instagram!",
                    PostId = publishBody
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Instagram upload exception");
                return new VideoPostResult
                {
                    Platform = "Instagram",
                    Success = false,
                    Message = "Error uploading to Instagram",
                    ErrorDetails = ex.Message
                };
            }
        }
    }
}
using VideoUploader.Models;
using System.Net.Http.Headers;

namespace VideoUploader.Services
{
    public interface IFacebookService
    {
        Task<VideoPostResult> UploadVideoAsync(string videoPath, string caption, string accessToken);
    }

    public class FacebookService : IFacebookService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FacebookService> _logger;

        public FacebookService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<FacebookService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<VideoPostResult> UploadVideoAsync(
            string videoPath,
            string caption,
            string accessToken) // we'll ignore this parameter
        {
            try
            {
                var pageId = _configuration["SocialMedia:FacebookPageId"];
                var configuredToken = _configuration["SocialMedia:AccessToken"]; // Use this token directly

                if (string.IsNullOrEmpty(pageId))
                {
                    return new VideoPostResult
                    {
                        Platform = "Facebook",
                        Success = false,
                        Message = "Facebook Page ID not configured"
                    };
                }

                if (string.IsNullOrEmpty(configuredToken))
                {
                    return new VideoPostResult
                    {
                        Platform = "Facebook",
                        Success = false,
                        Message = "Access token not found in appsettings.json"
                    };
                }

                using var form = new MultipartFormDataContent();

                // Read video file
                var videoBytes = await File.ReadAllBytesAsync(videoPath);
                var videoContent = new ByteArrayContent(videoBytes);
                videoContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");

                form.Add(videoContent, "source", Path.GetFileName(videoPath));
                form.Add(new StringContent(caption ?? string.Empty), "description");
                form.Add(new StringContent(configuredToken), "access_token"); // Use configured token directly

                // Use graph-video endpoint
                var url = $"https://graph-video.facebook.com/v24.0/{pageId}/videos";

                _logger.LogInformation($"Uploading video to Facebook Page: {pageId}");

                var response = await _httpClient.PostAsync(url, form);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Facebook upload successful");
                    return new VideoPostResult
                    {
                        Platform = "Facebook",
                        Success = true,
                        Message = "Video uploaded successfully to Facebook!",
                        PostId = responseContent
                    };
                }

                _logger.LogError($"Facebook upload failed: {responseContent}");
                return new VideoPostResult
                {
                    Platform = "Facebook",
                    Success = false,
                    Message = "Failed to upload to Facebook",
                    ErrorDetails = responseContent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Facebook upload exception: {ex.Message}");
                return new VideoPostResult
                {
                    Platform = "Facebook",
                    Success = false,
                    Message = "Error uploading to Facebook",
                    ErrorDetails = ex.Message
                };
            }
        }
    }
}
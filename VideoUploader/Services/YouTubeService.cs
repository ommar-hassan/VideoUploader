using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3.Data;
using VideoUploader.Models;

namespace VideoUploader.Services
{
    public interface IYouTubeService
    {
        Task<VideoPostResult> UploadVideoAsync(
            string videoPath,
            string title,
            string description,
            string accessToken);
    }

    public class YouTubeUploadService : IYouTubeService
    {
        private readonly ILogger<YouTubeUploadService> _logger;

        public YouTubeUploadService(ILogger<YouTubeUploadService> logger)
        {
            _logger = logger;
        }

        public async Task<VideoPostResult> UploadVideoAsync(
            string videoPath,
            string title,
            string description,
            string accessToken)
        {
            try
            {
                if (string.IsNullOrEmpty(accessToken))
                {
                    return new VideoPostResult
                    {
                        Platform = "YouTube",
                        Success = false,
                        Message = "Access token not found"
                    };
                }

                if (!System.IO.File.Exists(videoPath))
                {
                    return new VideoPostResult
                    {
                        Platform = "YouTube",
                        Success = false,
                        Message = "Video file not found"
                    };
                }

                // ⚠️ CRITICAL: YouTube requires a non-empty title
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = "Untitled Video - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                    _logger.LogWarning($"Empty title provided, using default: {title}");
                }

                // Trim and validate
                title = title.Trim();
                if (title.Length > 100)
                {
                    title = title.Substring(0, 100); // YouTube max title length
                }

                description = description?.Trim() ?? title;

                _logger.LogInformation($"Preparing YouTube upload - Title: '{title}', Description length: {description.Length}");

                // Create credential from access token
                var credential = GoogleCredential.FromAccessToken(accessToken);

                // Create YouTube service
                var youtubeService = new Google.Apis.YouTube.v3.YouTubeService(
                    new BaseClientService.Initializer()
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = "Video Uploader"
                    });

                // Create video metadata with validation
                var video = new Video
                {
                    Snippet = new VideoSnippet
                    {
                        Title = title,  // Already validated above
                        Description = description,
                        Tags = new[] { "shorts", "video", "upload" },
                        CategoryId = "22" // People & Blogs
                    },
                    Status = new VideoStatus
                    {
                        PrivacyStatus = "public",
                        SelfDeclaredMadeForKids = false
                    }
                };

                _logger.LogInformation($"Video object created with title: '{video.Snippet.Title}'");

                // Read video file
                using var fileStream = new FileStream(videoPath, FileMode.Open);

                // Create upload request
                var videosInsertRequest = youtubeService.Videos.Insert(
                    video,
                    "snippet,status",
                    fileStream,
                    "video/*");

                videosInsertRequest.ProgressChanged += (progress) =>
                {
                    switch (progress.Status)
                    {
                        case UploadStatus.Uploading:
                            _logger.LogInformation($"YouTube upload progress: {progress.BytesSent} bytes sent");
                            break;
                        case UploadStatus.Failed:
                            _logger.LogError($"YouTube upload failed: {progress.Exception?.Message}");
                            break;
                    }
                };

                videosInsertRequest.ResponseReceived += (uploadedVideo) =>
                {
                    _logger.LogInformation($"YouTube video uploaded! ID: {uploadedVideo.Id}");
                };

                // Upload
                _logger.LogInformation("Starting YouTube upload...");
                var uploadResponse = await videosInsertRequest.UploadAsync();

                if (uploadResponse.Status == UploadStatus.Completed)
                {
                    var uploadedVideo = videosInsertRequest.ResponseBody;

                    return new VideoPostResult
                    {
                        Platform = "YouTube Shorts",
                        Success = true,
                        Message = $"Video uploaded successfully! Video ID: {uploadedVideo.Id}",
                        PostId = uploadedVideo.Id
                    };
                }
                else if (uploadResponse.Exception != null)
                {
                    _logger.LogError($"Upload failed: {uploadResponse.Exception.Message}");
                    return new VideoPostResult
                    {
                        Platform = "YouTube Shorts",
                        Success = false,
                        Message = "Upload failed",
                        ErrorDetails = uploadResponse.Exception.Message
                    };
                }
                else
                {
                    return new VideoPostResult
                    {
                        Platform = "YouTube Shorts",
                        Success = false,
                        Message = "Upload incomplete or failed"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"YouTube upload exception: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return new VideoPostResult
                {
                    Platform = "YouTube Shorts",
                    Success = false,
                    Message = "Error uploading to YouTube",
                    ErrorDetails = ex.Message
                };
            }
        }
    }
}
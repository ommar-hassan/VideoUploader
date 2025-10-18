using VideoUploader.Models;
using VideoUploader.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VideoUploader.Pages
{
    [Authorize]
    public class DashboardModel : PageModel
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IOpenAIService _openAIService;
        private readonly IFacebookService _facebookService;
        private readonly IYouTubeService _youTubeService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DashboardModel> _logger;

        public DashboardModel(
            IWebHostEnvironment environment,
            IOpenAIService openAIService,
            IFacebookService facebookService,
            IYouTubeService youTubeService,
            ILogger<DashboardModel> logger,
            IConfiguration configuration)
        {
            _environment = environment;
            _openAIService = openAIService;
            _facebookService = facebookService;
            _youTubeService = youTubeService;
            _logger = logger;
            _configuration = configuration;
        }

        [BindProperty]
        public string GeneratedCaption { get; set; }

        public List<VideoPostResult> Results { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostGenerateCaptionAsync(
            IFormFile videoFile,
            string videoDescription)
        {
            if (videoFile == null)
            {
                ModelState.AddModelError("", "Please select a video file");
                return Page();
            }

            // Save video temporarily
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");
            var fileName = Guid.NewGuid() + Path.GetExtension(videoFile.FileName);
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await videoFile.CopyToAsync(stream);
            }

            // Generate caption
            var description = string.IsNullOrEmpty(videoDescription)
                ? "an interesting video"
                : videoDescription;

            GeneratedCaption = await _openAIService.GenerateCaptionAsync(description);

            // Store file path in TempData
            TempData["UploadedFilePath"] = filePath;
            TempData["UploadedFileName"] = fileName;

            // Return JSON for AJAX
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { caption = GeneratedCaption, fileName });
            }

            return Page();
        }

        public async Task<IActionResult> OnPostUploadAsync(
            IFormFile videoFile,
            string caption,
            bool postToFacebook,
            bool postToYouTube,
            string existingFileName)
        {
            Results = new List<VideoPostResult>();

            // Handle file
            string filePath;
            string fileName;
            var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads");

            if (videoFile == null)
            {
                if (string.IsNullOrEmpty(existingFileName))
                {
                    Results.Add(new VideoPostResult
                    {
                        Platform = "System",
                        Success = false,
                        Message = "Please select a video file"
                    });
                    return Page();
                }

                fileName = existingFileName;
                filePath = Path.Combine(uploadsPath, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    Results.Add(new VideoPostResult
                    {
                        Platform = "System",
                        Success = false,
                        Message = "File not found. Please re-upload."
                    });
                    return Page();
                }
            }
            else
            {
                fileName = Guid.NewGuid() + Path.GetExtension(videoFile.FileName);
                filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await videoFile.CopyToAsync(stream);
                }
            }

            if (string.IsNullOrEmpty(caption))
            {
                Results.Add(new VideoPostResult
                {
                    Platform = "System",
                    Success = false,
                    Message = "Please provide a caption/title"
                });
                return Page();
            }

            if (!postToFacebook && !postToYouTube)
            {
                Results.Add(new VideoPostResult
                {
                    Platform = "System",
                    Success = false,
                    Message = "Please select at least one platform"
                });
                return Page();
            }

            // Post to Facebook
            if (postToFacebook)
            {
                var fbToken = await HttpContext.GetTokenAsync("access_token")
                    ?? _configuration["SocialMedia:AccessToken"];

                if (!string.IsNullOrEmpty(fbToken))
                {
                    var fbResult = await _facebookService.UploadVideoAsync(
                        filePath,
                        caption,
                        fbToken);
                    Results.Add(fbResult);
                }
                else
                {
                    Results.Add(new VideoPostResult
                    {
                        Platform = "Facebook",
                        Success = false,
                        Message = "Facebook token not found. Please login again."
                    });
                }
            }

            // Upload to YouTube
            if (postToYouTube)
            {
                var ytToken = await HttpContext.GetTokenAsync("access_token");

                if (!string.IsNullOrEmpty(ytToken))
                {
                    _logger.LogInformation("Uploading to YouTube...");

                    var ytResult = await _youTubeService.UploadVideoAsync(
                        filePath,
                        caption, // Title
                        caption, // Description
                        ytToken);
                    Results.Add(ytResult);
                }
                else
                {
                    Results.Add(new VideoPostResult
                    {
                        Platform = "YouTube",
                        Success = false,
                        Message = "YouTube token not found. Please login with YouTube first."
                    });
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            await HttpContext.SignOutAsync();
            return RedirectToPage("/Index");
        }
    }
}
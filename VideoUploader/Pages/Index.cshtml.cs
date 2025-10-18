using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;

namespace VideoUploader.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public IndexModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [TempData]
        public string ErrorMessage { get; set; } = default!;

        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToPage("/Dashboard");
            }
            return Page();
        }

        public IActionResult OnPostFacebookLoginAsync()
        {
            var redirectUri = Url.Page("/FacebookCallback", pageHandler: null, values: null, protocol: Request.Scheme);
            var appId = _configuration["Authentication:Facebook:AppId"];
            var configId = _configuration["Authentication:Facebook:ConfigId"];

            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(configId) || string.IsNullOrEmpty(redirectUri))
            {
                ErrorMessage = "Facebook configuration incomplete";
                return Page();
            }

            var state = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString("fb_oauth_state", state);

            var scopes = "public_profile,email,pages_show_list,pages_read_engagement,pages_manage_posts";

            var oauthUrl = new StringBuilder("https://www.facebook.com/v24.0/dialog/oauth");
            oauthUrl.Append($"?client_id={Uri.EscapeDataString(appId)}");
            oauthUrl.Append($"&redirect_uri={Uri.EscapeDataString(redirectUri)}");
            oauthUrl.Append($"&config_id={Uri.EscapeDataString(configId)}");
            oauthUrl.Append("&response_type=code");
            oauthUrl.Append($"&scope={Uri.EscapeDataString(scopes)}");
            oauthUrl.Append($"&state={Uri.EscapeDataString(state)}");

            return Redirect(oauthUrl.ToString());
        }

        public async Task<IActionResult> OnPostYouTubeLoginAsync()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Page("/Dashboard"),
                IsPersistent = true
            };

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }
    }
}
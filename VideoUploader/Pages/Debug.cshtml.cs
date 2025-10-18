using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace VideoUploader.Pages
{
    public class DebugModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public DebugModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string? OAuthUrl { get; private set; }
        public string? AppId { get; private set; }
        public string? ConfigId { get; private set; }
        public string? RedirectUri { get; private set; }
        public string? Scopes { get; private set; }

        public void OnGet()
        {
            AppId = _configuration["Authentication:Facebook:AppId"];
            ConfigId = _configuration["Authentication:Facebook:ConfigId"];
            RedirectUri = Url.Page("/FacebookCallback", pageHandler: null, values: null, protocol: Request.Scheme);
            Scopes = "public_profile,pages_show_list,pages_manage_posts,pages_read_engagement";

            // Build the same OAuth URL as login
            var state = "debug_state";
            var oauthUrl = new System.Text.StringBuilder("https://www.facebook.com/v24.0/dialog/oauth");
            oauthUrl.Append($"?client_id={Uri.EscapeDataString(AppId ?? "")}");
            oauthUrl.Append($"&redirect_uri={Uri.EscapeDataString(RedirectUri ?? "")}");
            oauthUrl.Append($"&config_id={Uri.EscapeDataString(ConfigId ?? "")}");
            oauthUrl.Append("&response_type=code");
            oauthUrl.Append($"&scope={Uri.EscapeDataString(Scopes)}");
            oauthUrl.Append($"&state={Uri.EscapeDataString(state)}");

            OAuthUrl = oauthUrl.ToString();
        }
    }
}

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VideoUploader.Pages
{
    public class FacebookCallbackModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FacebookCallbackModel> _logger;

        public FacebookCallbackModel(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<FacebookCallbackModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(string? code, string? state, string? error, string? error_description)
        {
            if (!string.IsNullOrEmpty(error))
            {
                TempData["ErrorMessage"] = $"Facebook login error: {error} - {error_description}";
                return RedirectToPage("/Index");
            }

            // Validate state
            var expectedState = HttpContext.Session.GetString("fb_oauth_state");
            HttpContext.Session.Remove("fb_oauth_state");

            if (string.IsNullOrEmpty(state) || state != expectedState)
            {
                TempData["ErrorMessage"] = "Invalid OAuth state. Please try again.";
                return RedirectToPage("/Index");
            }

            if (string.IsNullOrEmpty(code))
            {
                TempData["ErrorMessage"] = "Facebook did not return an authorization code.";
                return RedirectToPage("/Index");
            }

            var appId = _configuration["Authentication:Facebook:AppId"];
            var appSecret = _configuration["Authentication:Facebook:AppSecret"];

            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appSecret))
            {
                TempData["ErrorMessage"] = "Facebook AppId or AppSecret not configured.";
                return RedirectToPage("/Index");
            }

            // Build redirectUri exactly like the request to avoid mismatch
            var redirectUri = Url.Page("/FacebookCallback", pageHandler: null, values: null, protocol: Request.Scheme)
                              ?? Url.Page("/FacebookCallback", pageHandler: null, values: null, protocol: Request.Scheme);

            if (string.IsNullOrEmpty(redirectUri))
            {
                TempData["ErrorMessage"] = "Unable to generate redirect URI for token exchange. Ensure callback page path is correct.";
                return RedirectToPage("/Index");
            }

            // Exchange code for access token
            var tokenRequestUrl = "https://graph.facebook.com/v24.0/oauth/access_token" +
                $"?client_id={Uri.EscapeDataString(appId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&client_secret={Uri.EscapeDataString(appSecret)}" +
                $"&code={Uri.EscapeDataString(code)}";

            var client = _httpClientFactory.CreateClient();
            HttpResponseMessage tokenResp;
            try
            {
                tokenResp = await client.GetAsync(tokenRequestUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token exchange request failed");
                TempData["ErrorMessage"] = "Token exchange failed.";
                return RedirectToPage("/Index");
            }

            var tokenContent = await tokenResp.Content.ReadAsStringAsync();
            if (!tokenResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Token exchange failed: {Response}", tokenContent);
                TempData["ErrorMessage"] = "Token exchange failed: " + tokenContent;
                return RedirectToPage("/Index");
            }

            using var tokenDoc = JsonDocument.Parse(tokenContent);
            var accessToken = tokenDoc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            var expiresIn = tokenDoc.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : (int?)null;

            if (string.IsNullOrEmpty(accessToken))
            {
                TempData["ErrorMessage"] = "Access token not returned by Facebook.";
                return RedirectToPage("/Index");
            }

            // Fetch basic profile (id, name, email)
            var meUrl = $"https://graph.facebook.com/v24.0/me?fields=id,name,email&access_token={Uri.EscapeDataString(accessToken)}";
            var meResp = await client.GetAsync(meUrl);
            var meContent = await meResp.Content.ReadAsStringAsync();
            if (!meResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get user profile: {Response}", meContent);
                TempData["ErrorMessage"] = "Failed to fetch Facebook profile: " + meContent;
                return RedirectToPage("/Index");
            }

            using var meDoc = JsonDocument.Parse(meContent);
            var fbId = meDoc.RootElement.TryGetProperty("id", out var pid) ? pid.GetString() : null;
            var fbName = meDoc.RootElement.TryGetProperty("name", out var pn) ? pn.GetString() : null;
            var fbEmail = meDoc.RootElement.TryGetProperty("email", out var pe) ? pe.GetString() : null;

            // Build claims and sign-in with cookie auth
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, fbId ?? Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, fbName ?? "FacebookUser")
            };

            if (!string.IsNullOrEmpty(fbEmail))
            {
                claims.Add(new Claim(ClaimTypes.Email, fbEmail));
            }

            // Store access token as a claim (for convenience) and persist token in auth properties
            claims.Add(new Claim("access_token", accessToken));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true
            };

            // Save token so HttpContext.GetTokenAsync("access_token") works
            var tokens = new List<AuthenticationToken>
            {
                new AuthenticationToken { Name = "access_token", Value = accessToken }
            };

            if (expiresIn.HasValue)
            {
                var expireAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn.Value).ToString("o");
                tokens.Add(new AuthenticationToken { Name = "expires_at", Value = expireAt });
            }

            authProperties.StoreTokens(tokens);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            return RedirectToPage("/Dashboard");
        }
    }
}
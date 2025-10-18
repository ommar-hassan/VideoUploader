using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using VideoUploader.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
});
builder.Services.AddHttpClient();

// Configure Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Configure Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Index";
    options.LogoutPath = "/Logout";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    options.SaveTokens = true;
    options.Scope.Add("https://www.googleapis.com/auth/youtube.upload");
    options.Scope.Add("https://www.googleapis.com/auth/youtube.readonly");
});

// Register services
builder.Services.AddScoped<IFacebookService, FacebookService>();
builder.Services.AddScoped<IYouTubeService, YouTubeUploadService>();
builder.Services.AddScoped<IInstagramService, InstagramService>();
builder.Services.AddScoped<IOpenAIService, OpenAIService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Important: Session before Authentication
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Create uploads directory
var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.Run();
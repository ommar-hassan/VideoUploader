# VideoUploader

AI Prompt:

Build a complete ASP.NET Core 8 Razor Pages web app that allows users to upload videos, generate AI captions, and post them to Facebook Pages and YouTube Shorts.
Use Bootstrap 5 for a modern, responsive UI and implement OAuth 2.0 for both platforms.

⚙️ Core Tech Stack

Framework: ASP.NET Core 8 (Razor Pages)

Frontend: Bootstrap 5 + Bootstrap Icons

Backend: C#, async/await, dependency injection

Authentication: OAuth 2.0 (Facebook & Google)

APIs:

Facebook Graph API v24.0 (for Facebook video posts)

YouTube Data API v3 (for YouTube uploads)

OpenAI API (gpt-4o-mini or gpt-4-turbo) for caption generation

🗂️ Structure
/VideoUploader
 ├── Pages/
 │   ├── Index.cshtml  (Login)
 │   ├── Dashboard.cshtml  (Upload + Caption)
 │   ├── FacebookCallback.cshtml  (OAuth callback)
 │   └── Shared/_Layout.cshtml
 ├── Services/
 │   ├── FacebookService.cs
 │   ├── YouTubeUploadService.cs
 │   └── OpenAIService.cs
 ├── Models/VideoPostResult.cs
 ├── appsettings.json
 └── Program.cs

🔐 Authentication

Facebook: Manual OAuth flow using Configuration ID, exchanging code → token, and fetching Page Access Token via /me/accounts.

YouTube: Built-in Google OAuth with youtube.upload scope using Microsoft.AspNetCore.Authentication.Google.

Store tokens securely in session/auth cookies.

💻 Pages

Index.cshtml (Landing/Login)

Two login buttons: Facebook (blue), YouTube (red).

Responsive card UI with gradient background.

Dashboard.cshtml (Main Page)

File upload (video/*), caption input, and “Generate Caption with AI” button.

Checkboxes for Facebook/YouTube (auto-disabled if not logged in).

Upload button with spinner and Bootstrap alerts for results.

🧩 Services

FacebookService: Upload video to https://graph-video.facebook.com/v24.0/{pageId}/videos using multipart/form-data.

YouTubeUploadService: Upload video using Google.Apis.YouTube.v3.YouTubeService with validated titles.

OpenAIService: Generate short, engaging captions with emojis and hashtags using gpt-4o-mini.

⚙️ Program.cs Highlights

Add Razor Pages, Session, and HttpClient.

Use Cookie + Google authentication.

Scopes:

https://www.googleapis.com/auth/youtube.upload
https://www.googleapis.com/auth/youtube.readonly


Register IFacebookService, IYouTubeService, IOpenAIService.

Middleware order:
UseHttpsRedirection → UseStaticFiles → UseRouting → UseSession → UseAuthentication → UseAuthorization → MapRazorPages.

🎨 UI/UX

Bootstrap 5 design with gradient background (#667eea → #764ba2).

Rounded white cards, spinners, success/error alerts.

Responsive mobile-first layout.

Platform badges showing which account is logged in.

✅ Expected Output

A fully working Razor Pages web app where the user can:

Login via Facebook or YouTube.

Upload a video and generate an AI-powered caption.

Post automatically to Facebook Pages or YouTube Shorts.

See real-time upload progress, success/failure messages, and platform indicators
Result:
A modern, secure, and AI-integrated Video Uploader with Facebook + YouTube integration, built on Razor Pages and Bootstrap 5.

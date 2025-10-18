namespace VideoUploader.Models
{
    public class VideoPostResult
    {
        public string Platform { get; set; } = default!;
        public bool Success { get; set; }
        public string Message { get; set; } = default!;
        public string PostId { get; set; } = default!;
        public string ErrorDetails { get; set; } = default!;
    }
}
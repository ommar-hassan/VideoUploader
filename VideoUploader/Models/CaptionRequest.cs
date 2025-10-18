namespace VideoUploader.Models
{
    public class CaptionRequest
    {
        public string VideoFileName { get; set; } = default!;
        public string VideoDescription { get; set; } = default!;
        public string Tone { get; set; } = "engaging";
    }
}
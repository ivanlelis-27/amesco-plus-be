using Microsoft.AspNetCore.Http;

namespace AmescoAPI.Models.DTOs
{
    public class UploadImage
    {
        public string UserId { get; set; }
        public IFormFile Image { get; set; }
    }
}
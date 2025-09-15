using Microsoft.AspNetCore.Http;

namespace AmescoAPI.Models.DTOs
{
    public class UploadImage
    {
        public int UserId { get; set; }   // reference sa user
        public IFormFile Image { get; set; } // actual file na iuupload
    }
}
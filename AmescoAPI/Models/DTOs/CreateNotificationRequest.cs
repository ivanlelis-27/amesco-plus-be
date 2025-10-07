using Microsoft.AspNetCore.Http;
using System;

namespace AmescoAPI.Models.DTOs
{
    public class CreateNotificationRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string MessageBody { get; set; }
        public DateTime ScheduledAt { get; set; }
        public bool IncludeImage { get; set; }
        public IFormFile? Image { get; set; }
    }
}
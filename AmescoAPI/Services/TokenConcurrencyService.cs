using AmescoAPI.Data;
using System.Linq;

namespace AmescoAPI.Services
{
    public class TokenConcurrencyService
    {
        private readonly AppDbContext _context;

        public TokenConcurrencyService(AppDbContext context)
        {
            _context = context;
        }

        public bool IsTokenValidForUser(string userId, string token)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id.ToString() == userId);
            return user != null && user.CurrentJwtToken == token;
        }

        public bool IsSessionValidForUser(string userId, string sessionId)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id.ToString() == userId);
            return user != null && !string.IsNullOrEmpty(user.CurrentSessionId) && user.CurrentSessionId == sessionId;
        }
    }
}
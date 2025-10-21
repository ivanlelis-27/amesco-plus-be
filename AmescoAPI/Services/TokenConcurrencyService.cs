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

        // ✅ Check validity of JWT token via UserSessions table
        public bool IsTokenValidForUser(string userId, string token)
        {
            var session = _context.UserSessions
                .FirstOrDefault(s => s.UserId.ToString() == userId && s.JwtToken == token);
            return session != null;
        }

        // ✅ Check validity of SessionId via UserSessions table
        public bool IsSessionValidForUser(string userId, string sessionId)
        {
            var session = _context.UserSessions
                .FirstOrDefault(s => s.UserId.ToString() == userId && s.SessionId == sessionId);
            return session != null;
        }
    }
}

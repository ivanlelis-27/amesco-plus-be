using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;
using System.Linq;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        [HttpGet("me")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public IActionResult Me()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null)
                return Unauthorized();

            if (!int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null)
                return NotFound();

            var points = _context.Points.FirstOrDefault(p => p.UserId == userId);

            return Ok(new
            {
                name = $"{user.FirstName} {user.LastName}",
                email = user.Email,
                mobile = user.Mobile,
                points = points?.PointsBalance ?? 0
            });
        }

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(_context.Users.ToList());
        }

        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null) return NotFound();
            return Ok(user);
        }

        [HttpPost]
        public IActionResult Create(Users user)
        {
            if (!IsValidEmail(user.Email))
                return BadRequest("Invalid email format.");

            user.CreatedAt = DateTime.Now; // auto set if not provided
            _context.Users.Add(user);
            _context.SaveChanges();
            return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
        }
        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var pattern = @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$";
            if (!System.Text.RegularExpressions.Regex.IsMatch(email, pattern)) return false;
            var domain = email.Split('@').Length > 1 ? email.Split('@')[1] : "";
            if (domain.Split('.').Length < 2) return false;
            var tld = domain.Substring(domain.LastIndexOf('.') + 1);
            if (tld.Length < 2) return false;
            return true;
        }


        [HttpPut("{id}")]
        public IActionResult Update(int id, Users updated)
        {
            var user = _context.Users.Find(id);
            if (user == null) return NotFound();

            // update only relevant fields
            user.FirstName = updated.FirstName;
            user.LastName = updated.LastName;
            user.Email = updated.Email;
            user.Mobile = updated.Mobile;

            _context.SaveChanges();
            return Ok(user);
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            _context.SaveChanges();
            return NoContent();
        }

    }
}

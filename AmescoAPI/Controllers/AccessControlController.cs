using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccessControlController : ControllerBase
    {
        private readonly AppDbContext _context;
        public AccessControlController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var users = _context.AccessControls.ToList();
            return Ok(users);
        }

        [HttpPost]
        public IActionResult Create([FromBody] CreateAccessControlRequest request)
        {
            var user = new AccessControl
            {
                FullName = $"{request.FirstName} {request.LastName}",
                Email = request.Email,
                PasswordHash = request.PasswordHash,
                Role = request.Role,
                BranchID = request.BranchID,
                CreatedAt = DateTime.Now,
                IsActive = true,
                LastLogin = null
            };
            _context.AccessControls.Add(user);
            _context.SaveChanges();
            return Ok(user);
        }

        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] AccessControl updated)
        {
            var user = _context.AccessControls.Find(id);
            if (user == null) return NotFound();

            user.FullName = updated.FullName;
            user.Email = updated.Email;
            user.PasswordHash = updated.PasswordHash;
            user.Role = updated.Role;
            user.BranchID = updated.BranchID;
            user.LastLogin = updated.LastLogin;
            user.IsActive = updated.IsActive;

            _context.SaveChanges();
            return Ok(user);
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var user = _context.AccessControls.Find(id);
            if (user == null) return NotFound();
            _context.AccessControls.Remove(user);
            _context.SaveChanges();
            return NoContent();
        }
    }
}
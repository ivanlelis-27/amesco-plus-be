using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExistingMembersController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ExistingMembersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("by-memberid")]
        public IActionResult GetByMemberId([FromQuery] string memberId)
        {
            var member = _context.ExistingMembers.FirstOrDefault(m => m.MemberId == memberId);
            if (member == null) return NotFound();
            return Ok(member);
        }

        [HttpPost]
        public IActionResult Create([FromBody] ExistingMember member)
        {
            member.ImportedAt = DateTime.UtcNow;
            _context.ExistingMembers.Add(member);
            _context.SaveChanges();
            return Ok(member);
        }
    }
}
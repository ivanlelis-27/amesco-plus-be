using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;
using System.Linq;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PromoGroupsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PromoGroupsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var groups = _context.PromoGroups.OrderByDescending(pg => pg.DateCreated).ToList();
            return Ok(groups);
        }

        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            var group = _context.PromoGroups.Find(id);
            if (group == null) return NotFound();
            return Ok(group);
        }

        [HttpPost]
        public IActionResult Create([FromBody] PromoGroup group)
        {
            group.DateCreated = DateTime.Now;
            _context.PromoGroups.Add(group);
            _context.SaveChanges();
            return CreatedAtAction(nameof(Get), new { id = group.PromoGroupId }, group);
        }

        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] PromoGroup updated)
        {
            var group = _context.PromoGroups.Find(id);
            if (group == null) return NotFound();
            group.Name = updated.Name;
            group.Description = updated.Description;
            _context.SaveChanges();
            return Ok(group);
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var group = _context.PromoGroups.Find(id);
            if (group == null) return NotFound();
            _context.PromoGroups.Remove(group);
            _context.SaveChanges();
            return NoContent();
        }
    }
}
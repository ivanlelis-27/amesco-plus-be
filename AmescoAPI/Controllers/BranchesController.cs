using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BranchesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public BranchesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var branches = _context.Branches.ToList();
            return Ok(branches);
        }

        [HttpPost]
        public IActionResult Create([FromBody] Branch branch)
        {
            _context.Branches.Add(branch);
            _context.SaveChanges();
            return Ok(branch);
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var branch = _context.Branches.Find(id);
            if (branch == null) return NotFound();
            _context.Branches.Remove(branch);
            _context.SaveChanges();
            return NoContent();
        }

        [HttpPost("bulk")]
        public IActionResult BulkInsert([FromBody] List<Branch> branches)
        {
            _context.Branches.AddRange(branches);
            _context.SaveChanges();
            return Ok(new { count = branches.Count });
        }

        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] Branch updated)
        {
            var branch = _context.Branches.Find(id);
            if (branch == null) return NotFound();

            branch.BranchName = updated.BranchName;
            branch.Address = updated.Address;
            branch.Contact = updated.Contact;
            branch.Latitude = updated.Latitude;
            branch.Longitude = updated.Longitude;
            branch.StartDay = updated.StartDay;
            branch.EndDay = updated.EndDay;
            branch.OpenTime = updated.OpenTime;
            branch.CloseTime = updated.CloseTime;
            branch.Email = updated.Email;

            _context.SaveChanges();
            return Ok(branch);
        }

        
    }
}
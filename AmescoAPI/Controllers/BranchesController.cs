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

        [HttpGet("head-office")]
        public IActionResult GetHeadOffice()
        {
            var branch = _context.Branches.FirstOrDefault(b => b.BranchID == 39);
            if (branch == null) return NotFound();
            return Ok(branch);
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

        [HttpPut("head-office")]
        public IActionResult UpdateHeadOffice([FromBody] Branch updated)
        {
            var branch = _context.Branches.Find(39);
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

            // All fields will accept nulls if your model and DB allow it
            _context.SaveChanges();
            return Ok(branch);
        }

        [HttpGet("branches-rankings")]
        public IActionResult GetTop5BranchesByPointsGiven(DateTime? startDate, DateTime? endDate)
        {
            var history = _context.BranchPointsHistory.AsQueryable();

            if (startDate.HasValue)
                history = history.Where(h => h.Date >= startDate.Value.Date);

            if (endDate.HasValue)
                history = history.Where(h => h.Date <= endDate.Value.Date);

            var topBranches = history
                .GroupBy(h => h.BranchID)
                .Select(g => new
                {
                    branchId = g.Key,
                    pointsGiven = g.Sum(x => x.PointsGiven)
                })
                .OrderByDescending(x => x.pointsGiven)
                .Take(5)
                .ToList();

            // Get branch names in one query
            var branchIds = topBranches.Select(x => x.branchId).ToList();
            var branches = _context.Branches
                .Where(b => branchIds.Contains(b.BranchID))
                .ToDictionary(b => b.BranchID, b => b.BranchName);

            var result = topBranches
                .Select(x => new
                {
                    branchId = x.branchId,
                    branchName = branches.ContainsKey(x.branchId) ? branches[x.branchId] : "",
                    pointsGiven = x.pointsGiven
                })
                .ToList();

            return Ok(result);
        }
    }
}
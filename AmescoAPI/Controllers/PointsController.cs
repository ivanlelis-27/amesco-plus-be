using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;
using System.Linq;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PointsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PointsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(_context.Points.ToList());
        }

        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            var points = _context.Points.Find(id);
            if (points == null) return NotFound();
            return Ok(points);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using AmescoAPI.Data;
using AmescoAPI.Models;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace AmescoAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public TransactionsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public IActionResult CreateTransactionWithProducts([FromBody] CreateTransactionWithProductsRequest request)
        {
            var transaction = new Transaction
            {
                UserId = request.UserId,
                EarnedPoints = request.EarnedPoints,
                DateIssued = DateTime.Now,
                BranchId = request.BranchId,
            };
            _context.Transactions.Add(transaction);
            _context.SaveChanges();

            // Add products
            foreach (var prod in request.Products)
            {
                var tp = new TransactionProduct
                {
                    TransactionId = transaction.TransactionId,
                    ProductName = prod.ProductName,
                    Quantity = prod.Quantity,
                };
                _context.TransactionProducts.Add(tp);
            }
            _context.SaveChanges();

            // Update user's PointsBalance
            var points = _context.Points.FirstOrDefault(p => p.UserId == request.UserId);
            if (points != null)
            {
                points.PointsBalance += request.EarnedPoints;
                points.UpdatedAt = DateTime.Now;
                _context.SaveChanges();
            }

            // Update PointsGiven for the branch
            var branch = _context.Branches.FirstOrDefault(b => b.BranchID == request.BranchId);
            if (branch != null)
            {
                branch.PointsGiven += request.EarnedPoints;
                _context.SaveChanges();
            }

            // Update BranchPointsHistory for date-based aggregation
            var today = DateTime.Today;
            var history = _context.BranchPointsHistory
                .FirstOrDefault(h => h.BranchID == request.BranchId && h.Date == today);

            if (history != null)
            {
                history.PointsGiven += request.EarnedPoints;
            }
            else
            {
                _context.BranchPointsHistory.Add(new BranchPointsHistory
                {
                    BranchID = request.BranchId,
                    Date = today,
                    PointsGiven = request.EarnedPoints
                });
            }
            _context.SaveChanges();

            // Return transaction and products
            var products = _context.TransactionProducts.Where(tp => tp.TransactionId == transaction.TransactionId).ToList();
            return Ok(new { transaction, products, newPointsBalance = points?.PointsBalance });
        }

        [HttpGet]
        public IActionResult GetAllTransactions()
        {
            var result = _context.Transactions
                .Select(t => new
                {
                    transactionId = t.TransactionId,
                    dateIssued = t.DateIssued,
                    earnedPoints = t.EarnedPoints,
                    userName = _context.Users.Where(u => u.Id == t.UserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                    branchName = _context.Branches.Where(b => b.BranchID == t.BranchId).Select(b => b.BranchName).FirstOrDefault(),
                    products = _context.TransactionProducts.Where(tp => tp.TransactionId == t.TransactionId).ToList()
                })
                .ToList();

            return Ok(result);
        }
        
        [HttpGet("{id}")]
        public IActionResult GetTransaction(int id)
        {
            var transaction = _context.Transactions.FirstOrDefault(t => t.TransactionId == id);
            if (transaction == null) return NotFound();
            var products = _context.TransactionProducts.Where(tp => tp.TransactionId == id).ToList();
            return Ok(new { transaction, products });
        }

        [HttpGet("top-10-ranking")]
        public IActionResult GetTop10Ranking(DateTime? startDate, DateTime? endDate)
        {
            var transactions = _context.Transactions.AsQueryable();

            if (startDate.HasValue)
                transactions = transactions.Where(t => t.DateIssued >= startDate.Value);

            if (endDate.HasValue)
                transactions = transactions.Where(t => t.DateIssued < endDate.Value.Date.AddDays(1)); // Inclusive

            // Group transactions by UserId and sum EarnedPoints
            var userPoints = transactions
                .GroupBy(t => t.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    TotalEarnedPoints = g.Sum(t => t.EarnedPoints)
                })
                .OrderByDescending(up => up.TotalEarnedPoints)
                .Take(10)
                .ToList();

            // Join with Users table to get MemberId, FullName, and Email
            var ranking = userPoints
                .Select((up, index) =>
                {
                    var user = _context.Users.FirstOrDefault(u => u.Id == up.UserId);
                    return new
                    {
                        rank = index + 1,
                        userId = up.UserId,
                        memberId = user?.MemberId ?? "",
                        fullName = user != null ? $"{user.FirstName} {user.LastName}" : "",
                        email = user?.Email ?? "",
                        totalEarnedPoints = up.TotalEarnedPoints
                    };
                })
                .ToList();

            return Ok(ranking);
        }

        [HttpGet("total-earned-points")]
        public IActionResult GetTotalEarnedPoints()
        {
            decimal totalEarnedPoints = _context.Transactions.Sum(t => t.EarnedPoints);
            return Ok(new { totalEarnedPoints });
        }

        [HttpGet("earned-points")]
        public IActionResult GetEarnedPoints(DateTime? startDate, DateTime? endDate)
        {
            var transactions = _context.Transactions.AsQueryable();

            if (startDate.HasValue)
                transactions = transactions.Where(t => t.DateIssued >= startDate.Value);

            if (endDate.HasValue)
                transactions = transactions.Where(t => t.DateIssued < endDate.Value.Date.AddDays(1)); // Inclusive of end date

            decimal earnedPoints = transactions.Sum(t => t.EarnedPoints);

            return Ok(new { earnedPoints });
        }


        [HttpGet("top-5-branches")]
        public IActionResult GetTop5BranchesByPoints()
        {
            var branchPoints = _context.Transactions
                .GroupBy(t => t.BranchId)
                .Select(g => new
                {
                    BranchId = g.Key,
                    TotalEarnedPoints = g.Sum(t => t.EarnedPoints)
                })
                .OrderByDescending(bp => bp.TotalEarnedPoints)
                .Take(5)
                .ToList();

            var result = branchPoints.Select((bp, idx) =>
            {
                var branch = _context.Branches.FirstOrDefault(b => b.BranchID == bp.BranchId);
                return new
                {
                    rank = $"top{idx + 1}",
                    branchId = bp.BranchId,
                    branchName = branch?.BranchName ?? "",
                    totalEarnedPoints = bp.TotalEarnedPoints
                };
            }).ToList();

            return Ok(result);
        }

        [HttpGet("by-date-range")]
        public IActionResult GetTransactionsByDateRange(DateTime start, DateTime end)
        {
            // Make sure 'end' includes the whole day
            var endInclusive = end.Date.AddDays(1);

            var transactions = _context.Transactions
                .Where(t => t.DateIssued >= start && t.DateIssued < endInclusive)
                .ToList();

            var result = transactions.Select(t => new
            {
                transaction = t,
                products = _context.TransactionProducts.Where(tp => tp.TransactionId == t.TransactionId).ToList()
            });

            return Ok(result);
        }


        // FOR TESTING PURPOSES ONLY
        [HttpDelete("clear-all")]
        public IActionResult ClearAllTransactions()
        {
            _context.Database.ExecuteSqlRaw("DELETE FROM TransactionProducts");
            _context.Database.ExecuteSqlRaw("DELETE FROM Transactions");
            _context.Database.ExecuteSqlRaw("DBCC CHECKIDENT ('Transactions', RESEED, 0)");
            return Ok(new { message = "All transactions deleted and identity reseeded." });
        }

        [HttpDelete("clear-products")]
        public IActionResult ClearAllTransactionProducts()
        {
            _context.Database.ExecuteSqlRaw("DELETE FROM TransactionProducts");
            _context.Database.ExecuteSqlRaw("DBCC CHECKIDENT ('TransactionProducts', RESEED, 0)");
            return Ok(new { message = "All transaction products deleted and identity reseeded." });
        }
        // END TESTING PURPOSES ONLY
    }
}

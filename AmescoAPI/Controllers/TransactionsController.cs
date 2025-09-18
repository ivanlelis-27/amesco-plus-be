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
                DateIssued = DateTime.Now
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
                    Quantity = prod.Quantity
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

            // Return transaction and products
            var products = _context.TransactionProducts.Where(tp => tp.TransactionId == transaction.TransactionId).ToList();
            return Ok(new { transaction, products, newPointsBalance = points?.PointsBalance });
        }

        [HttpGet]
        public IActionResult GetAllTransactions()
        {
            var transactions = _context.Transactions.ToList();
            var result = transactions.Select(t => new
            {
                transaction = t,
                products = _context.TransactionProducts.Where(tp => tp.TransactionId == t.TransactionId).ToList()
            });
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

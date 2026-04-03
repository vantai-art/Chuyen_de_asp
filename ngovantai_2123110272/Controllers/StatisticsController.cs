using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantAPI.Data;

namespace RestaurantAPI.Controllers
{
    [Route("api/statistics")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class StatisticsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public StatisticsController(AppDbContext context) { _context = context; }

        // GET: api/statistics/summary  (Tổng quan hôm nay)
        [HttpGet("summary")]
        public async Task<ActionResult> GetSummary()
        {
            var today = DateTime.Today;

            var todayOrders = await _context.Orders
                .Where(o => o.OrderDate.Date == today)
                .ToListAsync();

            var todayRevenue = await _context.Payments
                .Where(p => p.PaymentDate.Date == today)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            var tables = await _context.Tables.ToListAsync();
            var totalFoods = await _context.Foods.CountAsync();
            var totalUsers = await _context.Users.CountAsync(u => u.IsActive);

            return Ok(new
            {
                Today = today.ToString("dd/MM/yyyy"),
                TodayOrders = todayOrders.Count,
                TodayRevenue = todayRevenue,
                PendingOrders = todayOrders.Count(o => o.Status == "Pending"),
                CompletedOrders = todayOrders.Count(o => o.Status == "Completed"),
                TotalTables = tables.Count,
                AvailableTables = tables.Count(t => t.Status == "Available"),
                OccupiedTables = tables.Count(t => t.Status == "Occupied"),
                TotalFoods = totalFoods,
                TotalStaff = totalUsers
            });
        }

        // GET: api/statistics/revenue?year=2025&month=3
        [HttpGet("revenue")]
        public async Task<ActionResult> GetMonthlyRevenue([FromQuery] int year, [FromQuery] int? month)
        {
            if (year == 0) year = DateTime.Now.Year;

            if (month.HasValue)
            {
                // Doanh thu từng ngày trong tháng
                var daysInMonth = DateTime.DaysInMonth(year, month.Value);
                var data = new List<object>();

                for (int d = 1; d <= daysInMonth; d++)
                {
                    var date = new DateTime(year, month.Value, d);
                    var rev = await _context.Payments
                        .Where(p => p.PaymentDate.Date == date.Date)
                        .SumAsync(p => (decimal?)p.Amount) ?? 0;
                    var orders = await _context.Orders
                        .CountAsync(o => o.OrderDate.Date == date.Date && o.Status == "Completed");
                    data.Add(new { day = d, date = date.ToString("dd/MM"), revenue = rev, orders });
                }
                return Ok(data);
            }
            else
            {
                // Doanh thu từng tháng trong năm
                var data = new List<object>();
                for (int m = 1; m <= 12; m++)
                {
                    var rev = await _context.Payments
                        .Where(p => p.PaymentDate.Year == year && p.PaymentDate.Month == m)
                        .SumAsync(p => (decimal?)p.Amount) ?? 0;
                    var orders = await _context.Orders
                        .CountAsync(o => o.OrderDate.Year == year && o.OrderDate.Month == m && o.Status == "Completed");
                    data.Add(new { month = m, label = $"T{m}", revenue = rev, orders });
                }
                return Ok(data);
            }
        }

        // GET: api/statistics/top-foods?limit=10
        [HttpGet("top-foods")]
        public async Task<ActionResult> GetTopFoods([FromQuery] int limit = 10)
        {
            var topFoods = await _context.OrderDetails
                .Include(od => od.Food)
                .Where(od => od.Food != null)
                .GroupBy(od => new { od.FoodId, od.Food!.Name, od.Food.Price })
                .Select(g => new
                {
                    FoodId = g.Key.FoodId,
                    FoodName = g.Key.Name,
                    Price = g.Key.Price,
                    TotalQuantity = g.Sum(od => od.Quantity),
                    TotalRevenue = g.Sum(od => od.Quantity * od.UnitPrice),
                    OrderCount = g.Select(od => od.OrderId).Distinct().Count()
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(limit)
                .ToListAsync();

            return Ok(topFoods);
        }

        // GET: api/statistics/revenue/daily?date=2025-03-01
        [HttpGet("revenue/daily")]
        public async Task<ActionResult> GetDailyRevenue([FromQuery] DateTime? date)
        {
            var targetDate = (date ?? DateTime.Today).Date;

            var payments = await _context.Payments
                .Include(p => p.Order).ThenInclude(o => o!.Table)
                .Where(p => p.PaymentDate.Date == targetDate)
                .ToListAsync();

            return Ok(new
            {
                Date = targetDate.ToString("dd/MM/yyyy"),
                TotalRevenue = payments.Sum(p => p.Amount),
                TotalOrders = payments.Count,
                Payments = payments
            });
        }
    }
}

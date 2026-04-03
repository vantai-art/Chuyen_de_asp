using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantAPI.Data;
using RestaurantAPI.DTOs;
using RestaurantAPI.Models;

namespace RestaurantAPI.Controllers
{
    [Route("api/payment")]
    [ApiController]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _context;
        public PaymentController(AppDbContext context) { _context = context; }

        // POST: api/payment  (Staff+)
        [HttpPost]
        public async Task<ActionResult> CreatePayment([FromBody] CreatePaymentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var order = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.Payment)
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.Id == dto.OrderId);

            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });
            if (order.Payment != null) return BadRequest(new { message = "Đơn hàng này đã được thanh toán" });
            if (order.Status == "Cancelled") return BadRequest(new { message = "Không thể thanh toán đơn hàng đã hủy" });
            if (order.Status == "Pending") return BadRequest(new { message = "Đơn hàng chưa được xác nhận" });

            var validMethods = new[] { "Cash", "Card", "Transfer" };
            if (!validMethods.Contains(dto.Method))
                return BadRequest(new { message = "Phương thức thanh toán không hợp lệ (Cash/Card/Transfer)" });

            var payment = new Payment
            {
                OrderId = dto.OrderId,
                Amount = order.TotalAmount,
                Method = dto.Method,
                Note = dto.Note,
                PaymentDate = DateTime.Now
            };

            _context.Payments.Add(payment);

            // Cập nhật trạng thái đơn -> Completed
            order.Status = "Completed";

            // Giải phóng bàn
            if (order.Table != null)
            {
                var otherActive = await _context.Orders
                    .AnyAsync(o => o.TableId == order.TableId && o.Id != order.Id
                        && o.Status != "Completed" && o.Status != "Cancelled");
                if (!otherActive)
                    order.Table.Status = "Available";
            }

            await _context.SaveChangesAsync();

            var result = await _context.Payments
                .Include(p => p.Order).ThenInclude(o => o!.Table)
                .FirstAsync(p => p.Id == payment.Id);

            return Ok(new { message = "Thanh toán thành công", payment = result });
        }

        // GET: api/payment/order/{orderId}  (Staff+)
        [HttpGet("order/{orderId}")]
        public async Task<ActionResult> GetPaymentByOrder(int orderId)
        {
            var payment = await _context.Payments
                .Include(p => p.Order)
                .FirstOrDefaultAsync(p => p.OrderId == orderId);
            if (payment == null) return NotFound(new { message = "Chưa có thanh toán cho đơn này" });
            return Ok(payment);
        }

        // GET: api/payment  (Admin)
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetPayments([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var query = _context.Payments
                .Include(p => p.Order).ThenInclude(o => o!.Table)
                .AsQueryable();

            if (from.HasValue) query = query.Where(p => p.PaymentDate.Date >= from.Value.Date);
            if (to.HasValue) query = query.Where(p => p.PaymentDate.Date <= to.Value.Date);

            var payments = await query.OrderByDescending(p => p.PaymentDate).ToListAsync();
            return Ok(payments);
        }
    }
}

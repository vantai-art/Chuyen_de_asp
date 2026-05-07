using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantAPI.Data;
using RestaurantAPI.DTOs;
using RestaurantAPI.Models;
using RestaurantAPI.Services;

namespace RestaurantAPI.Controllers
{
    [Route("api/payment")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly VnPayService _vnPay;

        public PaymentController(AppDbContext context, VnPayService vnPay)
        {
            _context = context;
            _vnPay = vnPay;
        }

        // POST: api/payment  (Cash / Card / Transfer)
        [HttpPost]
        [Authorize]
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
                PaymentDate = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            order.Status = "Completed";

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

        // POST: api/payment/vnpay/create
        [HttpPost("vnpay/create")]
        [Authorize]
        public async Task<ActionResult> CreateVnPayUrl([FromBody] CreateVnPayDto dto)
        {
            var order = await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == dto.OrderId);

            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });
            if (order.Payment != null) return BadRequest(new { message = "Đơn hàng đã được thanh toán" });
            if (order.Status == "Cancelled") return BadRequest(new { message = "Đơn hàng đã bị hủy" });
            if (order.Status == "Pending") return BadRequest(new { message = "Đơn hàng chưa được xác nhận" });

            var ipAddr = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            if (ipAddr == "::1") ipAddr = "127.0.0.1";

            var payUrl = _vnPay.CreatePaymentUrl(
                orderId: order.Id,
                amount: order.TotalAmount,
                orderInfo: $"Thanh toan don hang #{order.Id}",
                returnUrl: dto.ReturnUrl,
                ipAddr: ipAddr
            );

            return Ok(new { payUrl });
        }

        // GET: api/payment/vnpay/callback
        [HttpGet("vnpay/callback")]
        [AllowAnonymous]
        public async Task<ActionResult> VnPayCallback()
        {
            var query = Request.Query;
            var isValid = _vnPay.ValidateSignature(query, out var txnRef, out var responseCode, out var amountRaw);

            if (!isValid)
                return BadRequest(new { success = false, message = "Chữ ký không hợp lệ" });

            var orderId = int.Parse(txnRef.Split('_')[0]);

            var order = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                return NotFound(new { success = false, message = "Không tìm thấy đơn hàng" });

            if (order.Payment != null)
                return Ok(new { success = true, message = "Đã thanh toán trước đó", orderId });

            if (responseCode == "00")
            {
                var payment = new Payment
                {
                    OrderId = orderId,
                    Amount = amountRaw / 100m,
                    Method = "VNPAY",
                    Note = $"VNPAY TxnRef={txnRef}",
                    PaymentDate = DateTime.UtcNow
                };

                _context.Payments.Add(payment);
                order.Status = "Completed";

                if (order.Table != null)
                {
                    var otherActive = await _context.Orders
                        .AnyAsync(o => o.TableId == order.TableId && o.Id != order.Id
                            && o.Status != "Completed" && o.Status != "Cancelled");
                    if (!otherActive)
                        order.Table.Status = "Available";
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Thanh toán thành công", orderId, responseCode });
            }
            else
            {
                return Ok(new { success = false, message = "Thanh toán thất bại hoặc bị hủy", orderId, responseCode });
            }
        }

        // GET: api/payment/order/{orderId}
        [HttpGet("order/{orderId}")]
        [Authorize]
        public async Task<ActionResult> GetPaymentByOrder(int orderId)
        {
            var payment = await _context.Payments
                .Include(p => p.Order)
                .FirstOrDefaultAsync(p => p.OrderId == orderId);
            if (payment == null) return NotFound(new { message = "Chưa có thanh toán cho đơn này" });
            return Ok(payment);
        }

        // GET: api/payment  (Admin only)
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantAPI.Data;
using RestaurantAPI.DTOs;
using RestaurantAPI.Models;
using System.Security.Claims;

namespace RestaurantAPI.Controllers
{
    [Route("api/order")]
    [ApiController]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly AppDbContext _context;
        public OrderController(AppDbContext context) { _context = context; }

        // GET: api/order?status=Pending  (Staff+)
        [HttpGet]
        public async Task<ActionResult> GetOrders([FromQuery] string? status)
        {
            var query = _context.Orders
                .Include(o => o.Table)
                .Include(o => o.Staff)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Food)
                .Include(o => o.Payment)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();
            return Ok(orders);
        }

        // GET: api/order/{id}  (Staff+)
        [HttpGet("{id}")]
        public async Task<ActionResult> GetOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.Staff)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Food)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });
            return Ok(order);
        }

        // POST: api/order  (Staff+)
        [HttpPost]
        public async Task<ActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (dto.Items == null || !dto.Items.Any())
                return BadRequest(new { message = "Đơn hàng phải có ít nhất 1 món" });

            // Kiểm tra bàn
            var table = await _context.Tables.FindAsync(dto.TableId);
            if (table == null) return BadRequest(new { message = "Bàn không tồn tại" });
            if (table.Status == "Occupied")
                return BadRequest(new { message = $"{table.TableNumber} đang có khách, không thể tạo đơn mới" });

            // Kiểm tra món ăn
            var foodIds = dto.Items.Select(i => i.FoodId).ToList();
            var foods = await _context.Foods.Where(f => foodIds.Contains(f.Id)).ToListAsync();

            foreach (var item in dto.Items)
            {
                var food = foods.FirstOrDefault(f => f.Id == item.FoodId);
                if (food == null) return BadRequest(new { message = $"Không tìm thấy món ăn ID={item.FoodId}" });
                if (!food.IsAvailable) return BadRequest(new { message = $"Món '{food.Name}' hiện không phục vụ" });
            }

            var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var orderCode = await GenerateOrderCode();

            var order = new Order
            {
                OrderCode = orderCode,
                TableId = dto.TableId,
                StaffId = staffId,
                Note = dto.Note,
                Status = "Pending",
                OrderDate = DateTime.UtcNow
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            decimal total = 0;
            foreach (var item in dto.Items)
            {
                var food = foods.First(f => f.Id == item.FoodId);
                var detail = new OrderDetail
                {
                    OrderId = order.Id,
                    FoodId = item.FoodId,
                    Quantity = item.Quantity,
                    UnitPrice = food.Price,
                    Note = item.Note
                };
                _context.OrderDetails.Add(detail);
                total += detail.Quantity * detail.UnitPrice;
            }

            order.TotalAmount = total;

            // Cập nhật trạng thái bàn -> Occupied
            table.Status = "Occupied";

            await _context.SaveChangesAsync();

            // Load lại đầy đủ
            var result = await _context.Orders
                .Include(o => o.Table)
                .Include(o => o.Staff)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Food)
                .FirstAsync(o => o.Id == order.Id);

            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, result);
        }

        // PATCH: api/order/{id}/status  (Staff+)
        [HttpPatch("{id}/status")]
        public async Task<ActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            var order = await _context.Orders.Include(o => o.Table).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });

            var validStatuses = new[] { "Pending", "Confirmed", "Serving", "Completed", "Cancelled" };
            if (!validStatuses.Contains(dto.Status))
                return BadRequest(new { message = "Trạng thái không hợp lệ" });

            // Kiểm tra quyền: chỉ Admin mới hủy đơn
            if (dto.Status == "Cancelled" && !User.IsInRole("Admin"))
                return Forbid();

            if (order.Status == "Completed" || order.Status == "Cancelled")
                return BadRequest(new { message = "Không thể thay đổi trạng thái đơn đã hoàn thành hoặc đã hủy" });

            order.Status = dto.Status;

            // Tự động giải phóng bàn khi hoàn thành hoặc hủy
            if ((dto.Status == "Completed" || dto.Status == "Cancelled") && order.Table != null)
            {
                var otherActiveOrders = await _context.Orders
                    .AnyAsync(o => o.TableId == order.TableId && o.Id != id
                        && o.Status != "Completed" && o.Status != "Cancelled");
                if (!otherActiveOrders)
                    order.Table.Status = "Available";
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật trạng thái thành công", status = order.Status });
        }

        // DELETE: api/order/{id}  (Admin only - chỉ xóa đơn Pending)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders.Include(o => o.Table).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });
            if (order.Status != "Pending")
                return BadRequest(new { message = "Chỉ có thể xóa đơn hàng ở trạng thái Pending" });

            var otherActive = await _context.Orders
                .AnyAsync(o => o.TableId == order.TableId && o.Id != id
                    && o.Status != "Completed" && o.Status != "Cancelled");
            if (!otherActive && order.Table != null)
                order.Table.Status = "Available";

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Xóa đơn hàng thành công" });
        }

        private async Task<string> GenerateOrderCode()
        {
            var date = DateTime.UtcNow.ToString("yyyyMMdd");
            var count = await _context.Orders.CountAsync(o => o.OrderCode.StartsWith($"HD{date}")) + 1;
            return $"HD{date}{count:D4}";
        }
    }
}

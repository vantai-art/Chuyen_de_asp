using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantAPI.Data;
using RestaurantAPI.Models;
using System.Security.Claims;

namespace RestaurantAPI.Controllers
{
    [Route("api/notification")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _context;
        public NotificationController(AppDbContext context) { _context = context; }

        // GET: api/notification  (Lấy thông báo của user đang đăng nhập)
        [HttpGet]
        public async Task<ActionResult> GetMyNotifications([FromQuery] bool unreadOnly = false)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var query = _context.Notifications
                .Where(n => n.UserId == userId || n.UserId == null)  // Của mình hoặc broadcast
                .AsQueryable();

            if (unreadOnly) query = query.Where(n => !n.IsRead);

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            return Ok(new
            {
                Total = notifications.Count,
                UnreadCount = notifications.Count(n => !n.IsRead),
                Data = notifications
            });
        }

        // PATCH: api/notification/{id}/read  (Đánh dấu đã đọc)
        [HttpPatch("{id}/read")]
        public async Task<ActionResult> MarkRead(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && (n.UserId == userId || n.UserId == null));

            if (notification == null) return NotFound(new { message = "Không tìm thấy thông báo" });
            notification.IsRead = true;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã đọc" });
        }

        // PATCH: api/notification/read-all  (Đánh dấu tất cả đã đọc)
        [HttpPatch("read-all")]
        public async Task<ActionResult> MarkAllRead()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var notifications = await _context.Notifications
                .Where(n => (n.UserId == userId || n.UserId == null) && !n.IsRead)
                .ToListAsync();

            notifications.ForEach(n => n.IsRead = true);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Đã đọc {notifications.Count} thông báo" });
        }

        // POST: api/notification/broadcast  (Admin - gửi thông báo cho tất cả)
        [HttpPost("broadcast")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Broadcast([FromBody] BroadcastDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { message = "Tiêu đề và nội dung không được để trống" });

            var notification = new Notification
            {
                Title = dto.Title,
                Message = dto.Message,
                Type = "System",
                UserId = null,  // null = gửi cho tất cả
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã gửi thông báo cho tất cả nhân viên" });
        }

        // GET: api/notification/unread-count  (Badge số thông báo chưa đọc)
        [HttpGet("unread-count")]
        public async Task<ActionResult> GetUnreadCount()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var count = await _context.Notifications
                .CountAsync(n => (n.UserId == userId || n.UserId == null) && !n.IsRead);
            return Ok(new { unreadCount = count });
        }
    }

    public class BroadcastDto
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}

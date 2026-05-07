using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantAPI.Data;
using RestaurantAPI.DTOs;
using RestaurantAPI.Models;
using System.Security.Claims;

namespace RestaurantAPI.Controllers
{
    [Route("api/reservation")]
    [ApiController]
    public class ReservationController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ReservationController(AppDbContext context) { _context = context; }

        // GET: api/reservation  (Staff+ - xem tất cả đặt bàn)
        [HttpGet]
        [Authorize]
        public async Task<ActionResult> GetReservations(
            [FromQuery] string? status,
            [FromQuery] DateTime? date)
        {
            var query = _context.Reservations
                .Include(r => r.Table)
                .Include(r => r.Staff)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);

            if (date.HasValue)
                query = query.Where(r => r.ReservationDate.Date == date.Value.Date);

            var list = await query.OrderBy(r => r.ReservationDate).ToListAsync();
            return Ok(list);
        }

        // GET: api/reservation/{id}
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult> GetReservation(int id)
        {
            var r = await _context.Reservations
                .Include(r => r.Table)
                .Include(r => r.Staff)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (r == null) return NotFound(new { message = "Không tìm thấy đặt bàn" });
            return Ok(r);
        }

        // POST: api/reservation  (Public - khách tự đặt bàn)
        [HttpPost]
        public async Task<ActionResult> CreateReservation([FromBody] CreateReservationDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Kiểm tra ngày giờ hợp lệ (phải đặt trước ít nhất 30 phút)
            if (dto.ReservationDate <= DateTime.UtcNow.AddMinutes(30))
                return BadRequest(new { message = "Phải đặt bàn trước ít nhất 30 phút" });

            // Kiểm tra bàn còn trống không (nếu chọn bàn cụ thể)
            if (dto.TableId.HasValue)
            {
                var table = await _context.Tables.FindAsync(dto.TableId.Value);
                if (table == null) return BadRequest(new { message = "Bàn không tồn tại" });
                if (table.Capacity < dto.GuestCount)
                    return BadRequest(new { message = $"Bàn chỉ chứa {table.Capacity} khách, bạn đặt {dto.GuestCount} khách" });

                // Kiểm tra bàn có bị đặt trùng giờ không (±2 tiếng)
                var conflict = await _context.Reservations.AnyAsync(r =>
                    r.TableId == dto.TableId &&
                    r.Status != "Cancelled" && r.Status != "Completed" && r.Status != "NoShow" &&
                    Math.Abs((r.ReservationDate - dto.ReservationDate).TotalHours) < 2);
                if (conflict)
                    return BadRequest(new { message = "Bàn này đã được đặt trong khung giờ đó" });
            }

            var reservation = new Reservation
            {
                CustomerName = dto.CustomerName,
                CustomerPhone = dto.CustomerPhone,
                CustomerEmail = dto.CustomerEmail,
                ReservationDate = dto.ReservationDate,
                GuestCount = dto.GuestCount,
                TableId = dto.TableId,
                Note = dto.Note,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetReservation), new { id = reservation.Id },
                new { message = "Đặt bàn thành công! Chúng tôi sẽ xác nhận sớm.", reservation });
        }

        // PATCH: api/reservation/{id}/status  (Staff+ - xác nhận/hủy)
        [HttpPatch("{id}/status")]
        [Authorize]
        public async Task<ActionResult> UpdateStatus(int id, [FromBody] UpdateReservationDto dto)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Table)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (reservation == null) return NotFound(new { message = "Không tìm thấy đặt bàn" });

            var validStatuses = new[] { "Confirmed", "Seated", "Completed", "Cancelled", "NoShow" };
            if (!validStatuses.Contains(dto.Status))
                return BadRequest(new { message = "Trạng thái không hợp lệ" });

            reservation.Status = dto.Status;
            if (dto.TableId.HasValue) reservation.TableId = dto.TableId;
            if (dto.Note != null) reservation.Note = dto.Note;

            // Cập nhật trạng thái bàn khi khách đến (Seated)
            if (dto.Status == "Seated" && reservation.TableId.HasValue)
            {
                var table = await _context.Tables.FindAsync(reservation.TableId.Value);
                if (table != null) table.Status = "Reserved";
            }

            // Giải phóng bàn khi hoàn thành hoặc hủy
            if ((dto.Status == "Completed" || dto.Status == "Cancelled" || dto.Status == "NoShow")
                && reservation.TableId.HasValue)
            {
                var table = await _context.Tables.FindAsync(reservation.TableId.Value);
                if (table != null && table.Status == "Reserved") table.Status = "Available";
            }

            var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (staffId != null) reservation.StaffId = int.Parse(staffId);

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật trạng thái thành công", status = reservation.Status });
        }

        // GET: api/reservation/today  (Staff - xem đặt bàn hôm nay)
        [HttpGet("today")]
        [Authorize]
        public async Task<ActionResult> GetTodayReservations()
        {
            var today = DateTime.Today;
            var list = await _context.Reservations
                .Include(r => r.Table)
                .Where(r => r.ReservationDate.Date == today)
                .OrderBy(r => r.ReservationDate)
                .ToListAsync();
            return Ok(list);
        }

        // DELETE: api/reservation/{id}  (Admin only)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteReservation(int id)
        {
            var r = await _context.Reservations.FindAsync(id);
            if (r == null) return NotFound(new { message = "Không tìm thấy đặt bàn" });
            _context.Reservations.Remove(r);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Xóa thành công" });
        }
    }
}

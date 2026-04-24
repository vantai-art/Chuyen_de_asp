using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantAPI.Data;
using RestaurantAPI.Models;

namespace RestaurantAPI.Controllers
{
    [Route("api/table")]
    [ApiController]
    public class TableController : ControllerBase
    {
        private readonly AppDbContext _context;

        // Status hợp lệ
        private static readonly string[] ValidStatuses = { "Available", "Occupied", "Reserved" };

        public TableController(AppDbContext context) { _context = context; }

        // GET: api/table?status=Available  (Public)
        [HttpGet]
        public async Task<ActionResult> GetTables([FromQuery] string? status)
        {
            var query = _context.Tables.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(t => t.Status == status);
            var tables = await query.OrderBy(t => t.Id).ToListAsync();
            return Ok(tables);
        }

        // GET: api/table/{id}  (Public)
        [HttpGet("{id}")]
        public async Task<ActionResult> GetTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound(new { message = "Không tìm thấy bàn" });
            return Ok(table);
        }

        // POST: api/table  (Admin only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> PostTable([FromBody] Table table)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(table.TableNumber))
                return BadRequest(new { message = "Tên bàn không được để trống" });

            var exists = await _context.Tables.AnyAsync(t => t.TableNumber == table.TableNumber);
            if (exists) return BadRequest(new { message = $"Bàn \"{table.TableNumber}\" đã tồn tại" });

            table.Status = "Available"; // Luôn bắt đầu ở trạng thái trống
            _context.Tables.Add(table);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetTable), new { id = table.Id }, table);
        }

        // PUT: api/table/{id}  (Admin only - sửa thông tin bàn)
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> PutTable(int id, [FromBody] Table table)
        {
            if (id != table.Id) return BadRequest(new { message = "ID không khớp" });
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (string.IsNullOrWhiteSpace(table.TableNumber))
                return BadRequest(new { message = "Tên bàn không được để trống" });

            if (!ValidStatuses.Contains(table.Status))
                return BadRequest(new { message = $"Trạng thái không hợp lệ. Chỉ chấp nhận: {string.Join(", ", ValidStatuses)}" });

            var numExists = await _context.Tables.AnyAsync(t => t.TableNumber == table.TableNumber && t.Id != id);
            if (numExists) return BadRequest(new { message = $"Bàn \"{table.TableNumber}\" đã tồn tại" });

            _context.Entry(table).State = EntityState.Modified;
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Tables.AnyAsync(t => t.Id == id))
                    return NotFound(new { message = "Không tìm thấy bàn" });
                throw;
            }
            return Ok(new { message = "Cập nhật bàn thành công", table });
        }

        // PATCH: api/table/{id}/status  (Admin + Staff - đổi trạng thái nhanh)
        [HttpPatch("{id}/status")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult> UpdateTableStatus(int id, [FromBody] UpdateStatusRequest req)
        {
            if (!ValidStatuses.Contains(req.Status))
                return BadRequest(new { message = $"Trạng thái không hợp lệ. Chỉ chấp nhận: {string.Join(", ", ValidStatuses)}" });

            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound(new { message = "Không tìm thấy bàn" });

            table.Status = req.Status;
            if (req.Note != null) table.Note = req.Note;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Đã cập nhật trạng thái bàn thành {req.Status}", table });
        }

        // DELETE: api/table/{id}  (Admin only)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound(new { message = "Không tìm thấy bàn" });

            if (table.Status != "Available")
                return Conflict(new { message = $"Không thể xóa bàn đang ở trạng thái \"{table.Status}\". Chỉ xóa được bàn trống." });

            var hasActiveOrders = await _context.Orders.AnyAsync(
                o => o.TableId == id && o.Status != "Completed" && o.Status != "Cancelled"
            );
            if (hasActiveOrders)
                return Conflict(new { message = "Bàn đang có đơn hàng chưa hoàn thành, không thể xóa" });

            _context.Tables.Remove(table);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Đã xóa bàn \"{table.TableNumber}\" thành công" });
        }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
    }
}

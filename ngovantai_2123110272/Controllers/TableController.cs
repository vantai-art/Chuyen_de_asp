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
        public TableController(AppDbContext context) { _context = context; }

        // GET: api/table?status=Available  (Public)
        [HttpGet]
        public async Task<ActionResult> GetTables([FromQuery] string? status)
        {
            var query = _context.Tables.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
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
            var exists = await _context.Tables.AnyAsync(t => t.TableNumber == table.TableNumber);
            if (exists) return BadRequest(new { message = "Số bàn đã tồn tại" });

            table.Status = "Available";
            _context.Tables.Add(table);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetTable), new { id = table.Id }, table);
        }

        // PUT: api/table/{id}  (Admin only)
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> PutTable(int id, [FromBody] Table table)
        {
            if (id != table.Id) return BadRequest(new { message = "ID không khớp" });
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var numExists = await _context.Tables.AnyAsync(t => t.TableNumber == table.TableNumber && t.Id != id);
            if (numExists) return BadRequest(new { message = "Số bàn đã tồn tại" });

            _context.Entry(table).State = EntityState.Modified;
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Tables.AnyAsync(t => t.Id == id)) return NotFound();
                throw;
            }
            return Ok(new { message = "Cập nhật bàn thành công", table });
        }

        // DELETE: api/table/{id}  (Admin only - chỉ xóa khi bàn trống)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteTable(int id)
        {
            var table = await _context.Tables.FindAsync(id);
            if (table == null) return NotFound(new { message = "Không tìm thấy bàn" });
            if (table.Status != "Available")
                return BadRequest(new { message = "Chỉ có thể xóa bàn đang trống" });

            var hasOrders = await _context.Orders.AnyAsync(o => o.TableId == id && o.Status != "Completed" && o.Status != "Cancelled");
            if (hasOrders) return BadRequest(new { message = "Bàn đang có đơn hàng chưa hoàn thành" });

            _context.Tables.Remove(table);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Xóa bàn thành công" });
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantAPI.Data;
using RestaurantAPI.Models;

namespace RestaurantAPI.Controllers
{
    [Route("api/food")]
    [ApiController]
    public class FoodController : ControllerBase
    {
        private readonly AppDbContext _context;
        public FoodController(AppDbContext context) { _context = context; }

        // GET: api/food?categoryId=1  (Public)
        [HttpGet]
        public async Task<ActionResult> GetFoods([FromQuery] int? categoryId, [FromQuery] string? search)
        {
            var query = _context.Foods.Include(f => f.Category).AsQueryable();
            if (categoryId.HasValue) query = query.Where(f => f.CategoryId == categoryId.Value);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(f => f.Name.Contains(search) || f.Description.Contains(search));

            var foods = await query.OrderBy(f => f.CategoryId).ThenBy(f => f.Name).ToListAsync();
            return Ok(foods);
        }

        // GET: api/food/available  (Public - chỉ món đang phục vụ)
        [HttpGet("available")]
        public async Task<ActionResult> GetAvailableFoods([FromQuery] int? categoryId)
        {
            var query = _context.Foods.Include(f => f.Category).Where(f => f.IsAvailable);
            if (categoryId.HasValue) query = query.Where(f => f.CategoryId == categoryId.Value);
            var foods = await query.OrderBy(f => f.CategoryId).ThenBy(f => f.Name).ToListAsync();
            return Ok(foods);
        }

        // GET: api/food/{id}  (Public)
        [HttpGet("{id}")]
        public async Task<ActionResult> GetFood(int id)
        {
            var food = await _context.Foods.Include(f => f.Category).FirstOrDefaultAsync(f => f.Id == id);
            if (food == null) return NotFound(new { message = $"Không tìm thấy món ăn ID={id}" });
            return Ok(food);
        }

        // POST: api/food  (Admin only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> PostFood([FromBody] Food food)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var catExists = await _context.Categories.AnyAsync(c => c.Id == food.CategoryId);
            if (!catExists) return BadRequest(new { message = "Danh mục không tồn tại" });

            _context.Foods.Add(food);
            await _context.SaveChangesAsync();
            await _context.Entry(food).Reference(f => f.Category).LoadAsync();
            return CreatedAtAction(nameof(GetFood), new { id = food.Id }, food);
        }

        // PUT: api/food/{id}  (Admin only)
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> PutFood(int id, [FromBody] Food food)
        {
            if (id != food.Id) return BadRequest(new { message = "ID không khớp" });
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var catExists = await _context.Categories.AnyAsync(c => c.Id == food.CategoryId);
            if (!catExists) return BadRequest(new { message = "Danh mục không tồn tại" });

            _context.Entry(food).State = EntityState.Modified;
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Foods.AnyAsync(f => f.Id == id))
                    return NotFound(new { message = "Không tìm thấy món ăn" });
                throw;
            }
            await _context.Entry(food).Reference(f => f.Category).LoadAsync();
            return Ok(new { message = "Cập nhật thành công", food });
        }

        // PATCH: api/food/{id}/toggle  (Admin only - bật/tắt phục vụ)
        [HttpPatch("{id}/toggle")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> ToggleFood(int id)
        {
            var food = await _context.Foods.FindAsync(id);
            if (food == null) return NotFound(new { message = "Không tìm thấy món ăn" });
            food.IsAvailable = !food.IsAvailable;
            await _context.SaveChangesAsync();
            return Ok(new { message = food.IsAvailable ? "Đã bật phục vụ" : "Đã tắt phục vụ", isAvailable = food.IsAvailable });
        }

        // DELETE: api/food/{id}  (Admin only)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteFood(int id)
        {
            var food = await _context.Foods.FindAsync(id);
            if (food == null) return NotFound(new { message = "Không tìm thấy món ăn" });

            var inUse = await _context.OrderDetails.AnyAsync(od => od.FoodId == id);
            if (inUse) return BadRequest(new { message = "Không thể xóa vì món đã có trong đơn hàng" });

            _context.Foods.Remove(food);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Xóa món ăn thành công" });
        }
    }
}

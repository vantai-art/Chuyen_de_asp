using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantAPI.Data;
using RestaurantAPI.Models;

namespace RestaurantAPI.Controllers
{
    [Route("api/category")]
    [ApiController]
    public class CategoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        public CategoryController(AppDbContext context) { _context = context; }

        // GET: api/category  (Public)
        [HttpGet]
        public async Task<ActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .Include(c => c.Foods)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Description,
                    FoodCount = c.Foods != null ? c.Foods.Count : 0
                })
                .OrderBy(c => c.Id)
                .ToListAsync();
            return Ok(categories);
        }

        // GET: api/category/{id}  (Public)
        [HttpGet("{id}")]
        public async Task<ActionResult> GetCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound(new { message = $"Không tìm thấy danh mục ID={id}" });
            return Ok(category);
        }

        // POST: api/category  (Admin only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> PostCategory([FromBody] Category category)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var exists = await _context.Categories.AnyAsync(c => c.Name == category.Name);
            if (exists) return BadRequest(new { message = "Tên danh mục đã tồn tại" });

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
        }

        // PUT: api/category/{id}  (Admin only)
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> PutCategory(int id, [FromBody] Category category)
        {
            if (id != category.Id) return BadRequest(new { message = "ID không khớp" });
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var nameExists = await _context.Categories.AnyAsync(c => c.Name == category.Name && c.Id != id);
            if (nameExists) return BadRequest(new { message = "Tên danh mục đã tồn tại" });

            _context.Entry(category).State = EntityState.Modified;
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Categories.AnyAsync(c => c.Id == id))
                    return NotFound(new { message = "Không tìm thấy danh mục" });
                throw;
            }
            return Ok(new { message = "Cập nhật thành công", category });
        }

        // DELETE: api/category/{id}?force=true  (Admin only)
        // - Không có món → xóa hẳn
        // - Có món, không có trong đơn hàng nào (force=false) → trả 409 gợi ý
        // - force=true → xóa hết món rồi xóa danh mục (nếu món chưa có trong đơn hàng)
        //               hoặc tắt phục vụ các món đã có trong đơn hàng rồi xóa danh mục
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteCategory(int id, [FromQuery] bool force = false)
        {
            var category = await _context.Categories
                .Include(c => c.Foods)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return NotFound(new { message = "Không tìm thấy danh mục" });

            var foods = category.Foods ?? new List<Food>();
            int foodCount = foods.Count;

            if (foodCount == 0)
            {
                // Không có món → xóa thẳng
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Xóa danh mục thành công" });
            }

            // Có món trong danh mục
            if (!force)
            {
                return Conflict(new
                {
                    message = $"Danh mục đang có {foodCount} món ăn.",
                    foodCount,
                    canForce = true,
                    suggestion = "Xóa hết món trong danh mục rồi xóa danh mục"
                });
            }

            // force=true: xóa hoặc tắt phục vụ từng món, rồi xóa danh mục
            var foodIds = foods.Select(f => f.Id).ToList();

            // Kiểm tra món nào đã có trong OrderDetails
            var foodsInOrders = await _context.OrderDetails
                .Where(od => foodIds.Contains(od.FoodId))
                .Select(od => od.FoodId)
                .Distinct()
                .ToListAsync();

            int softDeleted = 0;
            int hardDeleted = 0;

            foreach (var food in foods)
            {
                if (foodsInOrders.Contains(food.Id))
                {
                    // Đã có trong đơn hàng → tắt phục vụ thay vì xóa
                    food.IsAvailable = false;
                    // Chuyển sang danh mục mặc định (ID=1) hoặc không cần vì sẽ bị ẩn
                    softDeleted++;
                }
                else
                {
                    // Chưa có trong đơn hàng → xóa hẳn
                    _context.Foods.Remove(food);
                    hardDeleted++;
                }
            }

            await _context.SaveChangesAsync();

            // Nếu còn món soft-deleted thì không xóa được danh mục (FK constraint)
            // → Gán các món đó về danh mục khác (nếu có) hoặc giữ nguyên danh mục
            var remainingFoods = await _context.Foods.AnyAsync(f => f.CategoryId == id);
            if (remainingFoods)
            {
                return Ok(new
                {
                    message = $"Đã xóa {hardDeleted} món. {softDeleted} món đã có trong lịch sử đơn hàng được ẩn khỏi menu. Danh mục vẫn giữ lại để lưu lịch sử.",
                    categoryDeleted = false,
                    hardDeleted,
                    softDeleted
                });
            }

            // Tất cả món đã xóa hết → xóa danh mục
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return Ok(new
            {
                message = $"Đã xóa danh mục và {hardDeleted} món ăn thành công.",
                categoryDeleted = true,
                hardDeleted,
                softDeleted
            });
        }
    }
}

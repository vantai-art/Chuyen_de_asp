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

        // DELETE: api/category/{id}  (Admin only)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.Include(c => c.Foods).FirstOrDefaultAsync(c => c.Id == id);
            if (category == null) return NotFound(new { message = "Không tìm thấy danh mục" });
            if (category.Foods != null && category.Foods.Any())
                return BadRequest(new { message = "Không thể xóa vì danh mục đang có món ăn" });

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Xóa danh mục thành công" });
        }
    }
}

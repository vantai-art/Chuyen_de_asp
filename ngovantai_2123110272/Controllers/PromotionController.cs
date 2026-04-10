using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantAPI.Data;
using RestaurantAPI.DTOs;
using RestaurantAPI.Models;

namespace RestaurantAPI.Controllers
{
    [Route("api/promotion")]
    [ApiController]
    public class PromotionController : ControllerBase
    {
        private readonly AppDbContext _context;
        public PromotionController(AppDbContext context) { _context = context; }

        // GET: api/promotion  (Public - xem KM đang hoạt động)
        [HttpGet]
        public async Task<ActionResult> GetPromotions()
        {
            var now = DateTime.Now;
            var promotions = await _context.Promotions
                .Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now)
                .OrderBy(p => p.EndDate)
                .ToListAsync();
            return Ok(promotions);
        }

        // GET: api/promotion/all  (Admin - xem tất cả kể cả hết hạn)
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetAllPromotions()
        {
            var promotions = await _context.Promotions
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            return Ok(promotions);
        }

        // POST: api/promotion/apply  (Public - kiểm tra & áp dụng mã giảm giá)
        [HttpPost("apply")]
        public async Task<ActionResult> ApplyPromotion([FromBody] ApplyPromotionDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var now = DateTime.Now;
            var promo = await _context.Promotions
                .FirstOrDefaultAsync(p =>
                    p.Code.ToUpper() == dto.Code.ToUpper() &&
                    p.IsActive &&
                    p.StartDate <= now &&
                    p.EndDate >= now);

            if (promo == null)
                return BadRequest(new { message = "Mã giảm giá không hợp lệ hoặc đã hết hạn" });

            if (promo.UsageLimit.HasValue && promo.UsageCount >= promo.UsageLimit)
                return BadRequest(new { message = "Mã giảm giá đã hết lượt sử dụng" });

            if (promo.MinOrderAmount.HasValue && dto.OrderAmount < promo.MinOrderAmount)
                return BadRequest(new { message = $"Đơn hàng tối thiểu {promo.MinOrderAmount:N0}đ để dùng mã này" });

            // Tính số tiền giảm
            decimal discountAmount;
            if (promo.DiscountType == "Percent")
            {
                discountAmount = dto.OrderAmount * promo.DiscountValue / 100;
                if (promo.MaxDiscountAmount.HasValue)
                    discountAmount = Math.Min(discountAmount, promo.MaxDiscountAmount.Value);
            }
            else // Fixed
            {
                discountAmount = Math.Min(promo.DiscountValue, dto.OrderAmount);
            }

            return Ok(new
            {
                Valid = true,
                Code = promo.Code,
                PromotionName = promo.Name,
                DiscountAmount = discountAmount,
                FinalAmount = dto.OrderAmount - discountAmount,
                message = $"Áp dụng thành công! Giảm {discountAmount:N0}đ"
            });
        }

        // POST: api/promotion  (Admin - tạo KM mới)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> CreatePromotion([FromBody] CreatePromotionDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var exists = await _context.Promotions
                .AnyAsync(p => p.Code.ToUpper() == dto.Code.ToUpper());
            if (exists) return BadRequest(new { message = "Mã khuyến mãi đã tồn tại" });

            if (dto.EndDate <= dto.StartDate)
                return BadRequest(new { message = "Ngày kết thúc phải sau ngày bắt đầu" });

            var promo = new Promotion
            {
                Code = dto.Code.ToUpper(),
                Name = dto.Name,
                Description = dto.Description,
                DiscountType = dto.DiscountType,
                DiscountValue = dto.DiscountValue,
                MinOrderAmount = dto.MinOrderAmount,
                MaxDiscountAmount = dto.MaxDiscountAmount,
                UsageLimit = dto.UsageLimit,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.Promotions.Add(promo);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetPromotions), new { id = promo.Id },
                new { message = "Tạo khuyến mãi thành công", promo });
        }

        // PATCH: api/promotion/{id}/toggle  (Admin - bật/tắt KM)
        [HttpPatch("{id}/toggle")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> TogglePromotion(int id)
        {
            var promo = await _context.Promotions.FindAsync(id);
            if (promo == null) return NotFound(new { message = "Không tìm thấy khuyến mãi" });
            promo.IsActive = !promo.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { message = promo.IsActive ? "Đã bật khuyến mãi" : "Đã tắt khuyến mãi" });
        }

        // DELETE: api/promotion/{id}  (Admin)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeletePromotion(int id)
        {
            var promo = await _context.Promotions.FindAsync(id);
            if (promo == null) return NotFound(new { message = "Không tìm thấy khuyến mãi" });
            _context.Promotions.Remove(promo);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Xóa khuyến mãi thành công" });
        }
    }
}

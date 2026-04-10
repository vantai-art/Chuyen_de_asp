using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantAPI.Data;
using RestaurantAPI.DTOs;
using RestaurantAPI.Models;

namespace RestaurantAPI.Controllers
{
    [Route("api/review")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ReviewController(AppDbContext context) { _context = context; }

        // GET: api/review  (Public - xem tất cả đánh giá)
        [HttpGet]
        public async Task<ActionResult> GetReviews(
            [FromQuery] int? foodId,
            [FromQuery] int? rating,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = _context.Reviews
                .Include(r => r.Food)
                .Where(r => r.IsVisible)
                .AsQueryable();

            if (foodId.HasValue) query = query.Where(r => r.FoodId == foodId);
            if (rating.HasValue) query = query.Where(r => r.Rating == rating);

            var total = await query.CountAsync();
            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)total / pageSize),
                AverageRating = total > 0 ? await query.AverageAsync(r => (double)r.Rating) : 0,
                Data = reviews
            });
        }

        // GET: api/review/summary  (Public - thống kê đánh giá tổng quan)
        [HttpGet("summary")]
        public async Task<ActionResult> GetSummary()
        {
            var reviews = await _context.Reviews.Where(r => r.IsVisible).ToListAsync();
            if (!reviews.Any()) return Ok(new { message = "Chưa có đánh giá" });

            return Ok(new
            {
                TotalReviews = reviews.Count,
                AverageRating = Math.Round(reviews.Average(r => r.Rating), 1),
                FiveStar = reviews.Count(r => r.Rating == 5),
                FourStar = reviews.Count(r => r.Rating == 4),
                ThreeStar = reviews.Count(r => r.Rating == 3),
                TwoStar = reviews.Count(r => r.Rating == 2),
                OneStar = reviews.Count(r => r.Rating == 1)
            });
        }

        // POST: api/review  (Public - khách gửi đánh giá)
        [HttpPost]
        public async Task<ActionResult> CreateReview([FromBody] CreateReviewDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Kiểm tra đơn hàng tồn tại và đã hoàn thành
            var order = await _context.Orders.FindAsync(dto.OrderId);
            if (order == null) return BadRequest(new { message = "Không tìm thấy đơn hàng" });
            if (order.Status != "Completed")
                return BadRequest(new { message = "Chỉ đánh giá được đơn hàng đã hoàn thành" });

            // Kiểm tra đã đánh giá đơn này chưa (nếu review tổng thể)
            if (dto.FoodId == null)
            {
                var exists = await _context.Reviews.AnyAsync(r => r.OrderId == dto.OrderId && r.FoodId == null);
                if (exists) return BadRequest(new { message = "Đơn hàng này đã được đánh giá rồi" });
            }

            // Kiểm tra FoodId có trong đơn không
            if (dto.FoodId.HasValue)
            {
                var inOrder = await _context.OrderDetails
                    .AnyAsync(od => od.OrderId == dto.OrderId && od.FoodId == dto.FoodId);
                if (!inOrder) return BadRequest(new { message = "Món ăn này không có trong đơn hàng" });
            }

            var review = new Review
            {
                OrderId = dto.OrderId,
                FoodId = dto.FoodId,
                CustomerName = dto.CustomerName,
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedAt = DateTime.Now,
                IsVisible = true
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Cảm ơn bạn đã đánh giá!", review });
        }

        // PATCH: api/review/{id}/reply  (Admin - phản hồi đánh giá)
        [HttpPatch("{id}/reply")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> ReplyReview(int id, [FromBody] ReplyReviewDto dto)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound(new { message = "Không tìm thấy đánh giá" });

            review.ReplyComment = dto.ReplyComment;
            review.ReplyAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Phản hồi thành công", review });
        }

        // PATCH: api/review/{id}/toggle  (Admin - ẩn/hiện đánh giá)
        [HttpPatch("{id}/toggle")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> ToggleReview(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound(new { message = "Không tìm thấy đánh giá" });
            review.IsVisible = !review.IsVisible;
            await _context.SaveChangesAsync();
            return Ok(new { message = review.IsVisible ? "Đã hiện đánh giá" : "Đã ẩn đánh giá" });
        }
    }
}

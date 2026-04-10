using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantAPI.Models
{
    // Đánh giá món ăn / trải nghiệm (như Grab Food, ShopeeFood)
    public class Review
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }  // Review phải gắn với đơn hàng đã hoàn thành

        [ForeignKey("OrderId")]
        public Order? Order { get; set; }

        public int? FoodId { get; set; }  // Null = review tổng thể nhà hàng
        [ForeignKey("FoodId")]
        public Food? Food { get; set; }

        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; } = "Khách hàng";

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }  // 1-5 sao

        [StringLength(1000)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsVisible { get; set; } = true;  // Admin có thể ẩn review

        // Phản hồi từ nhà hàng
        [StringLength(500)]
        public string? ReplyComment { get; set; }
        public DateTime? ReplyAt { get; set; }
    }
}

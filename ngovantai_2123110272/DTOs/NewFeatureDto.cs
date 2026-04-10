using System.ComponentModel.DataAnnotations;

namespace RestaurantAPI.DTOs
{
    // ===== RESERVATION DTOs =====
    public class CreateReservationDto
    {
        [Required(ErrorMessage = "Tên khách hàng không được để trống")]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string CustomerPhone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string? CustomerEmail { get; set; }

        [Required(ErrorMessage = "Ngày giờ đặt bàn không được để trống")]
        public DateTime ReservationDate { get; set; }

        [Required]
        [Range(1, 50, ErrorMessage = "Số khách từ 1 đến 50")]
        public int GuestCount { get; set; }

        public int? TableId { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }
    }

    public class UpdateReservationDto
    {
        [Required]
        public string Status { get; set; } = string.Empty;
        // Confirmed | Seated | Completed | Cancelled | NoShow

        public int? TableId { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }
    }

    // ===== REVIEW DTOs =====
    public class CreateReviewDto
    {
        [Required]
        public int OrderId { get; set; }

        public int? FoodId { get; set; }

        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; } = "Khách hàng";

        [Required]
        [Range(1, 5, ErrorMessage = "Đánh giá từ 1 đến 5 sao")]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }
    }

    public class ReplyReviewDto
    {
        [Required]
        [StringLength(500)]
        public string ReplyComment { get; set; } = string.Empty;
    }

    // ===== PROMOTION DTOs =====
    public class CreatePromotionDto
    {
        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public string DiscountType { get; set; } = "Percent";  // Percent | Fixed

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal DiscountValue { get; set; }

        public decimal? MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public int? UsageLimit { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }
    }

    public class ApplyPromotionDto
    {
        [Required]
        public string Code { get; set; } = string.Empty;

        [Required]
        public decimal OrderAmount { get; set; }
    }
}

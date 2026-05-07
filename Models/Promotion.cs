using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantAPI.Models
{
    // Chương trình khuyến mãi / mã giảm giá (như các app đặt đồ ăn)
    public class Promotion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;  // VD: "GIAM10", "KHAI_TRUONG"

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;  // Tên chương trình KM

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [StringLength(20)]
        public string DiscountType { get; set; } = "Percent";  // Percent | Fixed

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }  // 10 (%) hoặc 50000 (VNĐ)

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinOrderAmount { get; set; }  // Đơn tối thiểu để áp dụng

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxDiscountAmount { get; set; }  // Giảm tối đa (cho loại %)

        public int? UsageLimit { get; set; }  // Giới hạn số lần dùng (null = không giới hạn)
        public int UsageCount { get; set; } = 0;  // Số lần đã dùng

        [Required]
        public DateTime StartDate { get; set; }
        [Required]
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

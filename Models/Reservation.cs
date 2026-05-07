using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantAPI.Models
{
    // Bảng đặt bàn trước (như các nhà hàng thực tế cho phép book online)
    public class Reservation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string CustomerPhone { get; set; } = string.Empty;

        [StringLength(200)]
        public string? CustomerEmail { get; set; }

        [Required]
        public DateTime ReservationDate { get; set; }  // Ngày giờ đặt bàn

        [Required]
        [Range(1, 50)]
        public int GuestCount { get; set; }  // Số lượng khách

        public int? TableId { get; set; }  // Bàn được phân công (có thể null khi mới đặt)

        [ForeignKey("TableId")]
        public Table? Table { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";
        // Pending | Confirmed | Seated | Completed | Cancelled | NoShow

        [StringLength(500)]
        public string? Note { get; set; }  // Yêu cầu đặc biệt: sinh nhật, dị ứng...

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? StaffId { get; set; }  // Nhân viên xác nhận
        [ForeignKey("StaffId")]
        public User? Staff { get; set; }
    }
}

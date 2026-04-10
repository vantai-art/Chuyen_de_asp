using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantAPI.Models
{
    // Thông báo nội bộ (nhân viên nhận thông báo đơn mới, bàn cần phục vụ...)
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Type { get; set; } = "Info";
        // NewOrder | OrderReady | TableCall | LowStock | System | Info

        public int? UserId { get; set; }  // Null = gửi cho tất cả
        [ForeignKey("UserId")]
        public User? User { get; set; }

        public int? OrderId { get; set; }  // Liên quan đến đơn hàng nào
        [ForeignKey("OrderId")]
        public Order? Order { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

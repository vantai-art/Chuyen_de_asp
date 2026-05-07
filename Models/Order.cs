using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantAPI.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string OrderCode { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Required]
        public int TableId { get; set; }

        [ForeignKey("TableId")]
        public Table? Table { get; set; }

        public int? StaffId { get; set; }

        [ForeignKey("StaffId")]
        public User? Staff { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending";
        // Pending | Confirmed | Serving | Completed | Cancelled

        [StringLength(500)]
        public string? Note { get; set; }

        public IList<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public Payment? Payment { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantAPI.Models
{
    public class OrderDetail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public int FoodId { get; set; }

        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; } // Giá tại thời điểm đặt hàng

     
        [NotMapped]
        public decimal SubTotal => Quantity * UnitPrice;

        [StringLength(255)]
        public string? Note { get; set; }

        // Navigation properties
        [ForeignKey("OrderId")]
        public Order? Order { get; set; }

        [ForeignKey("FoodId")]
        public Food? Food { get; set; }
    }
}

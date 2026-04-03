using System.ComponentModel.DataAnnotations;

namespace RestaurantAPI.DTOs
{
    public class CreateOrderDto
    {
        [Required(ErrorMessage = "Bàn không được để trống")]
        public int TableId { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        [Required(ErrorMessage = "Đơn hàng phải có ít nhất 1 món")]
        [MinLength(1, ErrorMessage = "Đơn hàng phải có ít nhất 1 món")]
        public List<CreateOrderDetailDto> Items { get; set; } = new();
    }

    public class CreateOrderDetailDto
    {
        [Required]
        public int FoodId { get; set; }

        [Required]
        [Range(1, 100, ErrorMessage = "Số lượng từ 1 đến 100")]
        public int Quantity { get; set; }

        [StringLength(255)]
        public string? Note { get; set; }
    }

    public class UpdateOrderStatusDto
    {
        [Required]
        public string Status { get; set; } = string.Empty;
    }

    public class CreatePaymentDto
    {
        [Required]
        public int OrderId { get; set; }

        [Required]
        public string Method { get; set; } = "Cash"; // Cash | Card | Transfer

        [StringLength(200)]
        public string? Note { get; set; }
    }
}

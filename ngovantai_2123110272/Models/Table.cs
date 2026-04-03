using System.ComponentModel.DataAnnotations;

namespace RestaurantAPI.Models
{
    public class Table
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string TableNumber { get; set; } = string.Empty; // "Bàn 01", "VIP 01", ...

        public int Capacity { get; set; } = 4; // Sức chứa

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Available"; // Available | Occupied | Reserved

        [StringLength(200)]
        public string? Note { get; set; }
    }
}

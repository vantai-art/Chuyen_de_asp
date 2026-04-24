using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RestaurantAPI.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // ✅ Thêm ImageUrl và Color để admin có thể lưu ảnh/màu cho danh mục
        [StringLength(2000)]
        public string? ImageUrl { get; set; }

        [StringLength(20)]
        public string? Color { get; set; }

        [JsonIgnore]
        public ICollection<Food>? Foods { get; set; }
    }
}

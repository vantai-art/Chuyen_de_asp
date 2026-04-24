using System.ComponentModel.DataAnnotations;

namespace RestaurantAPI.DTOs
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Username không được để trống")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password không được để trống")]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterDto
    {
        [Required(ErrorMessage = "Username không được để trống")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Username phải từ 3-100 ký tự")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password không được để trống")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password phải ít nhất 6 ký tự")]
        public string Password { get; set; } = string.Empty;

        [StringLength(200)]
        public string? FullName { get; set; }

        // Frontend gửi email/phone nhưng User model chưa có → nhận vào rồi ignore
        // Sau này có thể thêm vào User model nếu cần
        public string? Email { get; set; }
        public string? Phone { get; set; }

        // Không set default "Staff" nữa - controller tự xử lý
        public string? Role { get; set; }
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public int UserId { get; set; }
    }
}
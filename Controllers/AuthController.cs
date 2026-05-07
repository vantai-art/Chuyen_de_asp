using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RestaurantAPI.Data;
using RestaurantAPI.DTOs;
using RestaurantAPI.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RestaurantAPI.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // ================= LOGIN =================
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == dto.Username && u.IsActive);

                if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                    return Unauthorized(new { message = "Username hoặc mật khẩu không đúng" });

                var token = GenerateToken(user);

                return Ok(new AuthResponseDto
                {
                    Token = token,
                    Username = user.Username,
                    Role = user.Role,
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = user.Phone,
                    UserId = user.Id
                });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? "";
                Console.WriteLine("Login error: " + ex.Message + " | Inner: " + inner);
                return StatusCode(500, new { message = "Lỗi server, vui lòng thử lại." });
            }
        }

        // ================= REGISTER (Customer) =================
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var exists = await _context.Users.AnyAsync(u => u.Username == dto.Username);
                if (exists)
                    return BadRequest(new { message = "Username đã tồn tại" });

                // Mặc định CUSTOMER - chỉ Admin mới tạo được Staff/Admin
                string role = "Customer";
                var callerRole = User?.FindFirstValue(ClaimTypes.Role);

                if (!string.IsNullOrEmpty(callerRole) &&
                    callerRole == "Admin" &&
                    (dto.Role == "Admin" || dto.Role == "Staff"))
                {
                    role = dto.Role;
                }

                var user = new User
                {
                    Username = dto.Username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    Role = role,
                    FullName = dto.FullName,
                    Email = dto.Email,
                    Phone = dto.Phone,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Đăng ký tài khoản thành công",
                    userId = user.Id,
                    role = user.Role
                });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? "";
                Console.WriteLine("Register error: " + ex.Message + " | Inner: " + inner);
                return StatusCode(500, new { message = "Lỗi server khi đăng ký: " + (inner != "" ? inner : ex.Message) });
            }
        }

        // ================= REGISTER STAFF (ADMIN ONLY) =================
        [HttpPost("register-staff")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> RegisterStaff([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var exists = await _context.Users.AnyAsync(u => u.Username == dto.Username);
            if (exists)
                return BadRequest(new { message = "Username đã tồn tại" });

            var user = new User
            {
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = dto.Role == "Admin" ? "Admin" : "Staff",
                FullName = dto.FullName,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Tạo tài khoản thành công",
                userId = user.Id,
                role = user.Role
            });
        }

        // ================= GET ME =================
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult> GetMe()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Role,
                user.FullName
            });
        }

        // ================= GET ALL USERS (ADMIN) =================
        [HttpGet("users")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> GetUsers()
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Role,
                    u.FullName,
                    u.IsActive,
                    u.CreatedAt
                })
                .OrderBy(u => u.Id)
                .ToListAsync();

            return Ok(users);
        }

        // ================= TOGGLE USER =================
        [HttpPatch("users/{id}/toggle")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> ToggleUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng" });

            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (user.Id == currentUserId)
                return BadRequest(new { message = "Không thể tự khóa tài khoản của mình" });

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = user.IsActive ? "Kích hoạt thành công" : "Vô hiệu hóa thành công",
                isActive = user.IsActive
            });
        }

        // ================= GENERATE TOKEN =================
        private string GenerateToken(User user)
        {
            var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
                         ?? _config["Jwt:Key"]
                         ?? "Default_Secret_Key_123456789";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", user.FullName ?? "")
            };

            var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? _config["Jwt:Issuer"];
            var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? _config["Jwt:Audience"];

            var token = new JwtSecurityToken(
                issuer: string.IsNullOrEmpty(jwtIssuer) ? null : jwtIssuer,
                audience: string.IsNullOrEmpty(jwtAudience) ? null : jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
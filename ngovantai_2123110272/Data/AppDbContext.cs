using Microsoft.EntityFrameworkCore;
using RestaurantAPI.Models;

namespace RestaurantAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // ===== BẢNG CŨ =====
        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Food> Foods { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<Payment> Payments { get; set; }

        // ===== BẢNG MỚI =====
        public DbSet<Reservation> Reservations { get; set; }   // Đặt bàn trước
        public DbSet<Review> Reviews { get; set; }             // Đánh giá
        public DbSet<Promotion> Promotions { get; set; }       // Khuyến mãi
        public DbSet<Notification> Notifications { get; set; } // Thông báo

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ===== CẤU HÌNH BẢNG CŨ =====
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Username).IsUnique().HasDatabaseName("IX_User_Username_Unique");
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasIndex(e => e.Name).IsUnique().HasDatabaseName("IX_Category_Name_Unique");
            });

            modelBuilder.Entity<Food>(entity =>
            {
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.HasOne(f => f.Category)
                    .WithMany(c => c.Foods)
                    .HasForeignKey(f => f.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.Name).HasDatabaseName("IX_Food_Name");
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.HasOne(o => o.Table)
                    .WithMany()
                    .HasForeignKey(o => o.TableId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(o => o.Staff)
                    .WithMany()
                    .HasForeignKey(o => o.StaffId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(e => e.OrderCode).IsUnique().HasDatabaseName("IX_Order_Code_Unique");
                entity.HasIndex(e => e.OrderDate).HasDatabaseName("IX_Order_Date");
                entity.HasIndex(e => e.Status).HasDatabaseName("IX_Order_Status");
            });

            modelBuilder.Entity<OrderDetail>(entity =>
            {
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.HasOne(od => od.Order)
                    .WithMany(o => o.OrderDetails)
                    .HasForeignKey(od => od.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(od => od.Food)
                    .WithMany()
                    .HasForeignKey(od => od.FoodId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.HasOne(p => p.Order)
                    .WithOne(o => o.Payment)
                    .HasForeignKey<Payment>(p => p.OrderId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.OrderId).IsUnique().HasDatabaseName("IX_Payment_OrderId_Unique");
            });

            // ===== CẤU HÌNH BẢNG MỚI =====

            // Reservation
            modelBuilder.Entity<Reservation>(entity =>
            {
                entity.HasOne(r => r.Table)
                    .WithMany()
                    .HasForeignKey(r => r.TableId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(r => r.Staff)
                    .WithMany()
                    .HasForeignKey(r => r.StaffId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(e => e.ReservationDate).HasDatabaseName("IX_Reservation_Date");
                entity.HasIndex(e => e.Status).HasDatabaseName("IX_Reservation_Status");
                entity.HasIndex(e => e.CustomerPhone).HasDatabaseName("IX_Reservation_Phone");
            });

            // Review
            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasOne(r => r.Order)
                    .WithMany()
                    .HasForeignKey(r => r.OrderId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(r => r.Food)
                    .WithMany()
                    .HasForeignKey(r => r.FoodId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(e => e.OrderId).HasDatabaseName("IX_Review_OrderId");
                entity.HasIndex(e => e.Rating).HasDatabaseName("IX_Review_Rating");
            });

            // Promotion
            modelBuilder.Entity<Promotion>(entity =>
            {
                entity.Property(e => e.DiscountValue).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MinOrderAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MaxDiscountAmount).HasColumnType("decimal(18,2)");
                entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_Promotion_Code_Unique");
            });

            // Notification
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasOne(n => n.User)
                    .WithMany()
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(n => n.Order)
                    .WithMany()
                    .HasForeignKey(n => n.OrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(e => e.UserId).HasDatabaseName("IX_Notification_UserId");
                entity.HasIndex(e => e.IsRead).HasDatabaseName("IX_Notification_IsRead");
            });

            // ===== SEED DATA =====
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Username = "admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"), Role = "Admin", FullName = "Quản trị viên", CreatedAt = new DateTime(2025, 1, 1), IsActive = true },
                new User { Id = 2, Username = "staff1", PasswordHash = BCrypt.Net.BCrypt.HashPassword("staff123"), Role = "Staff", FullName = "Nhân viên 1", CreatedAt = new DateTime(2025, 1, 1), IsActive = true }
            );

            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Khai vị", Description = "Các món ăn khai vị" },
                new Category { Id = 2, Name = "Món chính", Description = "Các món ăn chính" },
                new Category { Id = 3, Name = "Tráng miệng", Description = "Các món tráng miệng" },
                new Category { Id = 4, Name = "Đồ uống", Description = "Nước uống các loại" }
            );

            modelBuilder.Entity<Food>().HasData(
                new Food { Id = 1, Name = "Gỏi cuốn tôm thịt", Description = "Gỏi cuốn tươi nhân tôm thịt", Price = 45000, IsAvailable = true, CategoryId = 1, ImageUrl = "https://images.unsplash.com/photo-1563245372-f21724e3856d?w=400" },
                new Food { Id = 2, Name = "Chả giò chiên", Description = "Chả giò giòn tan, nhân thịt heo", Price = 55000, IsAvailable = true, CategoryId = 1, ImageUrl = "https://images.unsplash.com/photo-1529693662653-9d480da3337e?w=400" },
                new Food { Id = 3, Name = "Cơm sườn nướng", Description = "Cơm tấm sườn nướng than hoa", Price = 75000, IsAvailable = true, CategoryId = 2, ImageUrl = "https://images.unsplash.com/photo-1516714435131-44d6b64dc6a2?w=400" },
                new Food { Id = 4, Name = "Phở bò tái nạm", Description = "Phở bò truyền thống tái nạm", Price = 70000, IsAvailable = true, CategoryId = 2, ImageUrl = "https://images.unsplash.com/photo-1582878826629-29b7ad1cdc43?w=400" },
                new Food { Id = 5, Name = "Bún bò Huế", Description = "Bún bò Huế cay đặc trưng miền Trung", Price = 65000, IsAvailable = true, CategoryId = 2, ImageUrl = "https://images.unsplash.com/photo-1585032226651-759b368d7246?w=400" },
                new Food { Id = 6, Name = "Chè ba màu", Description = "Chè đậu xanh, đỏ, thạch rau câu", Price = 35000, IsAvailable = true, CategoryId = 3, ImageUrl = "https://images.unsplash.com/photo-1563805042-7684c019e1cb?w=400" },
                new Food { Id = 7, Name = "Cà phê sữa đá", Description = "Cà phê phin truyền thống với sữa đặc", Price = 30000, IsAvailable = true, CategoryId = 4, ImageUrl = "https://images.unsplash.com/photo-1551030173-122aabc4489c?w=400" },
                new Food { Id = 8, Name = "Nước chanh ép", Description = "Chanh tươi ép lạnh", Price = 25000, IsAvailable = true, CategoryId = 4, ImageUrl = "https://images.unsplash.com/photo-1621506289937-a8e4df240d0b?w=400" }
            );

            modelBuilder.Entity<Table>().HasData(
                new Table { Id = 1, TableNumber = "Bàn 01", Capacity = 4, Status = "Available" },
                new Table { Id = 2, TableNumber = "Bàn 02", Capacity = 4, Status = "Available" },
                new Table { Id = 3, TableNumber = "Bàn 03", Capacity = 6, Status = "Available" },
                new Table { Id = 4, TableNumber = "Bàn 04", Capacity = 6, Status = "Available" },
                new Table { Id = 5, TableNumber = "Bàn 05", Capacity = 2, Status = "Available" },
                new Table { Id = 6, TableNumber = "VIP 01", Capacity = 8, Status = "Available" },
                new Table { Id = 7, TableNumber = "VIP 02", Capacity = 10, Status = "Available" },
                new Table { Id = 8, TableNumber = "Bàn 06", Capacity = 4, Status = "Available" }
            );

            // Seed khuyến mãi mẫu
            modelBuilder.Entity<Promotion>().HasData(
                new Promotion
                {
                    Id = 1,
                    Code = "WELCOME10",
                    Name = "Chào mừng khách mới",
                    Description = "Giảm 10% cho lần đầu tiên, đơn từ 100.000đ",
                    DiscountType = "Percent",
                    DiscountValue = 10,
                    MinOrderAmount = 100000,
                    MaxDiscountAmount = 50000,
                    UsageLimit = 100,
                    UsageCount = 0,
                    StartDate = new DateTime(2025, 1, 1),
                    EndDate = new DateTime(2026, 12, 31),
                    IsActive = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },
                new Promotion
                {
                    Id = 2,
                    Code = "GIAM50K",
                    Name = "Giảm 50.000đ cuối tuần",
                    Description = "Giảm thẳng 50.000đ cho đơn từ 300.000đ",
                    DiscountType = "Fixed",
                    DiscountValue = 50000,
                    MinOrderAmount = 300000,
                    MaxDiscountAmount = null,
                    UsageLimit = 50,
                    UsageCount = 0,
                    StartDate = new DateTime(2025, 1, 1),
                    EndDate = new DateTime(2026, 12, 31),
                    IsActive = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                }
            );
        }
    }
}

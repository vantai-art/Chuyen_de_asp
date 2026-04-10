using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RestaurantAPI.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;

if (string.IsNullOrEmpty(databaseUrl))
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string not found.");
}
else
{
    var databaseUri = new Uri(databaseUrl);
    var userInfo = databaseUri.UserInfo.Split(':');

    connectionString = $"Host={databaseUri.Host};" +
                       $"Port={databaseUri.Port};" +
                       $"Database={databaseUri.AbsolutePath.TrimStart('/')};" +
                       $"Username={userInfo[0]};" +
                       $"Password={userInfo[1]};" +
                       $"SSL Mode=Require;Trust Server Certificate=true;";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.WriteIndented = true;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? "Default_Secret_Key_123456789";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Urls.Add($"http://0.0.0.0:{port}");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = string.Empty;
});

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


// 🔥🔥🔥 SEED DATA ĐẶT Ở ĐÂY (QUAN TRỌNG) 🔥🔥🔥
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!context.Categories.Any())
    {
        var catFood = new Category { Name = "Đồ ăn" };
        var catDrink = new Category { Name = "Đồ uống" };
        var catDessert = new Category { Name = "Tráng miệng" };

        context.Categories.AddRange(catFood, catDrink, catDessert);
        context.SaveChanges();

        var foods = new List<Food>
        {
            new Food { Name = "Pizza", Price = 120000, CategoryId = catFood.Id },
            new Food { Name = "Burger", Price = 80000, CategoryId = catFood.Id },
            new Food { Name = "Trà sữa", Price = 40000, CategoryId = catDrink.Id },
            new Food { Name = "Coca Cola", Price = 20000, CategoryId = catDrink.Id },
            new Food { Name = "Kem", Price = 30000, CategoryId = catDessert.Id }
        };

        context.Foods.AddRange(foods);
        context.SaveChanges();

        var admin = new User { Username = "admin", Password = "123456", Role = "Admin" };
        var customer = new User { Username = "customer", Password = "123456", Role = "Customer" };

        context.Users.AddRange(admin, customer);
        context.SaveChanges();

        var table1 = new Table { Name = "Bàn 1", Status = "Trống" };
        var table2 = new Table { Name = "Bàn 2", Status = "Trống" };

        context.Tables.AddRange(table1, table2);
        context.SaveChanges();

        var order = new Order
        {
            UserId = customer.Id,
            TableId = table1.Id,
            TotalAmount = 200000,
            Status = "Completed"
        };

        context.Orders.Add(order);
        context.SaveChanges();

        context.OrderDetails.Add(new OrderDetail
        {
            OrderId = order.Id,
            FoodId = foods[0].Id,
            Quantity = 1,
            Price = foods[0].Price
        });

        context.SaveChanges();
    }
}

app.Run();

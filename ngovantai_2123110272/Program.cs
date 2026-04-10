using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RestaurantAPI.Data;
using RestaurantAPI.Models;
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
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var jwtKey = "Default_Secret_Key_123456789";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new()
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

app.Urls.Add($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "10000"}");

app.UseSwagger();
app.UseSwaggerUI(c => c.RoutePrefix = "");

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


// 🔥 SEED DATA
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!context.Categories.Any())
    {
        var catFood = new Category { Name = "Đồ ăn" };
        var catDrink = new Category { Name = "Đồ uống" };

        context.Categories.AddRange(catFood, catDrink);
        context.SaveChanges();

        var food1 = new Food
        {
            Name = "Pizza",
            Description = "Pizza phô mai",
            Price = 120000,
            CategoryId = catFood.Id
        };

        var food2 = new Food
        {
            Name = "Trà sữa",
            Description = "Trà sữa",
            Price = 40000,
            CategoryId = catDrink.Id
        };

        context.Foods.AddRange(food1, food2);
        context.SaveChanges();

        var user = new User
        {
            Username = "admin",
            PasswordHash = "123456",
            Role = "Admin"
        };

        context.Users.Add(user);
        context.SaveChanges();

        var table = new Table
        {
            TableNumber = "Bàn 1",
            Capacity = 4
        };

        context.Tables.Add(table);
        context.SaveChanges();

        var order = new Order
        {
            OrderCode = "ORD001",
            TableId = table.Id,
            StaffId = user.Id,
            TotalAmount = 200000,
            Status = "Completed"
        };

        context.Orders.Add(order);
        context.SaveChanges();

        context.OrderDetails.Add(new OrderDetail
        {
            OrderId = order.Id,
            FoodId = food1.Id,
            Quantity = 2,
            UnitPrice = food1.Price
        });

        context.SaveChanges();
    }
}

app.Run();

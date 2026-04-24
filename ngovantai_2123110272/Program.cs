using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RestaurantAPI.Data;
using System.Text;

// Bắt buộc với Npgsql + PostgreSQL: tất cả DateTime phải là UTC
// Đặt TRƯỚC mọi thứ khác
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);

var builder = WebApplication.CreateBuilder(args);

// Database connection
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
    var dbHost = databaseUri.Host;
    var dbPort = databaseUri.Port > 0 ? databaseUri.Port : 5432;
    var dbName = databaseUri.AbsolutePath.TrimStart('/');
    var dbUser = Uri.UnescapeDataString(userInfo[0]);
    var dbPass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";

    connectionString = $"Host={dbHost};" +
                       $"Port={dbPort};" +
                       $"Database={dbName};" +
                       $"Username={dbUser};" +
                       $"Password={dbPass};" +
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
    options.AddPolicy("AllowReactApp", policy =>
        policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:3001",
                "https://fe-asp-net.onrender.com"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

// JWT Configuration
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? builder.Configuration["Jwt:Key"] ?? "Default_Secret_Key_123456789";
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? builder.Configuration["Jwt:Issuer"];
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrEmpty(jwtIssuer),
            ValidateAudience = !string.IsNullOrEmpty(jwtAudience),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Restaurant API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Bearer token"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
app.Urls.Add($"http://0.0.0.0:{port}");

// ========================================================
// CORS MIDDLEWARE - PHẢI ĐẦU TIÊN - gắn header kể cả lỗi 500
// ========================================================
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers["Origin"].ToString();
    var allowedOrigins = new[]
    {
        "http://localhost:3000",
        "http://localhost:3001",
        "https://fe-asp-net.onrender.com"
    };

    if (allowedOrigins.Contains(origin))
    {
        context.Response.Headers["Access-Control-Allow-Origin"] = origin;
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS, PATCH";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With";
        context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
    }

    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 204;
        await context.Response.CompleteAsync();
        return;
    }

    await next();
});

app.UseRouting();
app.UseCors("AllowReactApp");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
    c.RoutePrefix = string.Empty;
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ========================================================
// DATABASE MIGRATION + SEED ADMIN
// ========================================================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (dbContext.Database.IsRelational())
        {
            dbContext.Database.Migrate();
            Console.WriteLine("Migration success");
        }

        // ========================================================
        // TỰ ĐỘNG THÊM CỘT Email/Phone NẾU CHƯA CÓ (an toàn 100%)
        // Cách này không phụ thuộc vào EF migration file
        // ========================================================
        try
        {
            dbContext.Database.ExecuteSqlRaw(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name='Users' AND column_name='Email'
                    ) THEN
                        ALTER TABLE ""Users"" ADD COLUMN ""Email"" VARCHAR(200) NULL;
                        RAISE NOTICE 'Added Email column';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name='Users' AND column_name='Phone'
                    ) THEN
                        ALTER TABLE ""Users"" ADD COLUMN ""Phone"" VARCHAR(20) NULL;
                        RAISE NOTICE 'Added Phone column';
                    END IF;
                END $$;
            ");
            Console.WriteLine("Email/Phone columns ensured.");
        }
        catch (Exception colEx)
        {
            Console.WriteLine("Column ensure warning: " + colEx.Message);
        }

        // Seed admin - chỉ tạo nếu chưa có, an toàn khi restart nhiều lần
        if (!dbContext.Users.Any(u => u.Username == "admin"))
        {
            dbContext.Users.Add(new RestaurantAPI.Models.User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Role = "Admin",
                FullName = "Quan tri vien",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            dbContext.SaveChanges();
            Console.WriteLine("Seed admin OK — admin / Admin@123");
        }
        else
        {
            Console.WriteLine("Admin da ton tai, bo qua seed.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Migration/Seed warning: " + ex.Message);
    }
}

app.Run();
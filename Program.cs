using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RestaurantAPI.Data;
using System.Text;

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

// ✅ CORS - AllowAnyOrigin để bypass Render proxy block
// Dùng token Bearer nên không cần AllowCredentials
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
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

// VnPay
builder.Services.AddSingleton<RestaurantAPI.Services.VnPayService>();

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

// ✅ CORS middleware thủ công - đặt ĐẦU TIÊN
// AllowAnyOrigin để Render proxy không chặn
app.Use(async (context, next) =>
{
    context.Response.Headers["Access-Control-Allow-Origin"] = "*";
    context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS, PATCH";
    context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With, Accept";
    context.Response.Headers["Access-Control-Max-Age"] = "86400";

    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 204;
        await context.Response.CompleteAsync();
        return;
    }

    await next();
});

app.UseRouting();
app.UseCors("AllowAll");

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
// DATABASE MIGRATION + SEED
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
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name='Users' AND column_name='Phone'
                    ) THEN
                        ALTER TABLE ""Users"" ADD COLUMN ""Phone"" VARCHAR(20) NULL;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name='Categories' AND column_name='ImageUrl'
                    ) THEN
                        ALTER TABLE ""Categories"" ADD COLUMN ""ImageUrl"" VARCHAR(2000) NULL;
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name='Categories' AND column_name='Color'
                    ) THEN
                        ALTER TABLE ""Categories"" ADD COLUMN ""Color"" VARCHAR(20) NULL;
                    END IF;
                END $$;
            ");
            Console.WriteLine("Columns ensured OK.");
        }
        catch (Exception colEx)
        {
            Console.WriteLine("Column ensure warning: " + colEx.Message);
        }

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
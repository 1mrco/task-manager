using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TaskManagerAPI.Data;
using TaskManagerAPI.Models;
using TaskManagerAPI.Models.Enums;
using TaskManagerAPI.Repositories;
using TaskManagerAPI.Services;
using TaskManagerAPI.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();

// Add CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Task Manager API",
        Version = "v1",
        Description = "REST API for task management with JWT authentication and role-based authorization."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// Multi-Database Provider Support (SQLite, SQL Server, PostgreSQL, InMemory)
var provider = builder.Configuration["DatabaseProvider"] ?? "";
var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=TaskManager.db";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var pLower = provider.ToLowerInvariant();
    if (pLower == "inmemory" || connStr.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
    {
        options.UseInMemoryDatabase("TaskManagerMemoryDb");
    }
    else if (pLower == "postgres" || pLower == "postgresql" || connStr.Contains("Host=") || connStr.Contains("Port="))
    {
        options.UseNpgsql(connStr);
    }
    else if (pLower == "sqlserver" || pLower == "mssql" || connStr.Contains("Server=") || connStr.Contains("localdb"))
    {
        options.UseSqlServer(connStr);
    }
    else
    {
        options.UseSqlite(connStr);
    }
});

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();

var app = builder.Build();

// Ensure Database Created & Seed Admin User
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();

        // Seed Admin User if none exists
        if (!context.Users.Any(u => u.Role == UserRole.Admin))
        {
            var adminUser = new User
            {
                Name = "System Admin",
                Email = "admin@taskmanager.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(adminUser);
            context.SaveChanges();

            // Seed Sample Tasks for Admin
            context.Tasks.AddRange(
                new TaskItem
                {
                    Title = "مراجعة تقرير متطلبات النظام PRD",
                    Description = "تأكد من تطبيق جميع الإندبوينتس والصلاحيات المطلوبة في المشروغ",
                    Status = TaskItemStatus.Completed,
                    DueDate = DateTime.UtcNow.AddDays(1),
                    CreatedAt = DateTime.UtcNow,
                    UserId = adminUser.Id
                },
                new TaskItem
                {
                    Title = "اختبار واجهة المستخدم UI/UX الجديدة",
                    Description = "فحص تصميم Glassmorphic Dashboard والتأكد من استجابة جميع الأزرار والبطاقات",
                    Status = TaskItemStatus.InProgress,
                    DueDate = DateTime.UtcNow.AddDays(2),
                    CreatedAt = DateTime.UtcNow,
                    UserId = adminUser.Id
                },
                new TaskItem
                {
                    Title = "إضافة مستخدمين تجريبيين وحماية البيانات",
                    Description = "إضافة أدوار الأدمن والمستخدم العادي واختبار حماية JWT Auth",
                    Status = TaskItemStatus.Pending,
                    DueDate = DateTime.UtcNow.AddDays(3),
                    CreatedAt = DateTime.UtcNow,
                    UserId = adminUser.Id
                }
            );
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

app.UseCors("AllowAll");
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Task Manager API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

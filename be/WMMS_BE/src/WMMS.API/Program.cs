using System.Reflection;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WMMS.API.Middlewares;
using WMMS.API.Schedulers;
using WMMS.Application.Interfaces.External;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Application.Interfaces.Services;
using WMMS.Application.Mappers;
using WMMS.Application.Services.BomRouting;
using WMMS.Application.Validators.Auth;
using WMMS.Application.Validators.BomRouting;
using WMMS.Infrastructure.Excel;
using WMMS.Infrastructure.Mail;
using WMMS.Infrastructure.Persistence;
using WMMS.Infrastructure.Persistence.Seed;
using WMMS.Infrastructure.Repositories;
using WMMS.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Context (Code-First with SQL Server)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<WmmsDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlServerOptions =>
    {
        sqlServerOptions.MigrationsAssembly(typeof(WmmsDbContext).Assembly.FullName);
    });
});

// 2. Application & Infrastructure Services (DI)
builder.Services.AddAutoMapper(typeof(BomMappingProfile).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateBomRequestValidator>();

builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// Phân hệ 1: Authentication & Session Security (AUT - UC-01..UC-05)
builder.Services.AddScoped<IAuthService, AuthService>();

// Phân hệ 3: System Administration & Multi-Role RBAC (ADM - UC-06..UC-15)
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// Phân hệ 4: Order Intake & Order Space Initialization (ORD - UC-30..UC-35)
builder.Services.AddScoped<IOrderIntakeService, OrderIntakeService>();

// Phân hệ 5: Material Breakdown & BOM Preparation (BOM - UC-38..UC-41)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IBomRepository, BomRepository>();
builder.Services.AddScoped<IRoutingRepository, RoutingRepository>();
builder.Services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IMaterialRepository, MaterialRepository>();
builder.Services.AddScoped<IOrderItemRepository, OrderItemRepository>();
builder.Services.AddScoped<IExcelBomParser, ExcelBomParser>();
builder.Services.AddScoped<IBomRoutingService, BomRoutingService>();

// Background Scheduler JOB-01: Auto-unlock expired locked accounts
builder.Services.AddHostedService<AccountUnlockBackgroundService>();

// 3. JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "WMMS_DEFAULT_FALLBACK_KEY_AT_LEAST_32_CHARS_LONG";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 4. CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("WmmsCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 5. API Controllers & JSON Formatting
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// 6. Swagger with JWT Support & XML Comments
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WMMS Web API",
        Version = "v1",
        Description = "Wooden Manufacturing Management System API - Clean Architecture & Multi-Role RBAC"
    });

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT Bearer token format: Bearer {your token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Global Exception Handling Middleware (standardized JSON ApiResponse with MSG01..MSG13)
app.UseMiddleware<GlobalExceptionMiddleware>();

// Auto-seed database during development if needed
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WMMS API v1");
    });
}

app.UseHttpsRedirection();

app.UseCors("WmmsCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

// Seed initial database records
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<WmmsDbContext>();
        await DbInitializer.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.MapControllers();

app.Run();

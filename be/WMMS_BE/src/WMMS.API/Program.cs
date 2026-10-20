using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WMMS.API.Middlewares;
using WMMS.Application.Interfaces.External;
using WMMS.Application.Interfaces.Repositories;
using WMMS.Application.Interfaces.Services;
using WMMS.Application.Mappers;
using WMMS.Application.Services.BomRouting;
using WMMS.Application.Validators.BomRouting;
using WMMS.Infrastructure.Excel;
using WMMS.Infrastructure.Persistence;
using WMMS.Infrastructure.Persistence.Seed;
using WMMS.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<WmmsDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlServerOptions =>
    {
        sqlServerOptions.MigrationsAssembly(typeof(WmmsDbContext).Assembly.FullName);
    });
});

// 2. JWT Authentication
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

// 3. CORS
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

// 4. Application & Infrastructure Services (DI)
builder.Services.AddAutoMapper(typeof(BomMappingProfile).Assembly);
builder.Services.AddValidatorsFromAssemblyContaining<CreateBomRequestValidator>();

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IBomRepository, BomRepository>();
builder.Services.AddScoped<IRoutingRepository, RoutingRepository>();
builder.Services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IMaterialRepository, MaterialRepository>();
builder.Services.AddScoped<IOrderItemRepository, OrderItemRepository>();

builder.Services.AddScoped<IExcelBomParser, ExcelBomParser>();
builder.Services.AddScoped<IBomRoutingService, BomRoutingService>();

// 5. API Controllers & JSON Formatting
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// 6. Swagger with JWT Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WMMS Web API",
        Version = "v1",
        Description = "Wooden Manufacturing Management System API"
    });

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

// Auto-seed database during development if needed
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WMMS API v1");
    });
}

app.UseMiddleware<GlobalExceptionMiddleware>();

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
        await context.Database.MigrateAsync();
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

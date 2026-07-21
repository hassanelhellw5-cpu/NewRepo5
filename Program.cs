using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QemmaProject.BackgroundTasks;
using QemmaProject.Data;
using Microsoft.AspNetCore.SignalR;   // ← ضيف السطر ده
using QemmaProject.Hubs;
using QemmaProject.Models.Payment;
using QemmaProject.Services;

var builder = WebApplication.CreateBuilder(args);
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnection))
{
    defaultConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? Environment.GetEnvironmentVariable("DEFAULT_CONNECTION");
}

if (string.IsNullOrWhiteSpace(defaultConnection))
{
    throw new InvalidOperationException(
        "Missing database connection string. Set ConnectionStrings__DefaultConnection or DEFAULT_CONNECTION before starting the API.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        defaultConnection,
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null
        )
    ));
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: Bearer {token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

var configuredJwtSecret = builder.Configuration["Jwt:Secret"] ?? Environment.GetEnvironmentVariable("JWT_SECRET");
var jwtSecret = !string.IsNullOrWhiteSpace(configuredJwtSecret) && configuredJwtSecret.Length >= 32
    ? configuredJwtSecret
    : "QemmaProjectDevelopmentOnlyJwtSecretKeyChangeMe";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = !string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"]),
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = !string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]),
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2)
    };
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddMemoryCache();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        if (configuredOrigins.Length > 0)
        {
            policy.WithOrigins(configuredOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy.AllowAnyHeader()
                .AllowAnyMethod()
                .SetIsOriginAllowed(_ => true)
                .AllowCredentials();
        }
    });
});
builder.Services.AddSignalR();
// 3. تسجيل الـ PaymentService
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<CoinWalletService>();
builder.Services.AddScoped<FantasyScoringService>();

// تسجيل الـ MatchService كـ Scoped لأنه بيعتمد على Scrapers وبايثون مش API خارجي
builder.Services.AddScoped<IMatchService, MatchService>();

// تسجيل الـ Background Worker
builder.Services.AddHostedService<EnhancedApiSyncWorker>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<EnhancedApiSyncWorker>>();
    var hubContext = provider.GetRequiredService<IHubContext<MatchHub>>();
    var configuration = provider.GetRequiredService<IConfiguration>();
    return new EnhancedApiSyncWorker(provider, logger, hubContext, configuration);
});

var app = builder.Build();

await SeedStaticAdminAccountAsync(app);

// Configure the HTTP request pipeline.
// Enabled Swagger for all environments to allow testing on Monster hosting
app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Qemma API V1");
    c.RoutePrefix = "swagger"; // This ensures it opens at /swagger
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<QemmaProject.Hubs.MatchHub>("/matchHub");
app.MapGet("/", () => Results.Redirect("/swagger")); // Redirect root to swagger for convenience

app.Run();

static async Task SeedStaticAdminAccountAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StaticAdminSeeder");

    const string adminRole = "Admin";
    if (!await roleManager.RoleExistsAsync(adminRole))
    {
        await roleManager.CreateAsync(new IdentityRole(adminRole));
    }

    var email = configuration["AdminAccount:Email"] ?? Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@qemma.local";
    var userName = configuration["AdminAccount:UserName"] ?? Environment.GetEnvironmentVariable("ADMIN_USERNAME") ?? "admin";
    var password = configuration["AdminAccount:Password"] ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@123456";

    var admin = await userManager.FindByEmailAsync(email) ?? await userManager.FindByNameAsync(userName);
    if (admin == null)
    {
        admin = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            QemmaCoinsBalance = 0m
        };

        var createResult = await userManager.CreateAsync(admin, password);
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed to seed static admin account: {Errors}", string.Join(" | ", createResult.Errors.Select(e => e.Description)));
            return;
        }
    }

    if (!await userManager.IsInRoleAsync(admin, adminRole))
    {
        await userManager.AddToRoleAsync(admin, adminRole);
    }
}

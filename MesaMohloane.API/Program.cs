using MesaMohloane.API.Data;
using MesaMohloane.API.Models;
using MesaMohloane.API.Services.Auditing;
using MesaMohloane.API.Services.Email;
using MesaMohloane.API.Services.InvoiceValidation;
using MesaMohloane.API.Services.TenderEvaluation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ========================
// DATABASE
// ========================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ========================
// IDENTITY
// ========================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ========================
// JWT AUTHENTICATION
// ========================
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "MesaMohloaneSuperSecretKey2024LesothoInfra!@#$";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "MesaMohloane.API",
        ValidAudience = jwtSettings["Audience"] ?? "MesaMohloane.Client",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

// ========================
// AUTHORIZATION POLICIES
// ========================
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CitizenOnly", policy => policy.RequireRole("Citizen"))
    .AddPolicy("ContractorOnly", policy => policy.RequireRole("Contractor"))
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
    .AddPolicy("AuditorOnly", policy => policy.RequireRole("Auditor"))
    .AddPolicy("AdminOrAuditor", policy => policy.RequireRole("Admin", "Auditor"));

// ========================
// APPLICATION SERVICES
// ========================
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IInvoiceValidationService, InvoiceValidationService>();
builder.Services.AddScoped<ITenderEvaluationService, TenderEvaluationService>();

// ========================
// CONTROLLERS + SWAGGER
// ========================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Mesa-Mohloane API",
        Version = "v1",
        Description = "Infrastructure Incident Reporting & Tender Management System for the Kingdom of Lesotho"
    });

    // JWT Bearer auth in Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by a space and your JWT token."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

// ========================
// CORS (allow MVC client)
// ========================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMvcClient", policy =>
    {
        policy.WithOrigins(
                builder.Configuration["ClientUrl"] ?? "https://localhost:7001")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// ========================
// MIDDLEWARE PIPELINE
// ========================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Mesa-Mohloane API v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowMvcClient");
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();
app.MapControllers();

// ========================
// SEED ROLES ON STARTUP
// ========================
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<MesaMohloane.API.Models.ApplicationUser>>();
    
    string[] roles = { "Citizen", "Contractor", "Admin", "Auditor" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Seed Default Admin
    if (await userManager.FindByEmailAsync("admin@mesa-mohloane.co.ls") == null)
    {
        var admin = new MesaMohloane.API.Models.ApplicationUser 
        { 
            UserName = "admin@mesa-mohloane.co.ls", 
            Email = "admin@mesa-mohloane.co.ls",
            FullName = "System Administrator"
        };
        var result = await userManager.CreateAsync(admin, "Admin123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }

    // Seed Default Auditor
    if (await userManager.FindByEmailAsync("auditor@mesa-mohloane.co.ls") == null)
    {
        var auditor = new MesaMohloane.API.Models.ApplicationUser 
        { 
            UserName = "auditor@mesa-mohloane.co.ls", 
            Email = "auditor@mesa-mohloane.co.ls",
            FullName = "Chief Auditor"
        };
        var result = await userManager.CreateAsync(auditor, "Auditor123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(auditor, "Auditor");
        }
    }
}

app.Run();

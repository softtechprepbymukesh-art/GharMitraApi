using System.Reflection;
using System.Text;
using GharMitra.Api.Security;
using GharMitra.Application.Auth.Interfaces;
using GharMitra.Application.Categories.Interfaces;
using GharMitra.Application.Common;
using GharMitra.Application.Expenses.Interfaces;
using GharMitra.Application.Families.Interfaces;
using GharMitra.Application.Incomes.Interfaces;
using GharMitra.Infrastructure.Data;
using GharMitra.Infrastructure.Security;
using GharMitra.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddExceptionHandler<GharMitra.Api.Security.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GharMitra API",
        Version = "v1",
        Description = "Family finance and financial planning"
    });

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the accessToken returned by /api/v1/auth/login."
    };

    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = new List<string>()
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is missing.");
}

// Direct ADO.NET access: the database and tables must already exist
// (run the scripts in Database/ in order). The app never creates or alters schema.
builder.Services.AddSingleton<ISqlConnectionFactory>(
    new SqlConnectionFactory(connectionString));

// ---- Authentication -------------------------------------------------------
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                 ?? new JwtOptions();
jwtOptions.Validate();

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// Every endpoint requires a token unless it opts out with [AllowAnonymous].
// Secure by default: a new controller cannot be forgotten into being public.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ---- Application services -------------------------------------------------
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IFamilyService, FamilyService>();
builder.Services.AddScoped<IIncomeService, IncomeService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<GharMitra.Application.Planning.Interfaces.IDashboardService, DashboardService>();

// ---- CORS -----------------------------------------------------------------
// An explicit allow-list. AllowAnyOrigin cannot be combined with credentials
// and would expose the API to any site once tokens are in play.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("GharMitraClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Read-only connectivity probe: logs the result but never creates or changes any database object.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Database");

    try
    {
        var factory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
        await using var connection = await factory.OpenConnectionAsync();
        logger.LogInformation(
            "Connected to SQL Server '{Server}', database '{Database}'.",
            connection.DataSource,
            connection.Database);
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Cannot connect to the database. Check ConnectionStrings:DefaultConnection and run the scripts in Database/.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "GharMitra API v1");
        c.RoutePrefix = string.Empty;
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();
app.UseCors("GharMitraClient");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Exposed so integration tests can reference the entry point assembly.</summary>
public partial class Program;

using System.Reflection;
using GharMitra.Application.Categories.Interfaces;
using GharMitra.Application.Expenses.Interfaces;
using GharMitra.Application.Families.Interfaces;
using GharMitra.Application.Incomes.Interfaces;
using GharMitra.Infrastructure.Data;
using GharMitra.Infrastructure.Services;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GharMitra API",
        Version = "v1",
        Description = "Household income and expense management for families"
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
// (run Database/01_Schema.sql once). The app never creates or alters schema.
builder.Services.AddSingleton<ISqlConnectionFactory>(
    new SqlConnectionFactory(connectionString));

builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IFamilyService, FamilyService>();
builder.Services.AddScoped<IIncomeService, IncomeService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
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
            "Cannot connect to the database. Check ConnectionStrings:DefaultConnection and run Database/01_Schema.sql.");
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

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();

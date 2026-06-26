using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using SalesInvoice.Api.Security;
using SalesInvoice.Application.Tools;
using SalesInvoice.Infrastructure.Persistence;
using SalesInvoice.Infrastructure.Tools;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddControllers();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p =>
        p.WithOrigins(builder.Configuration["FrontendOrigin"] ?? "http://localhost:5173")
         .AllowAnyHeader()
         .AllowAnyMethod()));

// Tool handlers (scoped — use AppDbContext)
builder.Services.AddScoped<ResolveCustomerHandler>();
builder.Services.AddScoped<GetPurchaseHistoryHandler>();
builder.Services.AddScoped<ValidateInventoryHandler>();
builder.Services.AddScoped<ResolveDiscountHandler>();
builder.Services.AddScoped<BuildDraftHandler>();
builder.Services.AddScoped<RecordStepHandler>();
builder.Services.AddScoped<RecommendProductsHandler>();
builder.Services.AddScoped<SaveRecommendationHandler>();
builder.Services.AddScoped<SearchProductsHandler>();

builder.Services.AddHttpClient("AiEngine", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AiEngineUrl"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromSeconds(120);
});

var app = builder.Build();

// --- Migrate + seed ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

// --- Middleware pipeline ---
app.UseCors();
app.UseMiddleware<EngineTokenMiddleware>();
app.MapControllers();

app.Run();

public partial class Program { }

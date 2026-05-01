using Qaly.Application;
using Qaly.Infrastructure;
using Qaly.Infrastructure.Data.Seeds;

var builder = WebApplication.CreateBuilder(args);

// =============================================
// Service Registration
// =============================================

// Application layer (Services, Validators)
builder.Services.AddApplication();

// Infrastructure layer (DbContext, Repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Data Seeder
builder.Services.AddScoped<DataSeeder>();

// Razor Pages
builder.Services.AddRazorPages();

// SignalR (realtime notifications)
builder.Services.AddSignalR();

var app = builder.Build();

// =============================================
// Database Migration & Seed (Development only)
// =============================================

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    await seeder.SeedAsync();
}

// =============================================
// Middleware Pipeline
// =============================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// SignalR hubs
// app.MapHub<NotificationHub>("/hubs/notification");

app.Run();

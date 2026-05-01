using Qaly.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// =============================================
// Service Registration
// =============================================

// Infrastructure layer (DbContext, Repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Razor Pages
builder.Services.AddRazorPages();

// SignalR (realtime notifications)
builder.Services.AddSignalR();

var app = builder.Build();

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

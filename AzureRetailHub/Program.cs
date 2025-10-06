using AzureRetailHub.Services;
using AzureRetailHub.Settings;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Bind storage settings
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("StorageOptions"));

// Register storage helpers
builder.Services.AddSingleton<TableStorageService>();
builder.Services.AddSingleton<BlobStorageService>();
builder.Services.AddSingleton<QueueStorageService>();
builder.Services.AddSingleton<FileStorageService>();
// Register function service
builder.Services.Configure<FunctionApiOptions>(builder.Configuration.GetSection("FunctionApi"));
builder.Services.AddHttpClient<FunctionApiClient>();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// after app is built but before app.Run()
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var table = scope.ServiceProvider.GetRequiredService<TableStorageService>();
    var blob = scope.ServiceProvider.GetRequiredService<BlobStorageService>();
    var opts = scope.ServiceProvider.GetRequiredService<IOptions<AzureRetailHub.Settings.StorageOptions>>().Value;

    // run seeding (fire-and-forget but awaited here)
    await AzureRetailHub.Data.SeedData.SeedProductsAsync(table, blob, opts, force: false);
}


app.Run();

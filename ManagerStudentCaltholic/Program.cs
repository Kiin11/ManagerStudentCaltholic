using ManagerStudentCaltholic.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Addd Extensions for Database Configuration, Reverse Proxy, and Application Services
builder.Services.AddDatabaseConfiguration(builder.Configuration)
                .AddReverseProxyConfiguration()
                .AddApplicationServices();

var app = builder.Build();

// Apply database migrations automatically on application startup
app.ApplyDatabaseMigrations();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
<<<<<<< HEAD
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using Recruit_Finder_AI.Extensions;
=======
using RabbitMQ.Client;
>>>>>>> f9c0fb8 (Adding architecture to Docker, PostgreSQL, RabbitMQ and UI)
using Recruit_Finder_AI.Areas.Identity.Data;
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Extensions;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not found.");

builder.Services.AddDbContext<Recruit_Finder_AIContext>(options =>
<<<<<<< HEAD
    options.UseSqlServer(connectionString));
=======
    options.UseNpgsql(connectionString));

>>>>>>> f9c0fb8 (Adding architecture to Docker, PostgreSQL, RabbitMQ and UI)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<Recruit_Finder_AIContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

<<<<<<< HEAD
builder.Services.AddIdentityServices(builder.Configuration);
=======
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, BCryptPasswordHasher>();

builder.Services.AddHttpClient("PythonClient")
    .ConfigurePrimaryHttpMessageHandler(() => {
        var handler = new HttpClientHandler();
        if (builder.Environment.IsDevelopment())
        {
            handler.ServerCertificateCustomValidationCallback =
                (message, cert, chain, errors) => true;
        }
        return handler;
    });

builder.Services.AddHttpClient();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<NotificationService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllLocal", policy =>
    {
        var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();

        if (origins != null && origins.Length > 0)
        {
            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});
>>>>>>> f9c0fb8 (Adding architecture to Docker, PostgreSQL, RabbitMQ and UI)

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.Cookie.Name = "RecruitFinderAuth";
});

<<<<<<< HEAD
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

=======
builder.Services.AddSingleton<ConnectionFactory>(sp =>
    new ConnectionFactory()
    {
        HostName = builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq"
    });
>>>>>>> f9c0fb8 (Adding architecture to Docker, PostgreSQL, RabbitMQ and UI)

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
<<<<<<< HEAD
    try
    {
        await Seed.SeedData(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during seeding.");
=======
    var context = services.GetRequiredService<Recruit_Finder_AIContext>();

    for (int i = 0; i < 10; i++)
    {
        try
        {
            await context.Database.MigrateAsync();
            await Seed.SeedData(services);
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"The base is not ready yet (test {i + 1}/10)...");
            await Task.Delay(3000);
        }
>>>>>>> f9c0fb8 (Adding architecture to Docker, PostgreSQL, RabbitMQ and UI)
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
app.MapRazorPages();
app.MapControllers();

app.Run();
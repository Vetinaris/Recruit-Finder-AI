using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
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
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;
    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
})
.AddEntityFrameworkStores<Recruit_Finder_AIContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

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

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.Cookie.Name = "RecruitFinderAuth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None;
});

builder.Services.AddSingleton<ConnectionFactory>(sp =>
    new ConnectionFactory()
    {
        HostName = builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq"
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
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
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseCors("AllowAllLocal");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.MapControllers();

app.Run();
using Microsoft.AspNetCore.Authentication.Cookies;
using LCP.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add MVC with Razor runtime compilation if needed
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// 2. Configure Typed HttpClient for Web API
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5152";
builder.Services.AddHttpClient<ILcpApiClient, LcpApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// 3. Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Login";
        options.Cookie.Name = "LCP_Auth_Cookie";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;
    });

var app = builder.Build();

// 4. Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();

// Compartir la carpeta física de uploads guardada por LCP.Api para servir comprobantes, licencias y fotos directamente
var apiUploadsFolder = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "LCP.Api", "wwwroot", "uploads"));
if (!Directory.Exists(apiUploadsFolder))
{
    Directory.CreateDirectory(apiUploadsFolder);
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(apiUploadsFolder),
    RequestPath = "/uploads"
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Bloqueo de navegación hasta que el usuario complete el cambio obligatorio de contraseña en primer ingreso
app.Use(async (context, next) =>
{
    var user = context.User;
    if (user.Identity?.IsAuthenticated == true && user.HasClaim("DebeCambiarPassword", "true"))
    {
        var path = context.Request.Path.Value ?? "";
        if (!path.StartsWith("/Auth/PrimerIngreso", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/Auth/Logout", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/Auth/PrimerIngreso");
            return;
        }
    }
    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

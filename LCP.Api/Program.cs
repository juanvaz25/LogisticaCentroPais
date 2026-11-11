using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using LCP.Api.Data;
using LCP.Api.Hubs;
using LCP.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Logística Centro País (LCP) API",
        Version = "v1",
        Description = "Backend unificado para el Nodo Logístico de Santa Rosa, La Pampa. Expone endpoints REST y SignalR Hubs."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Encabezado de autorización JWT. Formato: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 2. Database Context setup (Sqlite by default or SqlServer via appsettings.json)
var provider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var conn = builder.Configuration.GetConnectionString("SqlServerConnection");
    builder.Services.AddDbContext<LcpDbContext>(options => options.UseSqlServer(conn));
}
else
{
    var conn = builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=LogisticaCentroPais.db";
    builder.Services.AddDbContext<LcpDbContext>(options => options.UseSqlite(conn));
}

// 3. Domain and Application Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ILiquidacionService, LiquidacionService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IChatAuthorizationService, ChatAuthorizationService>();

// 4. SignalR Real-time communication
builder.Services.AddSignalR();

// 5. JWT Authentication
var secretKey = builder.Configuration["Jwt:Key"] ?? "LCP_SuperSecret_Jwt_Key_Nodo_Logistico_Centro_Pais_2026_Santa_Rosa!";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "LogisticaCentroPais";
var audience = builder.Configuration["Jwt:Audience"] ?? "LogisticaCentroPaisClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ClockSkew = TimeSpan.Zero
    };

    // Support JWT passing for SignalR WebSockets via query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// 6. CORS Policy for MVC and Mobile apps
builder.Services.AddCors(options =>
{
    options.AddPolicy("LcpCorsPolicy", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// 7. Seed Database automatically on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LcpDbContext>();
    if (args.Contains("--reset-db"))
    {
        await DbInitializer.ResetDatabaseToDemoAsync(db);
        if (args.Contains("--exit"))
        {
            Console.WriteLine("RESET_DB_COMPLETED_SUCCESSFULLY");
            return;
        }
    }
    else
    {
        await DbInitializer.SeedAsync(db);
    }
}

// 8. Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Logística Centro País API v1");
    });
}

app.UseStaticFiles();
app.UseRouting();

app.UseCors("LcpCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapPost("/api/dev/reset-db", async (LcpDbContext db) =>
{
    await DbInitializer.ResetDatabaseToDemoAsync(db);
    return Results.Ok(new { Mensaje = "Base de datos inicializada limpiamente. Solo usuario Administrador configurado." });
}).AllowAnonymous();

// SignalR Hub endpoints
app.MapHub<PatioHub>("/hubs/patio");
app.MapHub<ChatIncidentesHub>("/hubs/chat");

app.Run();

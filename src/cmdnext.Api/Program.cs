using System.Text;
using Asp.Versioning;
using Serilog;
using Serilog.Events;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using CmdNext.Api;
using Microsoft.EntityFrameworkCore;
using CmdNext.Repository;
using CmdNext.Repository.Implementation;
using CmdNext.Service;
using CmdNext.AI.Service.Generic;

// Bootstrap logger: captures failures that happen before the host is built
// (bad configuration, DI errors) which would otherwise be lost entirely.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
Log.Information("Starting CmdNext API host");

var builder = WebApplication.CreateBuilder(args);

// Read sink/level configuration from appsettings so log behaviour is changeable
// without a rebuild.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "CmdNext.Api"));

// Bind HTTPS on 47230 unless the host already supplied URLs (ASPNETCORE_URLS / --urls).
// Port 5000 is typically used by macOS ControlCenter, so we avoid it. 47230 is deliberately
// uncommon to avoid colliding with other local repos' dev APIs (e.g. the old 7230 clashed).
// In a container ASPNETCORE_URLS is always set, so this dev-only branch is skipped and
// Kestrel serves plain HTTP behind the reverse proxy that terminates TLS.
if (string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]) &&
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ListenAnyIP(47230, listenOptions =>
        {
            listenOptions.UseHttps();
        });
    });
}

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger
builder.Services.AddSwaggerGen(c =>
{
    // Doc names must match the versioned API explorer's group names ("v1.0"),
    // otherwise the generated document resolves to zero paths.
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CmdNext AI API", Version = "v1", Description = "API for CMDNEXT AI Assistant" });

    // Each action belongs to the doc matching its API version group.
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        var groupName = apiDesc.GroupName;
        return string.IsNullOrEmpty(groupName) || string.Equals(groupName, docName, StringComparison.OrdinalIgnoreCase);
    });

    // Resolve the {version} route token so paths render as /api/v1.0/...
    c.OperationFilter<CmdNext.Api.Infrastructure.ApiVersionOperationFilter>();
    
    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, xmlFile);
    if (System.IO.File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
    
    // Add JWT Authentication support
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer {token}'",
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

// Configure API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Configure Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "CmdNextDefaultSecretKeyChangeInProduction";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Without this, "sub" is rewritten to the ClaimTypes.NameIdentifier URI and
    // User.FindFirst("sub") silently returns null on every request.
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = "sub",
        ClockSkew = TimeSpan.FromSeconds(30),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "CmdNextAPI",
        ValidAudience = jwtSettings["Audience"] ?? "CmdNextClient",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

// Configure Authorization
builder.Services.AddAuthorization();

// Configure CORS - Simple allow-all for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Configure AI Services
builder.Services.AddGenericAiServices(builder.Configuration);

// Add HttpContextAccessor (required by some services)
builder.Services.AddHttpContextAccessor();

// Configure Application Services
builder.Services.ConfigureApplicationServices(builder.Configuration);

// Configure Repository
builder.Services.ConfigureRepositoryServices(builder.Configuration);

var app = builder.Build();

// Apply any pending EF migrations before serving traffic, so a deploy carries its own
// schema changes instead of needing a manual step on the host. Runs before the request
// pipeline is exercised; a failure here aborts startup deliberately rather than leaving
// the API up and answering every query with a missing-relation error.
// Set "Database:AutoMigrate" to false to manage the schema out-of-band instead.
if (app.Configuration.GetValue<bool?>("Database:AutoMigrate") ?? true)
{
    using var migrationScope = app.Services.CreateScope();
    var dbContext = migrationScope.ServiceProvider.GetRequiredService<CmdNextDbContext>();

    var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
    {
        Log.Information("Applying {Count} pending migration(s): {Migrations}",
            pending.Count, string.Join(", ", pending));
        await dbContext.Database.MigrateAsync();
        Log.Information("Database migrations applied");
    }
    else
    {
        Log.Information("Database schema is up to date; no migrations to apply");
    }
}

// Configure the HTTP request pipeline.
// Swagger visibility is driven by configuration rather than the hosting environment:
// launching the built executable directly yields "Production" (no ASPNETCORE_ENVIRONMENT),
// which previously disabled Swagger and produced a silent 404. Set "Swagger:Enabled"
// to false to turn it off for a real deployment.
var swaggerEnabled = app.Configuration.GetValue<bool?>("Swagger:Enabled") ?? true;
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CmdNext AI API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "CmdNext AI API Documentation";
    });

    // Browsing the root should land on the docs rather than a bare 404.
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

// One summary line per HTTP request, enriched with the caller's identity.
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    options.GetLevel = (httpContext, elapsed, ex) =>
        ex != null || httpContext.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 400
                ? LogEventLevel.Warning
                : LogEventLevel.Information;

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserId", httpContext.User?.FindFirst("sub")?.Value ?? "anonymous");
    };
});

app.UseCors("AllowAll");

// Only redirect when this process terminates TLS itself. Behind the reverse proxy
// Kestrel listens on HTTP alone and redirecting would break every request.
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Open the browser to Swagger on startup in Development. Rider does not honour the
// launchBrowser flag in launchSettings.json, so the app opens it itself. Disable
// with "LaunchBrowser": false (or run in a non-Development environment).
var launchBrowser = app.Configuration.GetValue<bool?>("LaunchBrowser") ?? app.Environment.IsDevelopment();
if (launchBrowser && swaggerEnabled)
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var address = app.Urls.FirstOrDefault()
                      ?? app.Configuration["ASPNETCORE_URLS"]?.Split(';').FirstOrDefault()
                      ?? "https://localhost:47230";
        var url = $"{address.TrimEnd('/')}/swagger";

        try
        {
            OpenBrowser(url);
            Log.Information("Opened browser at {Url}", url);
        }
        catch (Exception ex)
        {
            // Never let a browser-launch failure take down the host.
            Log.Warning(ex, "Could not open a browser automatically. Browse to {Url} manually.", url);
        }
    });
}

app.Run();

static void OpenBrowser(string url)
{
    if (OperatingSystem.IsWindows())
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }
    else if (OperatingSystem.IsMacOS())
    {
        System.Diagnostics.Process.Start("open", url);
    }
    else if (OperatingSystem.IsLinux())
    {
        System.Diagnostics.Process.Start("xdg-open", url);
    }
}
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "CmdNext API host terminated unexpectedly");
    throw;
}
finally
{
    // Flush buffered events; without this the file sink can lose the final writes.
    Log.CloseAndFlush();
}

using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RikiPath.Application;
using RikiPath.Application.IClients;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Domain;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Clients;

var builder = WebApplication.CreateBuilder(args);

// 1. Bind AppSettings
var configuration = builder.Configuration;
var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>()
                  ?? configuration.Get<AppSettings>()
                  ?? new AppSettings();

builder.Services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
builder.Services.AddSingleton(appSettings);

// 2. Database Context - Npgsql using ConnectionStrings:DefaultConnection
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connection = appSettings?.ConnectionStrings?.DefaultConnection
                     ?? builder.Configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connection))
        throw new InvalidOperationException("Database connection string not configured. Please set ConnectionStrings:DefaultConnection.");

    options.UseNpgsql(connection, npgOptions =>
    {
        npgOptions.EnableRetryOnFailure();
    });

    options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.MultipleCollectionIncludeWarning));
});

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// 3. Infrastructure & Helpers
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// Helper: kiểm tra 1 class có implement 1 interface không, hỗ trợ cả open generic
// interface (vd IGenericRepository<T>) vốn không hoạt động đúng với IsAssignableFrom thông thường.
static bool ImplementsInterface(Type type, Type iface)
{
    if (!iface.IsGenericTypeDefinition)
    {
        return iface.IsAssignableFrom(type);
    }

    // iface là open generic (IGenericRepository<>) -> so sánh qua GetGenericTypeDefinition()
    if (type.IsGenericTypeDefinition)
    {
        if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == iface))
            return true;

        var baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == iface)
                return true;
            baseType = baseType.BaseType;
        }
    }

    return false;
}

// 4. Repository & Unit of Work Registration
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

var repoInterfaceAssembly = typeof(IUserAccountRepository).Assembly; // RikiPath.Application
var repoImplAssembly = typeof(UnitOfWork).Assembly;                   // RikiPath.Infrastructure

var repoInterfaces = repoInterfaceAssembly.GetTypes()
    .Where(t => t.IsInterface && t.Namespace != null && t.Namespace.EndsWith(".IRepositories"));

var repoImplementations = repoImplAssembly.GetTypes()
    .Where(t => t.IsClass && !t.IsAbstract)
    .ToList();

foreach (var iface in repoInterfaces)
{
    // Quy ước đặt tên: IXxxRepository -> XxxRepository
    var expectedName = iface.Name.TrimStart('I');
    var candidates = repoImplementations
        .Where(t => ImplementsInterface(t, iface))
        .ToList();

    var impl = candidates.FirstOrDefault(t => t.Name == expectedName)
               ?? (candidates.Count == 1 ? candidates[0] : null);

    if (impl == null)
    {
        if (candidates.Count > 1)
            throw new InvalidOperationException(
                $"Nhiều class implement {iface.FullName} nhưng không có class nào tên '{expectedName}'. " +
                $"Ứng viên: {string.Join(", ", candidates.Select(c => c.Name))}. Đổi tên class hoặc đăng ký thủ công.");

        throw new InvalidOperationException($"Không tìm thấy implementation cho {iface.FullName} trong {repoImplAssembly.GetName().Name}.");
    }

    builder.Services.AddScoped(iface, impl);
}

// 5. Service Registration (Quét cả Application và Infrastructure để tự động nhận IFileStorageService)
var applicationAssembly = typeof(ILessonProgressService).Assembly; // RikiPath.Application
var infrastructureAssembly = typeof(UnitOfWork).Assembly;           // RikiPath.Infrastructure

var serviceInterfaces = applicationAssembly.GetTypes()
    .Where(t => t.IsInterface && t.Namespace != null && t.Namespace.EndsWith(".IServices"));

var allImplementations = applicationAssembly.GetTypes()
    .Concat(infrastructureAssembly.GetTypes())
    .Where(t => t.IsClass && !t.IsAbstract)
    .ToList();

foreach (var iface in serviceInterfaces)
{
    // Quy ước đặt tên: IXxxService -> XxxService (vd IAuthService -> AuthService)
    var expectedName = iface.Name.TrimStart('I');
    var candidates = allImplementations
        .Where(t => ImplementsInterface(t, iface))
        .ToList();

    // Ưu tiên đúng tên; nếu không có, chỉ chấp nhận khi CHỈ CÓ 1 ứng viên (vd IFileStorageService
    // được implement bởi 1 class duy nhất bên Infrastructure với tên khác quy ước).
    var impl = candidates.FirstOrDefault(t => t.Name == expectedName)
               ?? (candidates.Count == 1 ? candidates[0] : null);

    if (impl == null)
    {
        if (candidates.Count > 1)
            throw new InvalidOperationException(
                $"Nhiều class implement {iface.FullName} nhưng không có class nào tên '{expectedName}'. " +
                $"Ứng viên: {string.Join(", ", candidates.Select(c => c.Name))}. Đổi tên class hoặc đăng ký thủ công.");

        throw new InvalidOperationException(
            $"Không tìm thấy implementation cho {iface.FullName} trong {applicationAssembly.GetName().Name} hoặc {infrastructureAssembly.GetName().Name}.");
    }

    builder.Services.AddScoped(iface, impl);
}

// 6. Register IClients implemented in Infrastructure.Clients
if (Type.GetType("RikiPath.Infrastructure.Clients.PayOsClient, RikiPath.Infrastructure") != null)
{
    builder.Services.AddHttpClient<IPaymentGatewayClient, PayOsClient>(client =>
    {
        if (!string.IsNullOrEmpty(builder.Configuration["PayOs:BaseUrl"]))
            client.BaseAddress = new Uri(builder.Configuration["PayOs:BaseUrl"]);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    });
}

if (Type.GetType("RikiPath.Infrastructure.Clients.AiLearningPathClient, RikiPath.Infrastructure") != null)
{
    builder.Services.AddHttpClient<IAiLearningPathClient, AiLearningPathClient>(client =>
    {
        var baseUrl = builder.Configuration["OpenAI:BaseUrl"];
        if (!string.IsNullOrEmpty(baseUrl)) client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var apiKey = builder.Configuration["OpenAI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    });
}

// 7. JWT Authentication & Authorization
var secret = appSettings?.SecretToken?.Value ?? builder.Configuration["SecretToken:Value"];
if (string.IsNullOrWhiteSpace(secret))
{
    throw new InvalidOperationException("SecretToken:Value is not configured. Configure JWT secret in appsettings.");
}

var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = false,
            ValidateAudience = false
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].FirstOrDefault();
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/signalrHub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// 8. Controllers & JSON Formatting
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 9. Swagger / OpenAPI - with JWT security
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RikiPath API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter JWT Bearer token in format: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", securityScheme);

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            securityScheme,
            Array.Empty<string>()
        }
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// 10. Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.DocumentTitle = "RikiPath API Swagger";
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "RikiPath API v1");
    });
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("DefaultCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
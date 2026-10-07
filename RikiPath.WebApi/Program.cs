using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RikiPath.Application;
using RikiPath.Application.IClients;
using RikiPath.Application.IRepositories;
using RikiPath.Application.IServices;
using RikiPath.Domain;
using RikiPath.Infrastructure;
using RikiPath.Infrastructure.Clients;
using RikiPath.WebApi.Hubs;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 1. Bind AppSettings
        var configuration = builder.Configuration;
        var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>()
                          ?? configuration.Get<AppSettings>()
                          ?? new AppSettings();

        builder.Services.Configure<AppSettings>(configuration.GetSection("AppSettings"));

        var payOsSection = configuration.GetSection("AppSettings:PayOs");
        if (!payOsSection.Exists()) payOsSection = configuration.GetSection("PayOsSettings");
        if (!payOsSection.Exists()) payOsSection = configuration.GetSection("PayOs");
        appSettings.PayOs = payOsSection.Get<PayOsSettings>() ?? appSettings.PayOs ?? new PayOsSettings();
        builder.Services.AddSingleton(appSettings);

        builder.Services.Configure<PayOsSettings>(payOsSection);
        builder.Services.Configure<AiSettings>(configuration.GetSection("Ai"));

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
        builder.Services.AddMemoryCache();

        // 3b. Firebase Admin SDK — dùng để verify idToken do React (Firebase Phone Auth) gửi lên.
        // File service-account.json tải từ Firebase Console > Project Settings > Service Accounts,
        // KHÔNG commit vào git (đặt path trong appsettings.json: "Firebase:ServiceAccountPath").
        //var firebaseCredentialPath = configuration["Firebase:ServiceAccountPath"];
        //if (string.IsNullOrWhiteSpace(firebaseCredentialPath))
        //{
        //    throw new InvalidOperationException("Thiếu cấu hình Firebase:ServiceAccountPath trong appsettings.");
        //}

        // Guard tránh init 2 lần (vd. khi hot-reload) — FirebaseApp.Create ném exception nếu đã có DefaultInstance.
        //if (FirebaseApp.DefaultInstance == null)
        //{
        //    FirebaseApp.Create(new AppOptions
        //    {
        //        Credential = GoogleCredential.FromFile(firebaseCredentialPath),
        //    });
        //}

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

        var repoInterfaceAssembly = typeof(IUserAccountRepository).Assembly;
        var repoImplAssembly = typeof(UnitOfWork).Assembly;

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

        // 5. Service Registration (Quét cả Application và Infrastructure để tự động nhận IFileStorageService,
        //    IAiUsageQuotaService, IFirebaseAuthService,...)
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
            var expectedName = iface.Name.TrimStart('I');
            var candidates = allImplementations
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

                throw new InvalidOperationException(
                    $"Không tìm thấy implementation cho {iface.FullName} trong {applicationAssembly.GetName().Name} hoặc {infrastructureAssembly.GetName().Name}.");
            }

            builder.Services.AddScoped(iface, impl);
        }

        var manuallyRegisteredClientInterfaces = new[] { typeof(IPaymentGatewayClient), typeof(IAiLearningPathClient), typeof(IAiGradingClient), typeof(IGeminiOcrClient) };

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
                var baseUrl = appSettings?.Ai?.BaseUrl;
                if (!string.IsNullOrEmpty(baseUrl)) client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var apiKey = appSettings?.Ai?.ApiKey;
                if (!string.IsNullOrWhiteSpace(apiKey))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            });
        }

        if (Type.GetType("RikiPath.Infrastructure.Clients.AiGradingClient, RikiPath.Infrastructure") != null)
        {
            builder.Services.AddHttpClient<IAiGradingClient, AiGradingClient>(client =>
            {
                var baseUrl = appSettings?.Ai?.BaseUrl;
                if (!string.IsNullOrEmpty(baseUrl)) client.BaseAddress = new Uri(baseUrl);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var apiKey = appSettings?.Ai?.ApiKey;
                if (!string.IsNullOrWhiteSpace(apiKey))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            });
        }

        if (Type.GetType("RikiPath.Infrastructure.Clients.GeminiOcrClient, RikiPath.Infrastructure") != null)
        {
            builder.Services.AddHttpClient<IGeminiOcrClient, GeminiOcrClient>();
        }

        var clientInterfaces = applicationAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Namespace != null && t.Namespace.EndsWith(".IClients"))
            .Where(t => !manuallyRegisteredClientInterfaces.Contains(t));

        foreach (var iface in clientInterfaces)
        {
            var expectedName = iface.Name.TrimStart('I');
            var candidates = allImplementations
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

                Console.WriteLine(
                    $"[WARN] Chưa có implementation cho {iface.FullName} - bỏ qua đăng ký DI. " +
                    $"Service nào inject interface này sẽ lỗi lúc chạy tới khi bạn thêm class '{expectedName}' implement nó.");
                continue;
            }

            builder.Services.AddScoped(iface, impl);
        }

        // 6c. SignalR: the legacy Consultant signaling remains separate from the Mentor meeting hub.
        builder.Services.AddSignalR(o => o.EnableDetailedErrors = builder.Environment.IsDevelopment());
        builder.Services.AddSingleton<ICallConnectionTracker, CallConnectionTracker>();

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
                            (context.HttpContext.Request.Path.StartsWithSegments("/hubs/consultation-call")
                             || context.HttpContext.Request.Path.StartsWithSegments("/hubs/mentor-meeting")))
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
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            // Truyền true vào parameter includeControllerXmlComments để hiển thị cả comment của Controller Class
            c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
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
                policy.WithOrigins("http://localhost:5173", "https://riki-path-web.vercel.app") // Đổi AllowAnyOrigin() thành domain cụ thể của Frontend
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials(); // Bắt buộc phải có để SignalR hoạt động
            });
        });
        var app = builder.Build();

        // 10. Middleware Pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/error");
            app.UseHsts();
        }
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.DocumentTitle = "RikiPath API Swagger";
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "RikiPath API v1");
        });

        app.UseHttpsRedirection();
        app.UseCors("DefaultCorsPolicy");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<MentorMeetingHub>("/hubs/mentor-meeting");

        app.Run();
    }
}
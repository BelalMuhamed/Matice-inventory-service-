
using invetoryBackGroundServices.Machine;
using invetoryBackGroundServices.Middleware;
using invetoryBackGroundServices.Options;
using invetoryBackGroundServices.Security;
using invetoryBackGroundServices.Services;
using MATICA_S3300e.LAN;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace invetoryBackGroundServices
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "InvetoryServices";
            });


            builder.Services.AddControllers(options =>
            {
                // Centralized error-message localization for every action result, same mechanism
                // as the Inventory API's filter of the same name.
                options.Filters.Add<Filters.LocalizeErrorResultFilter>();
            }).AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            // Resource files live in Resources/Localization, so the localizer must look there:
            // IStringLocalizer<Messages> then resolves Messages.resx / Messages.ar.resx by error code.
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources/Localization");

            builder.Services.Configure<RequestLocalizationOptions>(options =>
            {
                // Same two cultures as the Inventory API, so both services answer a given
                // Accept-Language identically.
                var supported = new[] { new CultureInfo("en"), new CultureInfo("ar") };
                options.DefaultRequestCulture = new RequestCulture("en");
                options.SupportedCultures = supported;
                options.SupportedUICultures = supported;

                options.RequestCultureProviders = new IRequestCultureProvider[]
                {
                    new QueryStringRequestCultureProvider(),
                    new AcceptLanguageHeaderRequestCultureProvider()
                };
            });

            // Replaces the previous AllowAnyOrigin policy. Origins come from configuration so
            // they differ per environment without a code change; an empty list fails startup
            // rather than silently reverting to permitting everything.
            CorsPolicyOptions corsOptions =
                builder.Configuration.GetSection(CorsPolicyOptions.SectionName).Get<CorsPolicyOptions>() ?? new CorsPolicyOptions();
            EnsureCorsOriginsConfigured(corsOptions);

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(corsOptions.AllowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Matica Print Flow: strongly-typed configuration (Options pattern), replacing the old
            // Configuration["WebAPI"] string-indexer / static API_Handle.Init(...) pattern.
            builder.Services.AddOptions<InventoryApiOptions>()
                .Bind(builder.Configuration.GetSection(InventoryApiOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "InventoryApi BaseUrl is required.")
                .ValidateOnStart();

            builder.Services.AddOptions<PrintAgentAuthOptions>()
                .Bind(builder.Configuration.GetSection(PrintAgentAuthOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey), "PrintAgentAuth SigningKey is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "PrintAgentAuth Issuer is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "PrintAgentAuth Audience is required.")
                .ValidateOnStart();

            EnsurePrintAgentSigningKeyPresent(builder.Configuration);

            // Matica Print Flow: validates the short-lived Print Agent token Angular hands this
            // service. A single scheme is enough here (unlike the Inventory API, which also needs
            // its own separate tenant/admin scheme) - this service only ever validates print-agent
            // tokens, nothing else.
            PrintAgentAuthOptions printAgentAuth =
                builder.Configuration.GetSection(PrintAgentAuthOptions.SectionName).Get<PrintAgentAuthOptions>()
                ?? new PrintAgentAuthOptions();

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = printAgentAuth.Issuer,
                        ValidAudience = printAgentAuth.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(printAgentAuth.SigningKey))
                    };
                });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(PrintAgentAuthPolicy.Name, policy =>
                    policy.RequireClaim(PrintAgentClaims.Purpose, PrintAgentClaims.PurposeValue));
            });

            // Logger fix (prior review, Critical): a single shared instance for the process, not
            // one constructed per request. Same constructor arguments the old per-request field
            // initializer on MachineController used - only the lifetime changes. This also fixes
            // the log-file race (the old per-request Mutex never actually serialized concurrent
            // requests against each other) and stops the log-directory scan-and-delete from
            // running on every single request instead of once at startup.
            builder.Services.AddSingleton(_ => new Logger(
                Assembly.GetExecutingAssembly().GetName().Name + "_LOG",
                Path.Combine(AppContext.BaseDirectory, "AppLog"),
                true,
                true));

            builder.Services.AddScoped<MachineConnectionClass>();
            builder.Services.AddScoped<MachineInfoJSON>();
            builder.Services.AddScoped<ActionClass>();
            builder.Services.AddScoped<CardData>();

            // Machine communication timeout: backend configuration, not per-request data.
            builder.Services.AddOptions<MachineCommunicationOptions>()
                .Bind(builder.Configuration.GetSection(MachineCommunicationOptions.SectionName))
                .Validate(o => o.TimeoutSeconds > 0, "MachineCommunication TimeoutSeconds must be greater than zero.")
                .ValidateOnStart();

            // Async machine communication, replacing the blocking HttpWebRequest pattern. The
            // transport is deliberately vendor-neutral; MaticaCommandClient holds everything
            // Matica-specific, so another device family means a new command client rather than
            // changes to communication code.
            builder.Services.AddHttpClient<IMachineTransport, MachineHttpTransport>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    // Retained deliberately: the machine presents a self-signed certificate on the
                    // LAN, exactly as the pre-existing httpPOST/httpPOSTGetInfoJson assume. The
                    // earlier review's recommendation to pin this specific device's certificate
                    // instead of accepting any still stands as a follow-up - changing it here
                    // would alter behavior this phase is meant to hold constant.
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                });

            builder.Services.AddScoped<IMaticaCommandClient, MaticaCommandClient>();

            // Matica Print Flow: typed HttpClient (not a new HttpClient() per call, unlike the old
            // API_HttpClient this replaces) for the two backend calls to the Inventory API.
            builder.Services.AddHttpClient<IPrintFlowClient, PrintFlowClient>((sp, client) =>
            {
                InventoryApiOptions options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<InventoryApiOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            });


            builder.WebHost.UseUrls("http://localhost:8403");


            var app = builder.Build();

            // Outermost, so it catches anything thrown further down the pipeline.
            app.UseMiddleware<GlobalExceptionMiddleware>();

            // Before the endpoints, so the culture is resolved by the time either the error
            // filter or the exception middleware needs to localize a message.
            app.UseRequestLocalization();

            app.UseCors();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseRouting();

            // Matica Print Flow: authentication must run before authorization so the principal is
            // established first. UseAuthorization() alone (as before) was a no-op - no
            // authentication scheme was ever registered, so nothing was actually being checked.
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }

        private static void EnsureCorsOriginsConfigured(CorsPolicyOptions corsOptions)
        {
            if (corsOptions.AllowedOrigins is null || corsOptions.AllowedOrigins.Length == 0)
            {
                throw new InvalidOperationException(
                    $"No CORS origins are configured. Set '{CorsPolicyOptions.SectionName}:" +
                    $"{nameof(CorsPolicyOptions.AllowedOrigins)}' to the exact origin(s) allowed to call " +
                    "this service (for example the Angular app's origin). This deliberately fails startup " +
                    "rather than falling back to allowing any origin, since this service can physically " +
                    "move and emboss cards.");
            }
        }

        private static void EnsurePrintAgentSigningKeyPresent(IConfiguration configuration)
        {
            string? signingKey = configuration[
                $"{PrintAgentAuthOptions.SectionName}:{nameof(PrintAgentAuthOptions.SigningKey)}"];
            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new InvalidOperationException(
                    "Print Agent token signing key is not configured. Set " +
                    $"'{PrintAgentAuthOptions.SectionName}:{nameof(PrintAgentAuthOptions.SigningKey)}' via " +
                    "user-secrets (development) or an environment variable (production). It must be the exact " +
                    "same value as the Inventory API's 'PrintAgentToken:SigningKey' - see " +
                    "PrintAgentAuthOptions's doc comment for why.");
            }
        }
    }
}


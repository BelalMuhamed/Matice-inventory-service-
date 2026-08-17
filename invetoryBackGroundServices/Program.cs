
using CLS;
using invetoryBackGroundServices.Helper;
using MATICA_S3300e.LAN;
using System.Text.Json.Serialization;

namespace invetoryBackGroundServices
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            API_Handle.Init(builder.Configuration);



            builder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "InvetoryServices";
            });

            
            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); 
            });

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddHttpClient(); 


            // TODO: Register machine service, database context, etc., if needed
            // builder.Services.AddSingleton<IMachineService, MachineService>();
            builder.Services.AddScoped<MachineConnectionClass>();
            builder.Services.AddScoped<MachineInfoJSON>();
            builder.Services.AddScoped<ActionClass>();
            builder.Services.AddScoped<CardData>();
            builder.Services.AddScoped<API_Handle>();
            builder.Services.AddScoped<APIHelper>();


            builder.WebHost.UseUrls("http://localhost:8403");






            var app = builder.Build();
            app.UseCors();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseRouting();
            app.UseAuthorization();
            app.MapControllers(); 

            app.Run();
        }
    }
}

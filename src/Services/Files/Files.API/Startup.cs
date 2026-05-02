using Files.API.Configuration;
using Files.API.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace Files.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddOptions<FileStorageOptions>()
                .Configure<IConfiguration>((options, config) =>
                {
                    config.GetSection(FileStorageOptions.SectionName).Bind(options);

                    // Обратная совместимость со старым ключом верхнего уровня StoragePath.
                    var legacyPath = config["StoragePath"];
                    if (!string.IsNullOrWhiteSpace(legacyPath))
                        options.Path = legacyPath;
                });

            services.AddSingleton<IFileStorageService, LocalFileStorageService>();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title   = "Files Service",
                    Version = "v1"
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

            app.UseSwagger()
               .UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Files.API V1"));

            app.UseRouting();
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}

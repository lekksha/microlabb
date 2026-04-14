using Factories.API.BackgroundService;
using Factories.DAL.Data;
using Factories.Domain.Services;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace Factories.API
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
            // FIX: убран ServiceLifetime.Transient
            services.AddDbContext<FactoriesDbContext>(options =>
                options.UseSqlServer(Configuration["DefaultConnection"]));
            services.AddScoped<IFactoriesService, FactoriesService>();
            // FIX: убраны ConfigureJsonSerializer/Deserializer, AddMassTransitHostedService
            services.AddMassTransit(x =>
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(new Uri("rabbitmq://rabbit/"));
                });
            });
            services.AddHostedService<UpdateShopsTimedHostedService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Factories API is running");
                });
            });
        }
    }
}

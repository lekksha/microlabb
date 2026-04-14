using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Purchases.API.Consumers;
using Purchases.DAL.Data;
using Purchases.Domain.Services;
using RtuItLab.Infrastructure.Filters;
using RtuItLab.Infrastructure.Middlewares;
using System;
using System.Collections.Generic;

namespace Purchases.API
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
            services.AddControllers(option =>
            {
                option.Filters.Add(typeof(ValidateModelAttribute));
            });
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo()
                {
                    Title   = "Purchases Service",
                    Version = "v1"
                });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = @"JWT Authorization header using the Bearer scheme.
                      Enter 'Bearer' [space] and then your token in the text input below.
                      Example: 'Bearer 12345abcdef'",
                    Name   = "Authorization",
                    In     = ParameterLocation.Header,
                    Type   = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id   = "Bearer"
                            },
                            Scheme = "oauth2",
                            Name   = "Bearer",
                            In     = ParameterLocation.Header,

                        },
                        new List<string>()
                    }
                });
            });
            // FIX: убран ServiceLifetime.Transient
            services.AddDbContext<PurchasesDbContext>(
                option => option.UseSqlServer(Configuration["DefaultConnection"]));
            services.AddScoped<IPurchasesService, PurchasesService>();
            // FIX: убраны GreenPipes, ConfigureJsonSerializer/Deserializer, AddMassTransitHostedService
            services.AddMassTransit(x =>
            {
                x.AddConsumer<AddTransaction>();
                x.AddConsumer<GetTransactionById>();
                x.AddConsumer<GetTransactions>();
                x.AddConsumer<UpdateTransaction>();
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(new Uri("rabbitmq://rabbit/"));
                    cfg.ReceiveEndpoint("purchasesQueue", e =>
                    {
                        e.PrefetchCount = 20;
                        e.UseMessageRetry(r => r.Interval(2, 100));
                        e.Consumer<AddTransaction>(context);
                        e.Consumer<GetTransactionById>(context);
                        e.Consumer<GetTransactions>(context);
                        e.Consumer<UpdateTransaction>(context);
                    });
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();
            app.UseSwagger()
                .UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Purchases.API V1");
                });
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.UseMiddleware<JwtMiddleware>();
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}

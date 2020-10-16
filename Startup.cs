namespace WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    using AutoMapper;

    using CorrelationId;

    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.HttpOverrides;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.OpenApi.Models;

    using Serilog;
    using Serilog.Events;

    using WebApi.Entities;
    using WebApi.Helpers;
    using WebApi.Services;

    public class Startup
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;

        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            _env = env;
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // use sql server db in production and sqlite db in development
            if (_env.IsProduction())
                services.AddDbContext<DataContext>();
            else
                services.AddDbContext<DataContext, SqliteDataContext>();

            services.AddCors();
            services.AddControllers();
            services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
            services.AddCorrelationId();

            // configure strongly typed settings objects
            var appSettingsSection = _configuration.GetSection("AppSettings");
            services.Configure<AppSettings>(appSettingsSection);

            // configure jwt authentication
            var appSettings = appSettingsSection.Get<AppSettings>();
            var key = Encoding.ASCII.GetBytes(appSettings.Secret);
            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(x =>
            {
                x.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                        var userId = int.Parse(context.Principal.Identity.Name);
                        var user = userService.GetById(userId);
                        if (user == null)
                        {
                            // return unauthorized if user no longer exists
                            context.Fail("Unauthorized");
                        }
                        return Task.CompletedTask;
                    },
                };
                x.RequireHttpsMetadata = false;
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
            });
            services.AddSwaggerGen(
                c =>
                    {
                        c.SwaggerDoc(
                            "v1",
                            new OpenApiInfo
                            {
                                Title = "My API",
                                Version = "v1"
                            });
                        // disable bearer auth
                        if (1 == 0)
                        {
                            c.AddSecurityDefinition(
                                "Bearer",
                                new OpenApiSecurityScheme
                                    {
                                        In = ParameterLocation.Header,
                                        Description = "Please insert JWT with Bearer into field",
                                        Name = "Authorization",
                                        Type = SecuritySchemeType.ApiKey
                                    });
                            c.AddSecurityRequirement(
                                new OpenApiSecurityRequirement
                                    {
                                        {
                                            new OpenApiSecurityScheme
                                                {
                                                    Reference = new OpenApiReference
                                                                    {
                                                                        Type = ReferenceType
                                                                            .SecurityScheme,
                                                                        Id = "Bearer"
                                                                    }
                                                },
                                            new string[] { }
                                        }
                                    });
                        }
                    });
            services.AddSwaggerGenNewtonsoftSupport();

            // configure DI for application services
            services.AddScoped<IUserService, UserService>();

            services.AddAuthorization(config =>
                {
                    config.AddPolicy(Policies.Admin, Policies.AdminPolicy());
                    config.AddPolicy(Policies.User, Policies.UserPolicy());
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
         public void Configure(IApplicationBuilder app, IWebHostEnvironment env, DataContext dataContext)
        {
            // migrate any database changes on startup (includes initial db creation)
            dataContext.Database.Migrate();
            app.UseSerilogRequestLogging();
            app.UseStaticFiles();
            app.UseDefaultFiles();
            app.UseSwagger();
            //app.UseSwaggerUI(
            //    c =>
            //        {
            //            c.SwaggerEndpoint($"/swagger/v1/swagger.json", $"API v1");
            //        }
            //    );
            app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");

                    var assembly = GetType().GetTypeInfo().Assembly;
                    var ns = assembly.GetName().Name;
                    c.IndexStream = () => assembly.GetManifestResourceStream($"{ns}.index.html");
                });
            app.UseCorrelationId();
            app.UseMiddleware<CorrelationMiddleware>();
            app.UseMiddleware<RequestResponseLoggingMiddleware>();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
                                        {
                                            ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                                               ForwardedHeaders.XForwardedProto
                                        });
            app.UseSerilogRequestLogging(options =>
                {
                    // Customize the message template
                    options.MessageTemplate = "Handled {RequestPath}";

                    // Emit debug-level events instead of the defaults
                    options.GetLevel = (httpContext, elapsed, ex) => LogEventLevel.Debug;

                    // Attach additional properties to the request completion event
                    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                        {
                            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                        };
                });
            app.UseRouting();

            // global cors policy
            app.UseCors(x => x
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}

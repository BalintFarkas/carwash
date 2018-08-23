#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SpaServices.ReactDevelopmentServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using MSHU.CarWash.ClassLibrary;
using MSHU.CarWash.PWA.Controllers;
using MSHU.CarWash.PWA.Extensions;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using MSHU.CarWash.PWA.Services;

namespace MSHU.CarWash.PWA
{
    public class Startup
    {
        private readonly List<Company> _authorizedTenants = new List<Company>
        {
            new Company(Company.Carwash, "bca200e7-1765-4001-977f-5363e5f7a63a"),
            new Company(Company.Microsoft, "72f988bf-86f1-41af-91ab-2d7cd011db47"),
            new Company(Company.Sap, ""),
            new Company(Company.Graphisoft, "")
        };

        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public IConfiguration Configuration { get; }
        private IHostingEnvironment Environment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add application services
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<UsersController, UsersController>();
            services.AddScoped<IGraphService, GraphService>();
            services.AddScoped<ICalendarService, CalendarService>();

            // Add framework services
            services.AddApplicationInsightsTelemetry(Configuration);

            services.AddDbContextPool<ApplicationDbContext>(options => options.UseSqlServer(Configuration.GetConnectionString("Database")));

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.Audience = Configuration["AzureAD:ClientId"];
                    options.Authority = Configuration["AzureAD:Instance"];
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        IssuerValidator = (issuer, token, tvp) =>
                        {
                            issuer = issuer.Substring(24, 36); // Get the tenant id out of the issuer string (eg. https://sts.windows.net/72f988bf-86f1-41af-91ab-2d7cd011db47/)
                            if (_authorizedTenants.Select(i => i.TenantId).Contains(issuer))
                                return issuer;
                            else
                                throw new SecurityTokenInvalidIssuerException("Invalid issuer");
                        }

                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = async context =>
                        {
                            //Get EF context
                            var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

                            var company = _authorizedTenants.SingleOrDefault(t => t.TenantId == context.Principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid"))?.Name ?? throw new Exception("Tenant ('tenantid') cannot be found in auth token.");
                            var email = context.Principal.FindFirstValue(ClaimTypes.Upn)?.ToLower();
                            if (email == null && company == Company.Carwash) email = context.Principal.FindFirstValue(ClaimTypes.Email)?.ToLower();
                            if (email == null) throw new Exception("Email ('upn' or 'email') cannot be found in auth token.");

                            var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == email);

                            if (user == null)
                            {
                                user = new User
                                {
                                    FirstName = context.Principal.FindFirstValue(ClaimTypes.GivenName) ?? throw new Exception("First name ('givenname') cannot be found in auth token."),
                                    LastName = context.Principal.FindFirstValue(ClaimTypes.Surname),
                                    Email = email,
                                    Company = company,
                                    IsAdmin = false,
                                    IsCarwashAdmin = company == Company.Carwash
                                };
                                await dbContext.Users.AddAsync(user);
                                await dbContext.SaveChangesAsync();
                            }

                            var claims = new List<Claim>
                            {
                                new Claim("admin", user.IsAdmin.ToString()),
                                new Claim("carwashadmin", user.IsCarwashAdmin.ToString())
                            };
                            context.Principal.AddIdentity(new ClaimsIdentity(claims));

                            // Save Graph access token to the service
                            var accessToken = context.Request.Headers["X-Graph-Token"];
                            var graphService = context.HttpContext.RequestServices.GetRequiredService<IGraphService>();
                            graphService.SaveAccessToken(accessToken);
                        },
                        OnAuthenticationFailed = context =>
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            context.Response.ContentType = "application/json";
                            context.Response.Body = null;
                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            // In production, the React files will be served from this directory
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "ClientApp/build";
            });

            // Add gzip compression
            services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Optimal);
            services.AddResponseCompression(options =>
            {
                options.Providers.Add<GzipCompressionProvider>();
                //options.EnableForHttps = true;
                options.MimeTypes = new[]
                {
                    // Default
                    "text/plain",
                    "text/css",
                    "application/javascript",
                    "text/html",
                    "application/xml",
                    "text/xml",
                    "application/json",
                    "text/json",
 
                    // Custom
                    "image/svg+xml",
                    "application/font-woff2"
                };
            });

            services.Configure<HstsOptions>(options =>
            {
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });

            // Swagger API Documentation generator
            if (Environment.IsDevelopment()) services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v2", new Info { Title = "CarWash API", Version = "v2" });

                c.AddSecurityDefinition("Bearer", new ApiKeyScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = "header",
                    Type = "apiKey"
                });
                c.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>> {
                    { "Bearer", Enumerable.Empty<string>() },
                });

                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IServiceProvider serviceProvider)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();

            serviceProvider.ConfigureEmailProvider(Configuration);

            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Frame-Options", new[] { "SAMEORIGIN" });
                //context.Response.Headers.Add("Strict-Transport-Security", new[] { "max-age=31536000; includeSubDomains" });
                context.Response.Headers.Add("Expect-CT", new[] { "expect-ct: max-age=604800, report-uri=https://markszabo.report-uri.com/r/d/ct/enforce" });
                context.Response.Headers.Add("X-XSS-Protection", new[] { "1; mode=block; report=https://markszabo.report-uri.com/r/d/xss/enforce" });
                context.Response.Headers.Add("X-Content-Type-Options", new[] { "nosniff" });
                context.Response.Headers.Add("Referrer-Policy", new[] { "strict-origin-when-cross-origin" });
                context.Response.Headers.Add("Feature-Policy", new[] { "accelerometer 'none'; camera 'none'; geolocation 'self'; gyroscope 'none'; magnetometer 'none'; microphone 'none'; payment 'none'; usb 'none'" });
                context.Response.Headers.Add("Content-Security-Policy", new[] { "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' *.msecnd.net storage.googleapis.com; style-src 'self' 'unsafe-inline' fonts.googleapis.com fonts.gstatic.com; img-src 'self' data:; connect-src https: wss: 'self' fonts.googleapis.com fonts.gstatic.com; font-src 'self' fonts.googleapis.com fonts.gstatic.com; frame-src 'self' login.microsoftonline.com; form-action 'self'; upgrade-insecure-requests; report-uri https://markszabo.report-uri.com/r/d/csp/enforce" });
                context.Response.Headers.Remove(HeaderNames.Server);
                context.Response.Headers.Remove("X-Powered-By");
                await next();
            });

            app.UseResponseCompression();

            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    const int cacheExpirationInSeconds = 60 * 60 * 24 * 30; //one month
                    ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                        "public,max-age=" + cacheExpirationInSeconds;
                }
            });
            app.UseSpaStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller}/{action=Index}/{id?}");
            });

            if (env.IsDevelopment())
            {
                // Enable middleware to serve generated Swagger as a JSON endpoint.
                app.UseSwagger();

                // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), 
                // specifying the Swagger JSON endpoint.
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v2/swagger.json", "CarWash API");
                    c.EnableDeepLinking();
                    c.DocumentTitle = "CarWash API";
                });
            }

            app.UseSpa(spa =>
            {
                spa.Options.SourcePath = "ClientApp";

                if (env.IsDevelopment())
                {
                    spa.UseReactDevelopmentServer(npmScript: "start");
                }
            });
        }
    }
}

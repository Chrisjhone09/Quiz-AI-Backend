
using System.Threading.RateLimiting;
using back.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace back
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("allowAny", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            builder.Services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? httpContext.TraceIdentifier,
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 4,
                            Window = TimeSpan.FromSeconds(60),
                            QueueLimit = 2
                        }));
            });

            builder.Services.AddControllers();
            builder.Services.AddSignalR();
            builder.Services.AddOpenApi();

            var app = builder.Build();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }


            app.UseStaticFiles();

            app.UseRouting();

            app.UseRateLimiter();

            app.UseCors("allowAny");

            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<Hubs.QuizHub>("/quizhub");
            app.MapGet("/api/upload-file", () => "This endpoint is rate limited")
                .RequireRateLimiting("fixed");

            app.Run();
        }
    }
}

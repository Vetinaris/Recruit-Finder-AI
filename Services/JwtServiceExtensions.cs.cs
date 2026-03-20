using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Recruit_Finder_AI.Extensions
{
    public static class IdentityServiceExtensions
    {
        public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration config)
        {
            var jwtKey = config["Jwt:Key"];

            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("Missing JWT Key in configuration (check secrets.json)");
            }

            services.AddAuthentication()
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                        ValidateIssuer = true,
                        ValidIssuer = config["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = config["Jwt:Audience"],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });

            return services;
        }
    }
}
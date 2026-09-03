using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using CmdNext.Repository.Contracts;
using CmdNext.Repository.Implementation;
using CmdNext.Models.Domain.Model.App.Ai;
using CmdNext.Models.Domain.Model.App.Admin;

namespace CmdNext.Repository
{
    public static class ConfigureServices
    {
        public static IServiceCollection ConfigureRepositoryServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure DbContext
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Host=localhost;Port=5432;Database=cmdnext;Username=postgres;Password=;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=100;Connection Lifetime=0;";

            services.AddDbContext<CmdNextDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly("cmdnext.Migration");
                });
            });

            // Register UnitOfWork and Repository
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }
    }
}

using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using CmdNext.Repository.Implementation;

namespace CmdNext.EF.Migration
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CmdNextDbContext>
    {
        public CmdNextDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var builder = new DbContextOptionsBuilder<CmdNextDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=cmdnext;Username=postgres;Password=;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=100;Connection Lifetime=0;";

            builder.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly("cmdnext.Migration");
                // History lives in the "ai" schema (where InitialCreate recorded it).
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "ai");
            });

            return new CmdNextDbContext(builder.Options);
        }
    }
}

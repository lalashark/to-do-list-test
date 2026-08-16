using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TodoList.Api.Data;

namespace TodoList.Tests
{
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        private SqliteConnection? _connection;

        static CustomWebApplicationFactory()
        {
            // Inject JWT_SECRET into the test process environment variables dynamically before host startup
            System.Environment.SetEnvironmentVariable("JWT_SECRET", "this_is_a_very_long_mock_secret_key_used_only_for_unit_testing_32_bytes");
            // Inject a dummy database connection string to satisfy Program.cs startup checks before SQLite override
            System.Environment.SetEnvironmentVariable("DATABASE_CONNECTION_STRING", "Host=localhost;Database=dummy;Username=dummy;Password=dummy");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing PostgreSQL DbContext options configuration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<TodoDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Create and open a persistent SQLite in-memory connection
                _connection = new SqliteConnection("DataSource=:memory:");
                _connection.Open();

                // Add the DbContext using the SQLite connection
                services.AddDbContext<TodoDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

                // Ensure the SQLite database schema is created and initialized
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
                db.Database.EnsureCreated();
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _connection?.Dispose();
            }
        }
    }
}

using System;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TodoList.Api.Data;
using TodoList.Api.Middleware;
using TodoList.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as camelCase strings matching openapi spec (pending, inProgress, completed, etc.)
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Configure Database Connection
var connectionString = builder.Configuration["DATABASE_CONNECTION_STRING"] 
                       ?? builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database connection string is not configured. Please set the DATABASE_CONNECTION_STRING environment variable.");
}

builder.Services.AddDbContext<TodoDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// Configure JWT Authentication
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret))
{
    throw new InvalidOperationException("JWT Secret is not configured. Please set the JWT_SECRET environment variable.");
}
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? builder.Configuration["Jwt:Issuer"] ?? "TodoListApi";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? builder.Configuration["Jwt:Audience"] ?? "TodoListApi";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero
    };

    // Format auth-challenge errors as application/problem+json
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Access token is missing, invalid, or expired.",
                Instance = context.Request.Path,
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
            };

            return context.Response.WriteAsJsonAsync(problem);
        }
    };
});

builder.Services.AddScoped<ITokenService, TokenService>();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Multi-user To-do List API", 
        Version = "1.0.0",
        Description = "ASP.NET Core Web API for the Multi-user To-do List"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Enable Swagger UI in development (and production for evaluation convenience)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Multi-user To-do List API v1");
    c.RoutePrefix = "swagger"; // Access Swagger UI at http://localhost:8080/swagger
});

// Exception handling middleware at the beginning of the pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Automate migration execution on application startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<TodoDbContext>();
        if (db.Database.IsRelational() && db.Database.GetPendingMigrations().Any())
        {
            await db.Database.MigrateAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database on startup.");
    }
}

app.Run();

// Required to make Program class visible to integration test projects
public partial class Program { }

using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SqlSugar; // Add SQLSugar namespace
using NewWords.Api.Entities;
using NewWords.Api.Enums;
using NewWords.Api.Models.DTOs.Auth;
using NewWords.Api.Models.DTOs.User;
using NewWords.Api.Models.DTOs.Vocabulary;
using NewWords.Api.Services; // Add Entities namespace

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// --- Configure SQLSugar ---
builder.Services.AddSingleton<ISqlSugarClient>(s =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    var config = new ConnectionConfig()
    {
        ConnectionString = connectionString,
        DbType = DbType.MySql,
        IsAutoCloseConnection = true, // Recommended setting
        // Configure Code First settings if needed later
        // InitKeyType = InitKeyType.Attribute // Use attributes for PK/Identity definition
    };

    var db = new SqlSugarClient(config);

    // Log SQL statements in Development environment
    if (builder.Environment.IsDevelopment())
    {
        db.Aop.OnLogExecuting = (sql, pars) =>
        {
            Console.WriteLine("---------- SQL Executing ----------");
            Console.WriteLine(UtilMethods.GetSqlString(config.DbType, sql, pars));
            Console.WriteLine("-----------------------------------");
        };
        db.Aop.OnError = (exp) => // Log SQL errors
        {
             Console.WriteLine($"---------- SQL Error ----------\n{exp.Message}\n{exp.Sql}\n-------------------------------");
        };
    }

    // Optional: Code First - Create tables if they don't exist (use with caution in production)
    // Consider using explicit migrations for production environments
    // db.DbMaintenance.CreateDatabase(); // Ensure database exists
    // db.CodeFirst.SetStringDefaultLength(255).InitTables(typeof(User), typeof(Word), typeof(UserWord), typeof(LlmConfiguration));

    return db;
});
// --- End SQLSugar Configuration ---


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
// --- Configure Swagger for JWT ---
builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "NewWords API", Version = "v1" });
    option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter a valid token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            new string[]{}
        }
    });
});
// --- End Swagger JWT Configuration ---

// --- Configure JWT Authentication ---
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
{
    throw new InvalidOperationException("JWT Key, Issuer, or Audience not configured in appsettings.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true, // Validate token expiration
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero // Remove default clock skew
    };
});

builder.Services.AddAuthorization();
// --- End JWT Authentication Configuration ---

// --- Register Application Services ---
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVocabularyService, VocabularyService>();
// Register Repositories
builder.Services.AddScoped<NewWords.Api.Repositories.IUserRepository, NewWords.Api.Repositories.UserRepository>();
builder.Services.AddScoped<NewWords.Api.Repositories.IWordRepository, NewWords.Api.Repositories.WordRepository>();
builder.Services.AddScoped<NewWords.Api.Repositories.IUserWordRepository, NewWords.Api.Repositories.UserWordRepository>();
builder.Services.AddScoped<NewWords.Api.Repositories.ILlmConfigurationRepository, NewWords.Api.Repositories.LlmConfigurationRepository>();
// Add other services here as needed
// --- End Application Services ---


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// --- Add Authentication and Authorization Middleware ---
// IMPORTANT: Must be called after UseRouting (implicit in minimal APIs)
// and before endpoint mapping.
app.UseAuthentication();
app.UseAuthorization();
// --- End Auth Middleware ---

// --- Configure Controller Routing ---
app.MapControllers();
// --- End Controller Routing ---

app.Run();


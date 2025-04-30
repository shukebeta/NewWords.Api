using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Framework;
using Api.Framework.Database;
using Api.Framework.Extensions;
using Api.Framework.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NewWords.Api;
using NewWords.Api.Services;
using SqlSugar;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);
var logger = LoggerFactory.Create(config =>
{
    config.AddConsole();
    config.AddConfiguration(builder.Configuration.GetSection("Logging"));
}).CreateLogger("Program");

var envName = builder.Environment.EnvironmentName;

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(SetupSwaggerGen());
builder.Services.AddScoped(typeof(IRepositoryBase<>), typeof(RepositoryBase<>));
builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

// Configure SQLSugar
builder.Services.AddSqlSugarSetup(builder.Configuration.GetSection("DatabaseConnectionOptions").Get<DatabaseConnectionOptions>()!, logger);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddCors(SetupCors(builder));
ConfigAuthentication(builder);
builder.Services.AddHttpContextAccessor();

// Register Application Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVocabularyService, VocabularyService>();
builder.Services.AddScoped<NewWords.Api.Repositories.IUserRepository, NewWords.Api.Repositories.UserRepository>();
builder.Services.AddScoped<NewWords.Api.Repositories.IWordRepository, NewWords.Api.Repositories.WordRepository>();
builder.Services.AddScoped<NewWords.Api.Repositories.IUserWordRepository, NewWords.Api.Repositories.UserWordRepository>();
builder.Services.AddScoped<NewWords.Api.Repositories.ILlmConfigurationRepository, NewWords.Api.Repositories.LlmConfigurationRepository>();

var app = builder.Build();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowOrigins");
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
logger.LogInformation(envName);
app.Run();
return;

// Main program ends here, following are local methods

void ConfigAuthentication(WebApplicationBuilder b)
{
    var services = b.Services;
    var configuration = b.Configuration;
    services.Configure<JwtConfig>(configuration.GetSection("Jwt"));
    var jwtConfig = configuration.GetSection("Jwt").Get<JwtConfig>();
    services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtConfig!.Issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig.SymmetricSecurityKey)),
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

    JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
}

Action<SwaggerGenOptions> SetupSwaggerGen()
{
    return c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "NewWords API",
            Version = "v1"
        });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJodHRwO\"",
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
                new string[] { }
            }
        });
    };
}

Action<CorsOptions> SetupCors(WebApplicationBuilder webApplicationBuilder)
{
    return opts =>
    {
        string[] originList = webApplicationBuilder.Configuration.GetSection("AllowedCorsOrigins").Get<List<string>>()?.ToArray() ?? [];
        opts.AddPolicy("AllowOrigins", policy => policy.WithOrigins(originList)
            .AllowCredentials()
            .AllowAnyMethod()
            .AllowAnyHeader()
        );
    };
}

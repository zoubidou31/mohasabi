using System.Text.Json.Serialization;
using Factur.Api.Middleware;
using Factur.Application;
using Factur.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- Logging
var logPath = builder.Configuration["Serilog:File:Path"] ?? "logs/mohasabi-.log";
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("Démarrage de Mohasabi API");

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

    builder.Services.AddEndpointsApiExplorer();

    // Swagger
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Mohasabi API — Facturation algérienne",
            Version = "v1",
            Description = "API REST de l'application de facturation conforme à la fiscalité algérienne (TVA 19%, 9%, Exonéré).",
        });
    });

    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Frontend", policy =>
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
    });

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    // ---------------------------------------------------------------- Pipeline
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mohasabi API v1"));
    }

    app.UseCors("Frontend");

    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.MapControllers();
    app.MapFallbackToFile("index.html");

    // Migration + données de démarrage
    await app.Services.InitializeDatabaseAsync();

    Log.Information("Mohasabi API prête");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Échec du démarrage de Mohasabi API");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }

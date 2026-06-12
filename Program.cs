using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using RetellIntegrationApi.Configuration;
using RetellIntegrationApi.Middleware;
using RetellIntegrationApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add standard Web API controllers
builder.Services.AddControllers();

// Bind and register configurations using the IOptions pattern
builder.Services.Configure<RetellOptions>(builder.Configuration.GetSection(RetellOptions.Retell));
builder.Services.Configure<GoogleSheetsOptions>(builder.Configuration.GetSection(GoogleSheetsOptions.GoogleSheets));
builder.Services.Configure<ComplaintSheetsOptions>(builder.Configuration.GetSection(ComplaintSheetsOptions.ComplaintSheets));

// Register custom Google Sheets service for DI
builder.Services.AddScoped<IGoogleSheetsService, GoogleSheetsService>();
builder.Services.AddScoped<IComplaintGoogleSheetsService, ComplaintGoogleSheetsService>();
builder.Services.AddScoped<IEventSheetsService, EventSheetsService>();
builder.Services.AddScoped<IQuoteEstimatorService, QuoteEstimatorService>();
builder.Services.AddScoped<IOrangeEventsSheetSetupService, OrangeEventsSheetSetupService>();
builder.Services.AddScoped<IQuotationService, QuotationService>();
builder.Services.AddScoped<ILeadService, LeadService>();

// Configure Swashbuckle/Swagger generator
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Retell AI Web API Integration Test Server",
        Version = "v1",
        Description = "A complete, production-quality .NET 8 Web API designed to receive, validate, and log Retell AI voice call webhook events directly into Google Sheets."
    });

    // Define "x-api-key" header authorization inside Swagger UI
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Retell webhook authorization using the 'x-api-key' header. Example: 'x-api-key: your_secret_key'",
        Type = SecuritySchemeType.ApiKey,
        Name = "x-api-key",
        In = ParameterLocation.Header,
        Scheme = "ApiKeyScheme"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                },
                In = ParameterLocation.Header
            },
            Array.Empty<string>()
        }
    });

    // Enforce loading XML comments for detailed properties and manual testing example objects
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Configure the HTTP pipeline
// Swagger is enabled in both Development and Production for convenience in test evaluations
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Retell Webhook API v1");
    c.RoutePrefix = "swagger"; // Host Swagger UI at the /swagger route
});

// 1. Exception handling middleware at the top of the chain to catch all downstream faults
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 2. Request logger to capture endpoint execution metrics (Method, Path, Status, Duration)
app.UseMiddleware<RequestLoggingMiddleware>();

// 3. Webhook security validation middleware to secure incoming webhook calls
app.UseMiddleware<RetellApiKeyMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

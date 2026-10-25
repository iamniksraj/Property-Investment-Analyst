using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PropertyInvestmentAnalyst.Api.Data;
using PropertyInvestmentAnalyst.Api.Services;
using PropertyInvestmentAnalyst.Api.Services.Analyst;
using PropertyInvestmentAnalyst.Api.Services.MarketData;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.Configure<MarketDataOptions>(
    builder.Configuration.GetSection(MarketDataOptions.SectionName));
builder.Services.PostConfigure<MarketDataOptions>(options =>
    MarketDataEnvironmentGuard.Apply(builder.Environment, options));
builder.Services.AddScoped<ComparisonApplicationService>();
builder.Services.AddScoped<MarketIngestionService>();
builder.Services.AddScoped<MarketObservationApplicationService>();
builder.Services.AddHttpClient<IMarketDataProvider, LicensedFeedMarketDataProvider>();
builder.Services.AddScoped<MarketProviderCatalogService>();
builder.Services.Configure<InvestmentAnalystOptions>(
    builder.Configuration.GetSection(InvestmentAnalystOptions.SectionName));
builder.Services.AddHttpClient<IInvestmentAnalystProvider, OpenAiInvestmentAnalystProvider>();
builder.Services.AddScoped<InvestmentAnalystApplicationService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        context.Response.ContentType = "application/problem+json";

        if (exception is ComparisonValidationException validation)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Results.ValidationProblem(validation.Errors, title: "Validation failed").ExecuteAsync(context);
            return;
        }

        if (exception is AnalystValidationException analystValidation)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Results.ValidationProblem(analystValidation.Errors, title: "Validation failed").ExecuteAsync(context);
            return;
        }

        if (exception is AnalystProviderException provider)
        {
            context.Response.StatusCode = provider.StatusCode;
            await Results.Problem(title: provider.Title, detail: provider.Message, statusCode: provider.StatusCode)
                .ExecuteAsync(context);
            return;
        }

        if (exception is MarketValidationException marketValidation)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Results.ValidationProblem(marketValidation.Errors, title: "Validation failed").ExecuteAsync(context);
            return;
        }

        if (exception is ComparisonNotFoundException notFound)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await Results.Problem(title: "Comparison not found", detail: notFound.Message, statusCode: 404)
                .ExecuteAsync(context);
            return;
        }

        if (exception is MarketNotFoundException marketNotFound)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await Results.Problem(title: "Observation not found", detail: marketNotFound.Message, statusCode: 404)
                .ExecuteAsync(context);
            return;
        }

        if (exception is ComparisonConflictException conflict)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await Results.Problem(title: "Update conflict", detail: conflict.Message, statusCode: 409)
                .ExecuteAsync(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var detail = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")
            ? exception?.Message
            : "An unexpected error occurred.";
        await Results.Problem(title: "Server error", detail: detail, statusCode: 500).ExecuteAsync(context);
    });
});

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.MapControllers();
app.Run();

public partial class Program;

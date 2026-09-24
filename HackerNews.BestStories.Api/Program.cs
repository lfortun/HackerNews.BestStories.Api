using HackerNews.BestStories.Api.Middlewares;
using HackerNews.BestStories.Application;
using HackerNews.BestStories.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Register controller support required for API endpoints mapping
builder.Services.AddControllers();

builder.Services.AddOpenApi();

// Register Clean Architecture layers dependency injection extensions
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

var app = builder.Build();

// Global Exception Middleware positioned at the start of the pipeline to catch all downstream errors
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Enable Swagger UI and OpenAPI documentation endpoints in development mode
if (app.Environment.IsDevelopment())
{
    // Generates the openapi.json metadata file
    app.MapOpenApi();

    // Enables the interactive web UI accessible via browser at: http://localhost:YOUR_PORT/swagger
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Hacker News Best Stories API v1");
        options.RoutePrefix = "swagger"; // Standard professional route endpoint
    });
}

app.UseHttpsRedirection();

// Map and route API Controller endpoints dynamically
app.MapControllers();

app.Run();

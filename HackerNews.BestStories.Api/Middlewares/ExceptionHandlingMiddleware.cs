using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace HackerNews.BestStories.Api.Middlewares
{
    /// <summary>
    /// Global middleware to intercept, log, and handle unhandled exceptions across the application.
    /// Responds with a standardized ProblemDetails (RFC 7807) payload; exception details are only
    /// exposed in Development to avoid leaking internals.
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IWebHostEnvironment environment)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred during the request processing loop.");
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var problemDetails = new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = ReasonPhrases.GetReasonPhrase(context.Response.StatusCode),
                Detail = _environment.IsDevelopment() ? exception.Message : null
            };
            problemDetails.Extensions["traceId"] = context.TraceIdentifier;

            return context.Response.WriteAsJsonAsync(problemDetails, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
    }
}

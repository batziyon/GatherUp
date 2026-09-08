using System.Net;
using System.Text.Json;
using GatherUp.Core.Exceptions;

namespace GatherUp.API.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next   = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
                await WriteErrorResponse(context, ex);
            }
        }

        private static async Task WriteErrorResponse(HttpContext context, Exception ex)
        {
            if (context.Response.HasStarted) return;

            context.Response.ContentType = "application/json; charset=utf-8";

            (int statusCode, string errorCode) = ex switch
            {
                EntityNotFoundException        => (404, "ENTITY_NOT_FOUND"),
                InvalidInputException          => (400, "INVALID_INPUT"),
                ReceiptLockedException         => (409, "RECEIPT_LOCKED"),
                UnauthorizedOperationException => (403, "UNAUTHORIZED_OPERATION"),
                KeyNotFoundException           => (404, "ENTITY_NOT_FOUND"),
                InvalidOperationException      => (400, "INVALID_OPERATION"),
                ArgumentException              => (400, "INVALID_INPUT"),
                _                              => (500, "INTERNAL_ERROR")
            };

            context.Response.StatusCode = statusCode;

            var body = JsonSerializer.Serialize(new
            {
                statusCode,
                errorCode,
                message = ex.Message
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            await context.Response.WriteAsync(body);
        }
    }
}

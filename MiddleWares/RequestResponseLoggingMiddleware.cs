namespace WebApi
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Features;
    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using Serilog;
    using Serilog.Context;

    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public RequestResponseLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                var stopWatch = new Stopwatch();
                stopWatch.Start();
                var request = await FormatRequest(context.Request);

                var originalBodyStream = context.Response.Body;

                using (var responseBody = new MemoryStream())
                {
                    context.Response.Body = responseBody;

                    Logger.Write(LogLevel.Information, request);

                    await _next(context);

                    var response = await FormatResponse(context.Response, stopWatch);
                    Logger.Write(LogLevel.Information, response);

                    await responseBody.CopyToAsync(originalBodyStream);
                }
            }
            catch (Exception ex)
            {
                Logger.Write(LogLevel.Critical, new ExceptionData { Exception = ex, Mesage = ex.Message });
            }
        }

        private async Task<QueryRequestData> FormatRequest(HttpRequest request)
        {

            var remoteIpAddress = request.HttpContext.Connection.RemoteIpAddress;

            request.EnableBuffering();
            string bodyAsText;
            using (var reader = new StreamReader(request.Body, encoding: Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
            {
                bodyAsText = await reader.ReadToEndAsync();
                request.Body.Position = 0;
            }

            var l = new QueryRequestData();
            l.RemoteIpAddress = remoteIpAddress?.ToString();
            l.Scheme = request.Scheme;
            l.Host = request.Host.ToString();
            l.Path = request.Path;
            l.QueryString = request.QueryString.ToString();
            l.BodyAsJson = bodyAsText;

            return l;
        }

        private async Task<QueryResponseData> FormatResponse(HttpResponse response, Stopwatch stopwatch)
        {
            response.Body.Seek(0, SeekOrigin.Begin);

            string bodyAsText = await new StreamReader(response.Body).ReadToEndAsync();

            response.Body.Seek(0, SeekOrigin.Begin);

            stopwatch.Stop();

            var l = new QueryResponseData();
            l.StatusCode = response.StatusCode.ToString();
            l.ExecutedTime = stopwatch.ElapsedMilliseconds;
            l.BodyAsText = bodyAsText;
            return l;
        }
    }

}

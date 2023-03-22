using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using ExecutionContext = Microsoft.Azure.WebJobs.ExecutionContext;

namespace GraphNotifications.Functions
{
    public static class Client
    {
        [FunctionName("TestClient")]
        public static HttpResponseMessage Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "/")] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string fullFilePath = context.FunctionAppDirectory + "/TestClient/index.html";

            log.LogInformation(fullFilePath);
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var stream = new FileStream(fullFilePath, FileMode.Open);
            response.Content = new StreamContent(stream);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return response;
        }
    }
}

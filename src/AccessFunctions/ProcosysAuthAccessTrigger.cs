using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccessFunctions
{
    public static class ProcosysAuthAccessTrigger
    {
        private static IQueueClient _queueClient;
        private static ILogger _logger;

        #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        [FunctionName("ProcosysAuthAccessTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest request,
            ILogger logger)
        {
            _logger = logger;
            _logger.LogInformation($"Incomming request {request.Query}");

            /**
             * Microsoft graph sends a probing request with a token to test the endpoint.
             * If the json request body contains a property "valueToken",
             * graph expects a 202 response, with the token as the response body. 
             **/
            if (AccessTriggerHelper.GetToken(request) is string token)
            {
                return new OkObjectResult(token);
            }

            AddMessagesToQueue(request);

            //Allways return accepted, or notifications gets turned off
            return new AcceptedResult();
        }

        private static void AddMessagesToQueue(HttpRequest request)
        {
            InitializeQueueClient();

            var notifications = AccessTriggerHelper.ExtractNotifications(request, _logger);
            if (notifications.Count > 0)
            {
                notifications.ForEach(async notification => await SendMessageAsync(notification));
            }
            else
            {
                _logger.LogInformation($"The request{request.Query} didn't contain any relevant information");
            }
        }

        private static void InitializeQueueClient()
        {
            try
            {
                var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
                var queueName = Environment.GetEnvironmentVariable("ServiceBusQueueName");
                _queueClient = new QueueClient(serviceBusConnectionString, queueName);
            }catch(Exception e)
            {
                _logger.LogError($"InitializeQueueClient Failed with exception {e.Message}");
            }
        }

        private static async Task SendMessageAsync(string messageBody)
        {
            try
            {
                var message = new Message(Encoding.UTF8.GetBytes(messageBody));
                _logger.LogInformation($"Sending message: {messageBody}");
                await _queueClient.SendAsync(message);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"{DateTime.Now} :: Exception: {exception.Message}");
                _logger.LogError($"{DateTime.Now} :: Exception: {exception.Message}");
            }
        }
    }
}


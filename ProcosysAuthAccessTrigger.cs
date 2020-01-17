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

        [FunctionName("ProcosysAuthAccessTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest request,
            ILogger log)
        {
            _logger = log;
            _logger.LogInformation($"Incomming request {request.Query}");

            string token = AccessTriggerHelper.GetToken(request);
            if (!string.IsNullOrWhiteSpace(token))
            {
                return new OkObjectResult(token);
            }

            InitializeQueueClient();

            var notifications = AccessTriggerHelper.ExtractJsonNotifications(request, _logger);

            if (notifications.Count == 0)
            {
                _logger.LogInformation($"the request{request.Query} didn't contain any relevant information");
            }

            notifications.ForEach(async n => await SendMessagesAsync(n));

            //Allways return accepted, or notifications gets turned off
            return new AcceptedResult();
        }

        private static void InitializeQueueClient()
        {
            var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
            var queueName = Environment.GetEnvironmentVariable("ServiceBusQueueName");
            _queueClient = new QueueClient(serviceBusConnectionString, queueName);
        }

        private static async Task SendMessagesAsync(string messageBody)
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


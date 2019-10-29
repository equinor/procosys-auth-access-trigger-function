using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.ServiceBus;
using System.Text;

namespace AccessTriggerFunction
{
    public static class ProcosysAuthAccessTrigger
    {
        static IQueueClient queueClient;
        private static ILogger _logger;

        [FunctionName("ProcosysAuthAccessTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
           var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
           var queueName = Environment.GetEnvironmentVariable("ServiceBusQueueName");
           _logger = log;

           queueClient = new QueueClient(serviceBusConnectionString, queueName);
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            _logger.LogInformation("C# HTTP trigger function processed a request.");
            dynamic data = JsonConvert.DeserializeObject(requestBody);


            var validation = ValidateInput(data);
            if(!validation.Item1)
            {
                _logger.LogInformation($"Returning Bad request with message: {validation.Item2}");
                return new BadRequestObjectResult(validation.Item2);
            }

             var returnMessage =  data.hasAccess == true ? $"user with oid:  {data.userOid}, is added to {data.plantId}" 
                : $"user with oid: {data.userOid}, is removed from {data.plantId}";
          
            _logger.LogInformation($"Sending request to queue: {requestBody}");
            await SendMessagesAsync(requestBody);
        
            return new OkObjectResult(returnMessage);
        }

        static async Task SendMessagesAsync(string messageBody)
        {
            try
            {
                var message = new Message(Encoding.UTF8.GetBytes(messageBody));
                    _logger.LogInformation($"Sending message: {messageBody}");
                    await queueClient.SendAsync(message);
                
            }
            catch (Exception exception)
            {
                Console.WriteLine($"{DateTime.Now} :: Exception: {exception.Message}");
                _logger.LogError($"{DateTime.Now} :: Exception: {exception.Message}");
            }
        }

        private static (bool isValid, string message) ValidateInput(dynamic data)
        {
            string oid = data.userOid;
            string plantId = data.plantId;
            bool? plantAccess = data.hasAccess;
            
            if(string.IsNullOrWhiteSpace(oid))
            {
                return(false,"User identidier, userOid is missing from requestbody");
            }

            if(string.IsNullOrWhiteSpace(plantId))
            {
                return (false,"Plant identifier, plantId is missing from requestbody");
            }

            if(plantAccess == null)
            {
                return (false,"Plant access modifer, hasAccess is missing from requestbody");
            }
            return (true, null);
        }
    }
}


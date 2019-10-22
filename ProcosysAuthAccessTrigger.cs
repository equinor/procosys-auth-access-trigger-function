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

        [FunctionName("ProcosysAuthAccessTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
           var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
           var queueName = Environment.GetEnvironmentVariable("ServiceBusQueueName");

           queueClient = new QueueClient(serviceBusConnectionString, queueName);
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();


            log.LogInformation("C# HTTP trigger function processed a request.");
            dynamic data = JsonConvert.DeserializeObject(requestBody);
         
            var oid = data?.access?.UserOid;
            var plantId = data?.access?.plant?.plantId;

            bool? plantAccess = data?.access?.plant?.hasAccess;
            
            if(oid == null)
            {
                return new BadRequestObjectResult("Please pass a oid in the request body");
            }

            if(plantAccess == null || plantId == null)
            {
                return new BadRequestObjectResult("Please pass a plant information in the request body");
            }
             var returnMessage = plantAccess == true ? $"user with oid:  {oid}, is added to {plantId}" 
                : $"user with oid:  {oid}, is removed from {plantId}";
          
            await SendMessagesAsync(requestBody);
        
            return  (ActionResult)new OkObjectResult(returnMessage);
                
        }

        static async Task SendMessagesAsync(string messageBody)
        {
            try
            {
                var message = new Message(Encoding.UTF8.GetBytes(messageBody));
                    // Send the message to the queue
                    Console.WriteLine($"Sending message: {messageBody}");
                    await queueClient.SendAsync(message);
                
            }
            catch (Exception exception)
            {
                Console.WriteLine($"{DateTime.Now} :: Exception: {exception.Message}");
            }
        }
    }
}


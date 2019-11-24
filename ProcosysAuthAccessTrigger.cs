using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
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
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            _logger = log;
            
            _logger.LogInformation($"Incomming request {req.Query.ToString()}");
            string token = req.GetQueryParameterDictionary().FirstOrDefault(
                q => string.Compare(q.Key, "validationToken", true) == 0).Value;
            if (!string.IsNullOrWhiteSpace(token))
            {
                return new OkObjectResult(token);
            }

            var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
            var queueName = Environment.GetEnvironmentVariable("ServiceBusQueueName");
            _queueClient = new QueueClient(serviceBusConnectionString, queueName);
       
                var notifications = new List<string>();
                using (var inputStream = new StreamReader(req.Body))
                {
                    JObject jsonObject = JObject.Parse(inputStream.ReadToEnd());
                    if (jsonObject != null)
                    {
                        // Notifications are sent in a 'value' array. The array might contain multiple notifications for events that are
                        // registered for the same notification endpoint, and that occur within a short timespan.
                        JArray value = JArray.Parse(jsonObject["value"].ToString());
                        foreach (var notification in value)
                        {
                            Notification current = JsonConvert.DeserializeObject<Notification>(notification.ToString());
                        
                        // Check client state to verify the message is from Microsoft Graph.
                        var hasValidClientState = current.ClientState.Equals(Environment.GetEnvironmentVariable("SubscriptionClientState"));
                        if (!hasValidClientState)
                        {
                            _logger.LogInformation($"ClientState wrong, the clientstate used was: {current.ClientState}");
                        }


                            if (hasValidClientState && current.ResourceData.Members != null)
                            {
                                var json = JObject.FromObject(new
                                {
                                    groupId = current.ResourceData.Id,
                                    members = current.ResourceData.Members
                                      .Select(m => new { id = m.Id, remove = "deleted".Equals(m.Removed) })
                                }).ToString();
                            notifications.Add(json);
                            }
                        }
                    }
                    if (notifications.Count > 0)
                    {
                        foreach(string notification in notifications)
                        {
                            await SendMessagesAsync(notification);
                        }
                    }
                    //always return accepted
                    return new AcceptedResult();
                }
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


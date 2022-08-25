using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace AccessFunctions;

public static class ProCoSysAuthAccessTrigger
{
    private static ServiceBusSender _serviceBusSender;
    private static ILogger _logger;

    [FunctionName("ProCoSysAuthAccessTrigger")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest request,
        ILogger logger)
    {
        _logger = logger;
        _logger.LogInformation($"Incoming request {request.Query}");

        /***
         * Microsoft graph sends a probing request with a token to test the endpoint.
         * If the json request body contains a property "valueToken",
         * graph expects a 202 response, with the token as the response body. 
         **/
        if (AccessTriggerHelper.GetValueToken(request) is { } valueToken)
        {
            return new OkObjectResult(valueToken);
        }

        await Task.Run(()=> AddMessagesToQueue(request));

        //Always return accepted, or notifications gets turned off
        return new AcceptedResult();
    }

    private static async void AddMessagesToQueue(HttpRequest request)
    {
        await InitializeQueueClient();

        var notifications = AccessTriggerHelper.ExtractNotifications(request, _logger);
        if (notifications.Count > 0)
        {
            try
            {
                await BatchAndSendMessages(notifications);
            }
            catch (Exception e)
            {
                _logger.LogError($"InitializeQueueClient Failed with exception {e.Message}");
            }

        }
        else
        {
            _logger.LogInformation($"The request{request.Query} didn't contain any relevant information");
        }
    }

    private static async Task BatchAndSendMessages(List<string> notifications)
    {
        Queue<ServiceBusMessage> messages = new();
        notifications.ForEach(n => messages.Enqueue(new ServiceBusMessage(n)));
        var messageCount = messages.Count;
        while (messages.Count > 0)
        {
            using var messageBatch = await _serviceBusSender.CreateMessageBatchAsync();
            // add the first message to the batch
            if (messageBatch.TryAddMessage(messages.Peek()))
            {
                // dequeue the message from the .NET queue once the message is added to the batch
                messages.Dequeue();
            }
            else
            {
                // if the first message can't fit, then it is too large for the batch
                throw new Exception($"Message {messageCount - messages.Count} is too large and cannot be sent.");
            }

            // add as many messages as possible to the current batch
            while (messages.Count > 0 && messageBatch.TryAddMessage(messages.Peek()))
            {
                // dequeue the message from the .NET queue as it has been added to the batch
                messages.Dequeue();
            }

            // now, send the batch
            await _serviceBusSender.SendMessagesAsync(messageBatch);
        }
    }

    private static async Task InitializeQueueClient()
    {
        try
        {
            var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
            var queueName = Environment.GetEnvironmentVariable("ServiceBusQueueName");
            await using var client = new ServiceBusClient(serviceBusConnectionString);
            _serviceBusSender = client.CreateSender(queueName);
        }
        catch (Exception e)
        {
            _logger.LogError($"InitializeQueueClient Failed with exception {e.Message}");
        }
    }
}
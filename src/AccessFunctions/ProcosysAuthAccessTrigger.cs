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
    private static ServiceBusClient _serviceBusClient;
    private static ILogger _logger;

    [FunctionName("ProCoSysAuthAccessTrigger")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest request,
        ILogger logger)
    {
        AppDomain.CurrentDomain.ProcessExit += async (s, e) =>
        {
            await _serviceBusClient.DisposeAsync();
            await _serviceBusSender.DisposeAsync();
        };

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

        await Task.Run(()=> HandleRequest(request));

        //Always return accepted, or notifications gets turned off
        return new AcceptedResult();
    }

    private static async void HandleRequest(HttpRequest request)
    {
        var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
        var queueName = Environment.GetEnvironmentVariable("ServiceBusQueueName");
        var client = new ServiceBusClient(serviceBusConnectionString);
        _serviceBusSender = client.CreateSender(queueName);

        var notifications = AccessTriggerHelper.ExtractNotifications(request, _logger);
        if (notifications.Count > 0)
        {
            try
            {
                await SendMessagesToServiceBusQueue(notifications);
            }
            catch (Exception e)
            {
                _logger.LogError($"SendMessages Failed with exception {e.Message}");
            }

        }
        else
        {
            _logger.LogInformation($"The request{request.Query} didn't contain any relevant information");
        }
    }

    private static async Task SendMessagesToServiceBusQueue(List<string> notifications)
    {
        /***
         * group messages into batches, and fail before sending if message exceeds size limit
         *
         * Pattern taken from:
         * https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/MigrationGuide.md
         */

        Queue<ServiceBusMessage> messages = new();
        notifications.ForEach(n => messages.Enqueue(new ServiceBusMessage(n)));
        var messageCount = messages.Count;

        while (messages.Count > 0)
        {
            using var messageBatch = await _serviceBusSender.CreateMessageBatchAsync();
            // add first unsent message to batch
            if (messageBatch.TryAddMessage(messages.Peek()))
            {
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
                messages.Dequeue();
            }
            await _serviceBusSender.SendMessagesAsync(messageBatch);
        }
    }

    private static void InitializeServiceBusClient()
    {
        try
        {
            var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
            var queueName = Environment.GetEnvironmentVariable("ServiceBusQueueName");
            _serviceBusClient = new ServiceBusClient(serviceBusConnectionString);
            _serviceBusSender = _serviceBusClient.CreateSender(queueName);
        }
        catch (Exception e)
        {
            _logger.LogError($"InitializeServiceBusClient Failed with exception {e.Message}");
        }
    }
}
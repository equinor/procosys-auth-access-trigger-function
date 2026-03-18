using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccessFunctions;

public class ProCoSysAuthAccessTrigger(
    ILogger<ProCoSysAuthAccessTrigger> logger,
    ServiceBusClient serviceBusClient,
    IConfiguration configuration)
{
    [Function("ProCoSysAuthAccessTrigger")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData request)
    {
        logger.LogInformation("Incoming request to {Url}", request.Url);

        /***
         * Microsoft graph sends a probing request with a token to test the endpoint.
         * If the json request body contains a property "valueToken",
         * graph expects a 202 response, with the token as the response body. 
         **/
        if (AccessTriggerHelper.GetValueToken(request) is { } valueToken)
        {
            var okResponse = request.CreateResponse(HttpStatusCode.OK);
            await okResponse.WriteStringAsync(valueToken);
            return okResponse;
        }

        await HandleRequest(request);

        //Always return accepted, or notifications gets turned off
        return request.CreateResponse(HttpStatusCode.Accepted);
    }

    private async Task HandleRequest(HttpRequestData request)
    {
        var notifications = await AccessTriggerHelper.ExtractNotifications(request, logger);
        if (notifications.Count > 0)
        {
            try
            {
                await SendMessagesToServiceBusQueue(notifications);
            }
            catch (Exception e)
            {
                logger.LogError(e, "SendMessages Failed");
            }
        }
        else
        {
            logger.LogInformation("The request to {Url} didn't contain any relevant information", request.Url);
        }
    }

    private async Task SendMessagesToServiceBusQueue(List<string> notifications)
    {
        /***
         * group messages into batches, and fail before sending if message exceeds size limit
         *
         * Pattern taken from:
         * https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/servicebus/Azure.Messaging.ServiceBus/MigrationGuide.md
         */

        var queueName = configuration["ServiceBusQueueName"];
        var serviceBusSender = serviceBusClient.CreateSender(queueName);

        try
        {
            Queue<ServiceBusMessage> messages = new();
            notifications.ForEach(n => messages.Enqueue(new ServiceBusMessage(n)));
            var messageCount = messages.Count;

            while (messages.Count > 0)
            {
                using var messageBatch = await serviceBusSender.CreateMessageBatchAsync();
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
                await serviceBusSender.SendMessagesAsync(messageBatch);
            }

            logger.LogInformation("Successfully sent {MessageCount} messages to queue {QueueName}", messageCount, queueName);
        }
        finally
        {
            await serviceBusSender.DisposeAsync();
        }
    }
}

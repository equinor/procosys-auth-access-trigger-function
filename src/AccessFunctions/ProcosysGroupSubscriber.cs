using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace AccessFunctions;

public class ProCoSysGroupSubscriber(
    ILogger<ProCoSysGroupSubscriber> logger,
    GraphServiceClient graphClient,
    IConfiguration configuration)
{
    private const string Cron = "0 2 * * *"; //Every night at 2am
    private const string Resource = "groups";
    private const string ChangeType = "updated";

    //[Disable]
    [Function("ProCoSysGroupSubscriber")]
    public async Task Run([TimerTrigger(Cron)] TimerInfo _)
    {
        var subscriptionTime = double.Parse(configuration["SubscriptionTimeToLive"] ?? "4230"); //FallbackValue 
        var clientState = configuration["SubscriptionClientState"];
        var notificationUrl = configuration["NotificationUrl"];

        var currentSubscriptions = await graphClient.Subscriptions.Request().GetAsync();

        var subscriptionToUpdateId = GetSubscriptionToUpdate(notificationUrl, currentSubscriptions);

        if (subscriptionToUpdateId != null)
        {
            await UpdateSubscription(subscriptionTime, subscriptionToUpdateId);
        }
        else
        {
            await CreateSubscription(subscriptionTime, clientState, notificationUrl);
        }
    }

    private async Task CreateSubscription(double subscriptionTime, string clientState, string notificationUrl)
    {
        var expirationDateTime = DateTimeOffset.UtcNow.AddMinutes(subscriptionTime);

        var subscription = new Subscription
        {
            ChangeType = ChangeType,
            NotificationUrl = notificationUrl,
            Resource = Resource,
            ExpirationDateTime = expirationDateTime,
            ClientState = clientState
        };

        logger.LogInformation("Creating new Graph subscription with NotificationUrl: {NotificationUrl}. Expiration time: {ExpirationDateTime}", notificationUrl, expirationDateTime);

        var request = graphClient.Subscriptions.Request();
        await request.AddAsync(subscription);
    }

    private async Task UpdateSubscription(double subscriptionTime, string updateId)
    {
        var expirationDateTime = DateTimeOffset.UtcNow.AddMinutes(subscriptionTime);

        var subscription = new Subscription
        {
            ExpirationDateTime = expirationDateTime
        };

        logger.LogInformation("Updating Graph subscription with Id: {SubscriptionId}. New expiration time: {ExpirationDateTime}", updateId, expirationDateTime);

        await graphClient.Subscriptions[updateId].Request().UpdateAsync(subscription);
    }

    private static string GetSubscriptionToUpdate(string notificationUrl, IGraphServiceSubscriptionsCollectionPage subscriptions)
    {
        return subscriptions.Where(subscription => subscription.NotificationUrl == notificationUrl)
            .Select(subscription => subscription.Id)
            .FirstOrDefault();
    }
}

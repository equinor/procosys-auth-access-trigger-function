using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using static System.Environment;
using System.Linq;

namespace AccessFunctions;

public static class ProCoSysGroupSubscriber
{
    private const string Cron = "0 2 * * *"; //Every night at 2am
    private const string Resource = "groups";
    private const string ChangeType = "updated";

    private static ILogger _logger;

    //[Disable]
    [FunctionName("ProCoSysGroupSubscriber")]
    public static async Task Run([TimerTrigger(Cron)] TimerInfo myTimer,
        ILogger logger)
    {
        _logger = logger;

        var subscriptionTime = double.Parse(GetEnvironmentVariable("SubscriptionTimeToLive") ?? "4230"); //FallbackValue 
        var clientState = GetEnvironmentVariable("SubscriptionClientState");
        var notificationUrl = GetEnvironmentVariable("NotificationUrl");

        var graphClient = await SetUpGraphClient();

        var currentSubscriptions = await graphClient.Subscriptions.Request().GetAsync();

        var subscriptionToUpdateId = GetSubscriptionToUpdate(notificationUrl, currentSubscriptions);

        if (subscriptionToUpdateId != null)
        {
            await UpdateSubscription(subscriptionTime, graphClient, subscriptionToUpdateId);
        }
        else
        {
            await CreateSubscription(subscriptionTime, clientState, notificationUrl, graphClient);
        }
    }

    private static async Task CreateSubscription(double subscriptionTime, string clientState, string notificationUrl, GraphServiceClient graphClient)
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

        _logger.LogInformation($"Creating new Graph subscription with NotificationUrl: {notificationUrl}. Expiration time: {expirationDateTime}");

        var request = graphClient.Subscriptions.Request();
        await request.AddAsync(subscription);
    }

    private static async Task UpdateSubscription(double subscriptionTime, GraphServiceClient graphClient, string updateId)
    {
        var expirationDateTime = DateTimeOffset.UtcNow.AddMinutes(subscriptionTime);

        var subscription = new Subscription
        {
            ExpirationDateTime = expirationDateTime
        };

        _logger.LogInformation($"Updating Graph subscription with Id: {updateId}. New expiration time: {expirationDateTime}");

        await graphClient.Subscriptions[updateId].Request().UpdateAsync(subscription);
    }

    private static string GetSubscriptionToUpdate(string notificationUrl, IGraphServiceSubscriptionsCollectionPage subscriptions)
    {
        return subscriptions.Where(subscription => subscription.NotificationUrl == notificationUrl)
            .Select(subscription => subscription.Id)
            .FirstOrDefault();
    }

    private static async Task<GraphServiceClient> SetUpGraphClient()
    {
        var authority = GetEnvironmentVariable("AzureAuthority");
        var graphUrl = GetEnvironmentVariable("GraphUrl");
        var clientId = GetEnvironmentVariable("AzureClientId");
        var clientSecret = GetEnvironmentVariable("AzureClientSecret");
        var authContext = new AuthenticationContext(authority);

        var clientCred = new ClientCredential(clientId, clientSecret);
        var authenticationResult = await authContext.AcquireTokenAsync(graphUrl, clientCred);
        var accessToken = authenticationResult.AccessToken;

        var graphClient = new GraphServiceClient(
            new DelegateAuthenticationProvider(
                requestMessage =>
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                    return Task.FromResult(0);
                }));

        return graphClient;
    }
}
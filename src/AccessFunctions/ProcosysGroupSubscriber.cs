using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using static System.Environment;
using System.Diagnostics.CodeAnalysis;

namespace AccessFunctions
{
    public static class ProcosysGroupSubscriber
    {
        private const string Cron = "* * * * *"; //Every night at 2am
        private const string Resource = "groups";
        private const string ChangeType = "updated";

        private static ILogger _logger;

        //[Disable]
        [FunctionName("ProcosysGroupSubscriber")]
        [SuppressMessage("Redundancy", "RCS1163:Unused parameter.")]
        public static async Task Run([TimerTrigger(Cron)]TimerInfo myTimer,
           ILogger logger)
        {
            _logger = logger;

            double subScriptionTime = double.Parse(GetEnvironmentVariable("SubscriptionTimeToLive"));
            var clientState = GetEnvironmentVariable("SubscriptionClientState");
            var notificationUrl = GetEnvironmentVariable("NotificationUrl");

            var graphClient = await SetUpGraphClient();
            var currentSubscriptions = await graphClient.Subscriptions
                .Request()
                .GetAsync();

            if (GetSubscriptionToUpdate(notificationUrl, currentSubscriptions) is string updateId)
            {
                await UpdateSubscription(subScriptionTime, graphClient, updateId);
            }
            else
            {
                await CreateSubscription(subScriptionTime, clientState, notificationUrl, graphClient);
            }
        }

        private static async Task CreateSubscription(double subScriptionTime, string clientState, string notificationUrl, IGraphServiceClient graphClient)
        {
            var subscription = new Subscription
            {
                ChangeType = ChangeType,
                NotificationUrl = notificationUrl,
                Resource = Resource,
                ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(subScriptionTime),
                ClientState = clientState
            };

            var request = graphClient.Subscriptions.Request();

            _logger.LogInformation($"Subscribing to microsoft graph with with request: {request}");
            await request.AddAsync(subscription);
        }

        private static async Task UpdateSubscription(double subScriptionTime, IGraphServiceClient graphClient, string updateId)
        {
            var subscription = new Subscription
            {
                ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(subScriptionTime)
            };
            await graphClient.Subscriptions[updateId]
                .Request()
                .UpdateAsync(subscription);
        }

        private static string GetSubscriptionToUpdate(string notificationUrl, IGraphServiceSubscriptionsCollectionPage subscriptions)
            => subscriptions.Count > 0 && notificationUrl.Equals(subscriptions[0].NotificationUrl) ? subscriptions[0].Id : null;

        private static async Task<IGraphServiceClient> SetUpGraphClient()
        {
            var authority = GetEnvironmentVariable("AzureAuthority");
            var graphUrl = GetEnvironmentVariable("GraphUrl");
            var clientId = GetEnvironmentVariable("AzureClientId");
            var clientSecret = GetEnvironmentVariable("AzureClientSecret");
            var authContext = new AuthenticationContext(authority);

            ClientCredential clientCred = new ClientCredential(clientId, clientSecret);
            var authenticationResult = await authContext.AcquireTokenAsync(graphUrl, clientCred);
            var accessToken = authenticationResult.AccessToken;

            var graphClient = new GraphServiceClient(
                 new DelegateAuthenticationProvider(
                    (requestMessage) =>
                    {
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                        return Task.FromResult(0);
                    }));
            return graphClient;
        }
    }
}
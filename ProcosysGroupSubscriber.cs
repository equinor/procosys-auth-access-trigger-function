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
        private const string Cron = "0 2 * * *"; //Every night at 2am //TODO add to env variable if possible?
        private const string Resource = "groups";
        private const string ChangeType = "updated";

        [Disable("FUNCTION_DISABLED")]
        [FunctionName("ProcosysGroupSubscriber")]
        [SuppressMessage("Redundancy", "RCS1163:Unused parameter.")]
        public static async Task Run([TimerTrigger(Cron)]TimerInfo myTimer,
           ILogger log)
        {
            double subScriptionTime = double.Parse(GetEnvironmentVariable("SubscriptionTimeToLive"));
            var authority = GetEnvironmentVariable("AzureAuthority");
            var graphUrl = GetEnvironmentVariable("GraphUrl");
            var clientId = GetEnvironmentVariable("AzureClientId");
            var clientSecret = GetEnvironmentVariable("AzureClientSecret");
            var clientState = GetEnvironmentVariable("SubscriptionClientState");
            var notificationUrl = GetEnvironmentVariable("NotificationUrl");
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

            var subscriptions = await graphClient.Subscriptions
                .Request()
                .GetAsync();

            if (subscriptions.Count > 0 && notificationUrl.Equals(subscriptions[0].NotificationUrl))
            {
                var subscription = new Subscription
                {
                    ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(subScriptionTime)
                };
                var updateId = subscriptions[0].Id;
                await graphClient.Subscriptions[updateId]
                    .Request()
                    .UpdateAsync(subscription);
            }
            else
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

                log.LogInformation($"Subscribing to microsoft graph with with request: {request}");
                await request.AddAsync(subscription);
            }
        }
    }
}
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace AccessFunctions
{
    public static class ProcosysGroupSubscriber
    {
        private const string Cron = "0 2 * * *"; //Every night at 2am //TODO add to env variable
        private const string Resource = "groups";
        private const string ChangeType = "updated";
        private const int SubscriptionTimeToLive = 3;

        [FunctionName("ProcosysGroupSubscriber")]
        public static async Task Run([TimerTrigger(Cron)]TimerInfo myTimer,
           ILogger log)
        {
            var authority = Environment.GetEnvironmentVariable("AzureAuthority");
            var graphUrl = Environment.GetEnvironmentVariable("GraphUrl");
            var clientId = Environment.GetEnvironmentVariable("AzureClientId");
            var clientSecret = Environment.GetEnvironmentVariable("AzureClientSecret");
            var clientState = Environment.GetEnvironmentVariable("SubscriptionClientState");
            var notificationUrl = Environment.GetEnvironmentVariable("NotificationUrl");
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
            var subscription = new Subscription
            {
                ChangeType = ChangeType,
                NotificationUrl = notificationUrl,
                Resource = Resource,
                ExpirationDateTime = DateTimeOffset.UtcNow.AddDays(SubscriptionTimeToLive),
                ClientState = clientState
            };

            var request = graphClient.Subscriptions.Request();
            log.LogInformation($"Subscribing to microsoft graph with with request: {request}");
            await request.AddAsync(subscription);
        }
    }
}
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using static System.Environment;

namespace AccessFunctions
{
    public static class ProcosysGroupSubscriber
    {
        private const string Cron = "0 2 * * *"; //Every night at 2am //TODO add to env variable
        private const string Resource = "groups";
        private const string ChangeType = "updated";

        [FunctionName("ProcosysGroupSubscriber")]
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
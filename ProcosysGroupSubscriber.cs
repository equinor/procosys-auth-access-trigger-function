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

        [FunctionName("ProcosysGroupSubscriber")]
        public static async Task Run(
           [TimerTrigger(Cron)]TimerInfo myTimer,
           ILogger log)
        {
            var authority = Environment.GetEnvironmentVariable("Azure:Authority");
            var graphUrl = Environment.GetEnvironmentVariable("GraphUrl");
            var clientId = Environment.GetEnvironmentVariable("Azure:ClientId");
            var clientSecret = Environment.GetEnvironmentVariable("Azure:ClientSecret");
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
                    ChangeType = "updated",
                    NotificationUrl = "https://pcs-auth-access-function-dev.azurewebsites.net/api/ProcosysAuthAccessTrigger",
                    Resource = "groups", 
                    ExpirationDateTime = DateTimeOffset.UtcNow.AddDays(3),
                    ClientState = "sicktrickpony"
                };

                var request = graphClient.Subscriptions.Request();
                await request.AddAsync(subscription);
        }



    }


}
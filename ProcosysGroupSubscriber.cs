using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace AccessFunctions
{
    public static class ProcosysGroupSubscriber
    {
        private const string Cron = "123";

         [FunctionName("ProcosysGroupSubscriber")]
        public static async Task Run(
            [TimerTrigger("c")]
            string graphToken,
            ILogger log)
        {


    //TODO https://docs.microsoft.com/en-us/graph/tutorials/dotnet-core?tutorial-step=4
            //save secrets
            //send request with subscription to graph service
            var authority = "abc";
            var graphUrl = "abc";
            var clientId = "abc";
            var clientSecret = "bc";
            //await GraphService
            var authContext = new AuthenticationContext(authority);
            ClientCredential clientCred = new ClientCredential(clientId, clientSecret);
            var authenticationResult = await authContext.AcquireTokenAsync(graphUrl, clientCred);

            //call graph api
/*
            GraphServiceClient graphClient = new GraphServiceClient( authProvider );

var subscription = new Subscription
{
	ChangeType = "created,updated",
	NotificationUrl = "https://webhook.azurewebsites.net/api/send/myNotifyClient",
	Resource = "me/mailFolders('Inbox')/messages",
	ExpirationDateTime = DateTimeOffset.Parse("2016-11-20T18:23:45.9356913Z"),
	ClientState = "secretClientValue"
};

await graphClient.Subscriptions
	.Request()
	.AddAsync(subscription);


        }



    }
*/
    
}
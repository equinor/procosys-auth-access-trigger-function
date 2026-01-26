using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AccessFunctions
{
   public static class AccessTriggerHelper
    {
        private const string Deleted = "deleted";
        private const string ValidationToken = "validationToken";
        private const string ClientState = "SubscriptionClientState";
        private const string Value = "value";

        public static string GetValueToken(HttpRequestData request)
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
            var token = queryParams[ValidationToken];
            return !string.IsNullOrWhiteSpace(token) ? token : null;
        }

        public static async Task<List<string>> ExtractNotifications(HttpRequestData req, ILogger logger)
        {
            var notifications = new List<string>();
            using var inputStream = new StreamReader(req.Body);
            var jsonContent = await inputStream.ReadToEndAsync();
            var jsonObject = JsonNode.Parse(jsonContent);
            
            if (jsonObject != null)
            {
                // Notifications are sent in a 'value' array. The array might contain multiple notifications for events that are
                // registered for the same notification endpoint, and that occur within a short timespan.
                notifications.AddRange(GetNotifications(jsonObject, logger));
            }
            return notifications;
        }

        private static IEnumerable<string> GetNotifications(JsonNode jsonObject, ILogger logger)
        {
            var valueArray = jsonObject[Value]?.AsArray();
            if (valueArray == null)
            {
                yield break;
            }

            foreach (var value in valueArray)
            {
                var notification = JsonSerializer.Deserialize<Notification>(value.ToJsonString());
                var isValid = HasValidClientState(notification);

                if (isValid && notification.ResourceData.Members != null)
                {
                    yield return CreateJsonString(notification);
                }
                else if (!isValid)
                {
                    logger.LogInformation("ClientState wrong, the client state used was: {ClientState}", notification.ClientState);
                }
            }
        }

        private static string CreateJsonString(Notification notification)
        {
            var result = new
            {
                groupId = notification.ResourceData.Id,
                members = notification.ResourceData.Members
                  .Select(m => new { id = m.Id, remove = Deleted.Equals(m.Removed) })
            };
            
            return JsonSerializer.Serialize(result);
        }

        private static bool HasValidClientState(Notification current) =>
            // Check client state to verify the message is from Microsoft Graph.
            current.ClientState.Equals(Environment.GetEnvironmentVariable(ClientState));
    }
}

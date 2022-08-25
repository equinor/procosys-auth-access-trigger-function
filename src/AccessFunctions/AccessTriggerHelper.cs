using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AccessFunctions
{
   public static class AccessTriggerHelper
    {
        private const string Deleted = "deleted";
        private const string ValidationToken = "validationToken";
        private const string ClientState = "SubscriptionClientState";
        private const string Value = "value";

        public static string GetValueToken(HttpRequest request)
        {
            var token = request.GetQueryParameterDictionary()
                .FirstOrDefault(q => string.Compare(q.Key, ValidationToken, StringComparison.OrdinalIgnoreCase) == 0).Value;

            return !string.IsNullOrWhiteSpace(token) ? token : null;
        }

        public static List<string> ExtractNotifications(HttpRequest req, ILogger logger)
        {
            var notifications = new List<string>();
            using var inputStream = new StreamReader(req.Body);
            var jsonObject = JObject.Parse(inputStream.ReadToEnd());
            if (jsonObject != null)
            {
                // Notifications are sent in a 'value' array. The array might contain multiple notifications for events that are
                // registered for the same notification endpoint, and that occur within a short timespan.
                notifications.AddRange(GetNotifications(jsonObject,logger));
            }
            return notifications;
        }

        private static IEnumerable<string> GetNotifications(JObject jsonObject, ILogger logger)
        {
            foreach (var value in ExtractValues(jsonObject))
            {
                var notification = JsonConvert.DeserializeObject<Notification>(value.ToString());
                var isValid = HasValidClientState(notification);

                if (isValid && notification.ResourceData.Members != null)
                {
                    yield return CreateJsonString(notification);
                }
                else if (!isValid)
                {
                    logger.LogInformation($"ClientState wrong, the client state used was: {notification.ClientState}");
                }
            }
        }

        private static JArray ExtractValues(JObject jsonObject)
        {
            return JArray.Parse(jsonObject[Value].ToString());
        }

        private static string CreateJsonString(Notification notification)
        {
            return JObject.FromObject(new
            {
                groupId = notification.ResourceData.Id,
                members = notification.ResourceData.Members
                  .Select(m => new { id = m.Id, remove = Deleted.Equals(m.Removed) })
            }).ToString();
        }

        private static bool HasValidClientState(Notification current)
        {
            // Check client state to verify the message is from Microsoft Graph.
            return current.ClientState.Equals(Environment.GetEnvironmentVariable(ClientState));
        }
    }
}

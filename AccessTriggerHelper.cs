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
        private const string DELETED = "deleted";
        private const string VALIDATION_TOKEN = "validationToken";
        private const string CLIENT_STATE = "SubscriptionClientState";

        public static string GetToken(HttpRequest request)
        {
            return request.GetQueryParameterDictionary()
                .FirstOrDefault(q => string.Compare(q.Key, VALIDATION_TOKEN, true) == 0).Value;
        }

        public static List<string> ExtractJsonNotifications(HttpRequest req, ILogger logger)
        {
            var notifications = new List<string>();
            using (var inputStream = new StreamReader(req.Body))
            {
                JObject jsonObject = JObject.Parse(inputStream.ReadToEnd());
                if (jsonObject != null)
                {
                    // Notifications are sent in a 'value' array. The array might contain multiple notifications for events that are
                    // registered for the same notification endpoint, and that occur within a short timespan.
                    var value = JArray.Parse(jsonObject["value"].ToString());
                    foreach (var notification in value)
                    {
                        Notification current = JsonConvert.DeserializeObject<Notification>(notification.ToString());

                        // Check client state to verify the message is from Microsoft Graph.
                        var hasValidClientState = current.ClientState.Equals(Environment.GetEnvironmentVariable(CLIENT_STATE));
                        if (!hasValidClientState)
                        {
                            logger.LogInformation($"ClientState wrong, the clientstate used was: {current.ClientState}");
                        }

                        if (hasValidClientState && current.ResourceData.Members != null)
                        {
                            var json = JObject.FromObject(new
                            {
                                groupId = current.ResourceData.Id,
                                members = current.ResourceData.Members
                                  .Select(m => new { id = m.Id, remove = DELETED.Equals(m.Removed) })
                            }).ToString();
                            notifications.Add(json);
                        }
                    }
                }
                return notifications;
            }
        }
    }
}

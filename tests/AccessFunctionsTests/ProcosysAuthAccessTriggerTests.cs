using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Http;
using Moq;
using System.IO;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System;
using Newtonsoft.Json.Linq;

namespace AccessFunctions.Tests
{
    [TestClass]
    public class ProcosysAuthAccessTriggerTests
    {
        private const string MemberOid = "8eed7b20-13f5-41d6-9b82-add95fdd6860";
        private const string GroupOid = "88e74a06-8f81-43c6-9a3d-7428d29f826d";

        public ProcosysAuthAccessTriggerTests()
        {
            SetUpEnvironmentalVariables();
        }

        [TestMethod]
        public void ExtractJsonNotifications_ParsesCorrectly_WhenValidRequest()
        {
            //Arrange
            var req = CreateValidRequest();
            var logger = new Mock<ILogger>();

            //Act
            var result = AccessTriggerHelper.ExtractNotifications(req, logger.Object);

            //Assert
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].Contains(MemberOid));
            Assert.IsTrue(result[0].Contains(GroupOid));
        }

        [TestMethod]
        public void ExtractJsonNotifications_Fails_WhenInvalidValidClientState()
        {
            //Arrange
            var req = CreateInValidRequest();
            var logger = new Mock<ILogger>();

            //Act
            var result = AccessTriggerHelper.ExtractNotifications(req, logger.Object);

            //Assert
            Assert.AreEqual(0, result.Count);
        }

        private HttpRequest CreateInValidRequest()
        {
            var payload = new { value = new object[] { new
            {
                changetype = "updated",
                clientState  = "someInvalidState",
                resourceData = new
                {
                    id =  GroupOid,
                    Members = new object[] { new
                    {
                        id = MemberOid
                    }}
                }
            }}};
            return CreateMockRequest(payload).Object;
        }

        private HttpRequest CreateValidRequest()
        {
            var payload = new { value = new object[] { new
            {
                changetype = "updated",
                clientState  =  Environment.GetEnvironmentVariable("SubscriptionClientState"),
                resourceData = new
                {
                    id =  GroupOid,
                    Members = new object[] { new
                    {
                        id = MemberOid
                    }}
                }
            }}};
            return CreateMockRequest(payload).Object;
        }

        private static Mock<HttpRequest> CreateMockRequest(object body)
        {
            var ms = new MemoryStream();
            var sw = new StreamWriter(ms);

            // JsonConvert.PopulateObject("Members", "members@delta");
            var json = JsonConvert.SerializeObject(body);

            //@is not allowed in propertyname
            json = json.Replace("Members", "members@delta");
            sw.Write(json);
            sw.Flush();

            ms.Position = 0;

            var mockRequest = new Mock<HttpRequest>();
            mockRequest.Setup(x => x.Body).Returns(ms);
            return mockRequest;
        }

        private static void SetUpEnvironmentalVariables()
        {
            using (var file = File.OpenText("test.settings.json"))
            {
                var reader = new JsonTextReader(file);
                var jObject = JObject.Load(reader);

                var variables = jObject
                    .GetValue("Values").Children<JProperty>();

                foreach (var variable in variables)
                {
                    Environment.SetEnvironmentVariable(variable.Name, variable.Value.ToString());
                }
            }
        }
    }
}
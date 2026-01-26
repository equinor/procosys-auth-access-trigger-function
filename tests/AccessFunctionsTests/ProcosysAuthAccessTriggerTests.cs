using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Moq;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using AccessFunctions;
using System.Threading.Tasks;
using System.Net;

namespace AccessFunctionsTests
{
    [TestClass]
    public class ProCoSysAuthAccessTriggerTests
    {
        private const string MemberOid = "8eed7b20-13f5-41d6-9b82-add95fdd6860";
        private const string GroupOid = "88e74a06-8f81-43c6-9a3d-7428d29f826d";

        public ProCoSysAuthAccessTriggerTests()
        {
            SetUpEnvironmentalVariables();
        }

        [TestMethod]
        public async Task ExtractJsonNotifications_ParsesCorrectly_WhenValidRequest()
        {
            //Arrange
            var req = CreateValidRequest();
            var logger = new Mock<ILogger>();

            //Act
            var result = await AccessTriggerHelper.ExtractNotifications(req, logger.Object);

            //Assert
            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result[0].Contains(MemberOid));
            Assert.IsTrue(result[0].Contains(GroupOid));
        }

        [TestMethod]
        public async Task ExtractJsonNotifications_Fails_WhenInvalidValidClientState()
        {
            //Arrange
            var req = CreateInValidRequest();
            var logger = new Mock<ILogger>();

            //Act
            var result = await AccessTriggerHelper.ExtractNotifications(req, logger.Object);

            //Assert
            Assert.AreEqual(0, result.Count);
        }

        private static HttpRequestData CreateInValidRequest()
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
            return CreateMockRequest(payload);
        }

        private static HttpRequestData CreateValidRequest()
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
            return CreateMockRequest(payload);
        }

        private static HttpRequestData CreateMockRequest(object body)
        {
            var ms = new MemoryStream();
            var sw = new StreamWriter(ms);

            var json = JsonSerializer.Serialize(body);

            json = json.Replace("Members", "members@delta");
            sw.Write(json);
            sw.Flush();

            ms.Position = 0;

            var context = new Mock<FunctionContext>();
            var request = new TestHttpRequestData(context.Object, new Uri("https://test.com"), ms);
            return request;
        }

        private static void SetUpEnvironmentalVariables()
        {
            using var file = File.OpenText("test.settings.json");
            var jsonContent = file.ReadToEnd();
            var jsonObject = JsonNode.Parse(jsonContent);

            var variables = jsonObject["Values"].AsObject();

            foreach (var variable in variables)
            {
                Environment.SetEnvironmentVariable(variable.Key, variable.Value.ToString());
            }
        }
    }

    public class TestHttpRequestData : HttpRequestData
    {
        private readonly Stream _body;

        public TestHttpRequestData(FunctionContext functionContext, Uri url, Stream body) 
            : base(functionContext)
        {
            Url = url;
            _body = body;
        }

        public override Stream Body => _body;

        public override HttpHeadersCollection Headers => new HttpHeadersCollection();

        public override IReadOnlyCollection<IHttpCookie> Cookies => new List<IHttpCookie>();

        public override Uri Url { get; }

        public override IEnumerable<System.Security.Claims.ClaimsIdentity> Identities => new List<System.Security.Claims.ClaimsIdentity>();

        public override string Method => "POST";

        public override HttpResponseData CreateResponse()
        {
            return new TestHttpResponseData(FunctionContext, HttpStatusCode.OK);
        }
    }

    public class TestHttpResponseData : HttpResponseData
    {
        public TestHttpResponseData(FunctionContext functionContext, HttpStatusCode statusCode) 
            : base(functionContext)
        {
            StatusCode = statusCode;
            Body = new MemoryStream();
        }

        public override HttpStatusCode StatusCode { get; set; }

        public override HttpHeadersCollection Headers { get; set; } = new HttpHeadersCollection();

        public override Stream Body { get; set; }

        public override HttpCookies Cookies => throw new NotImplementedException();
    }
}
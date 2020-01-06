using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ASP_Core_MVC_Template.Utilities
{
    public class DataAPI
    {
        private readonly ILogger<Controller> _logger;

        public DataAPI(ILogger<Controller> logger)
        {
            _logger = logger;
        }

        public async Task<JsonResult> ExecuteQuery(string endpoint, IConfiguration configuration, 
            HttpRequest request, bool isDevelopment)
        {
            JsonResult jsonResult;
            var baseAddress = new Uri(configuration["FMDataAPIURL"]);
            var environmentEndpoint = (!isDevelopment ? "/FMDataAPI" : "") + endpoint;
            using (var handler = new HttpClientHandler()
            {
                UseCookies = false,
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }
            })
            using (var client = new HttpClient(handler) { BaseAddress = baseAddress })
            {
                var message = new HttpRequestMessage(HttpMethod.Get, environmentEndpoint);
                var cookieName = configuration["SharedCookieName"];
                var cookieValue = request.Cookies[cookieName];
                message.Headers.Add("Cookie", cookieName + "=" + cookieValue);
                try
                {
                    _logger.LogInformation("Sending Data API request to " + baseAddress + environmentEndpoint);
                    var result = client.SendAsync(message).Result;
                    result.EnsureSuccessStatusCode();
                    var jsonObject = JObject.Parse(await result.Content.ReadAsStringAsync());
                    jsonResult = new JsonResult(jsonObject);
                    result.Dispose();
                    _logger.LogInformation("Received Data API response from " + baseAddress + environmentEndpoint);
                }
                catch (Exception ex)
                {
                    // Return an error object.
                    var errorString = "Failed to query the FM Data API: " + ex.Message;
                    jsonResult = new JsonResult(JObject.Parse("{ 'error' : '" + errorString + "' }"));
                }
                finally
                {
                    client.Dispose();
                }
            }

            return jsonResult;
        }

        public string ErrorFromResult(JsonResult result)
        {
            var resultObject = (JObject)result.Value;
            return (string)resultObject["error"];
        }

        public List<Dictionary<string, object>> QueryResultToDict(JsonResult result)
        {
            var queryDict = new List<Dictionary<string, object>>();

            var resultObject = (JObject)result.Value;
            var queryResult = (JObject)resultObject["result"];
            if (queryResult == null)
            {
                // Query failed.
                return queryDict;
            }
            var columnNames = (JArray)queryResult["columnNames"];
            var columnTypes = (JArray)queryResult["columnTypes"];
            var queryData = (JArray)queryResult["queryData"];
            if (columnNames == null || columnTypes == null || queryData == null)
            {
                // Query failed. Return empty list.
                return queryDict;
            }

            foreach (JArray dataRow in queryData)
            {
                var rowDict = new Dictionary<string, object>();
                for (int i = 0; i < columnNames.Count; i++)
                {
                    var columnName = (string)columnNames[i];
                    var columnType = Type.GetType((string)columnTypes[i]);
                    rowDict.Add(columnName, String.IsNullOrEmpty((string)dataRow[i]) ? null : Convert.ChangeType(dataRow[i], columnType));
                }

                queryDict.Add(rowDict);
            }

            return queryDict;
        }
    }
}

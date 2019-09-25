using Newtonsoft.Json;
using System;
using System.Net;

namespace Blindrelay.Core
{
    public class ApiError
    {
        [JsonProperty("c")]
        public string Code { get; set; }
        [JsonProperty("m")]
        public string Message { get; set; }
    }

    public class ApiErrorCollection
    {
        [JsonProperty("e")]
        public ApiError[] Errors { get; set; }

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static ApiErrorCollection FromException(Exception x)
        {
            return new ApiErrorCollection
            {
                Errors = new ApiError[] { new ApiError { Code = "Exception", Message = x.Message } }
            };
        }
    }
    public class ApiException : Exception
    {
        public ApiErrorCollection Errors { get; set; }
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
    }
}

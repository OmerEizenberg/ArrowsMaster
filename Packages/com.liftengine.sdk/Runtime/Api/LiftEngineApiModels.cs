using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LiftEngine.Api
{
    [Serializable]
    public class LiftEnginePredictResult
    {
        public string model;
        public string keyword;
        public string auction_id;
        public string param;
        public float prediction;
        public float[] multipliers;

        // Bound to the decoded predict JSON only when it contains a top-level key named
        // exactly "cpm". Newtonsoft matches the key name exactly, so "ecpm"/"ecpm_history"
        // never bind here. Stays null when the key is absent.
        [JsonProperty("cpm")]
        public JToken cpm;

        [JsonIgnore]
        public bool HasMultipliers =>
            multipliers != null && multipliers.Length > 0;

        // True only when the decoded predict JSON contained a "cpm" key (this is our placement).
        [JsonIgnore]
        public bool HasCpmKey => cpm != null;
    }

    public class LiftEngineError
    {
        public int StatusCode { get; }
        public string Message { get; }

        public LiftEngineError(int statusCode, string message)
        {
            StatusCode = statusCode;
            Message = message;
        }
    }

    internal class PredictRequestBody
    {
        public string[] models;
        public Context.PredictDataPayload data;
    }

    internal class ApiResponseArrayWrapper
    {
        // Newtonsoft parses top-level arrays directly
    }
}

using System;
using Newtonsoft.Json;

namespace LiftEngine.Api
{
    [Serializable]
    public class LiftEnginePredictResult
    {
        public string model;
        public string keyword;
        public string auction_id;
        public string param;
        public string message;
        public float prediction;
        public float[] multipliers;

        [JsonIgnore]
        public bool HasMultipliers =>
            multipliers != null && multipliers.Length > 0;
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

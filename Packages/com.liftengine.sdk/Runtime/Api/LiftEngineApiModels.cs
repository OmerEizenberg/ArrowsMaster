using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LiftEngine.Api
{
    [Serializable]
    internal class LiftEngineOptimizationResult
    {
        public string model;
        public string keyword;
        public string auction_id;
        public string param;
        [JsonProperty("prediction")]
        public float BaseValue;
        public float cpm;
        [JsonProperty("multipliers")]
        public float[] Factors;
        public string treatment;
        public Dictionary<string, int> group_ratios;

        [JsonIgnore]
        public bool HasFactors =>
            Factors != null && Factors.Length > 0;

        public void ResolveOptimizationValue(float fallback)
        {
            if (cpm > 0f)
                BaseValue = cpm;
            else if (BaseValue <= 0f)
                BaseValue = fallback;
        }
    }

    internal class LiftEngineError
    {
        public int StatusCode { get; }
        public string Message { get; }

        public LiftEngineError(int statusCode, string message)
        {
            StatusCode = statusCode;
            Message = message;
        }
    }

    internal sealed class LiftEngineTrackErrorParams
    {
        public string BundleId;
        public string DeviceId;
        public string AppVersion;
        public string AuctionId;
        public string ErrorCode;
        public string ErrorMessage;
        public string AdType;
        public string PlacementId;
        public string Keyword;
        public string AdUnitId;
        public long? Timestamp;
    }

    internal class OptimizationRequestBody
    {
        public string[] models;
        public Context.ContextPayload data;
    }

    internal class ApiResponseArrayWrapper
    {
        // Newtonsoft parses top-level arrays directly
    }
}

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
        public float prediction;
        public float cpm;
        public float[] multipliers;
        public string treatment;
        public Dictionary<string, int> group_ratios;

        [JsonIgnore]
        public bool HasMultipliers =>
            multipliers != null && multipliers.Length > 0;

        public void ResolveOptimizationValue(float fallback)
        {
            if (cpm > 0f)
                prediction = cpm;
            else if (prediction <= 0f)
                prediction = fallback;
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

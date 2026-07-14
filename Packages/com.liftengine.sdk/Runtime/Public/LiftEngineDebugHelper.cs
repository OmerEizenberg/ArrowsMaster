using Newtonsoft.Json;

namespace LiftEngine
{
    public static class LiftEngineDebugHelper
    {
        public static string TestIpCountryLookup()
        {
#if UNITY_EDITOR
            try
            {
                using var client = new System.Net.Http.HttpClient
                {
                    Timeout = System.TimeSpan.FromSeconds(5)
                };
                var body = client.GetStringAsync("https://www.cloudflare.com/cdn-cgi/trace")
                    .GetAwaiter()
                    .GetResult();
                var code = Context.IpCountryResolver.ParseTraceResponse(body);
                return string.IsNullOrEmpty(code) ? "FAILED (could not parse response)" : code;
            }
            catch (System.Exception ex)
            {
                return $"FAILED ({ex.Message})";
            }
#else
            return "Editor only";
#endif
        }

        public static string BuildContextPayloadPreview(LiftEngineAdFormat format, string installType = "Organic",
            string mediaSource = null)
        {
            var service = new Context.ReportContextService();
            service.Initialize();
            service.SetAttribution(installType, mediaSource);

            var settings = UnityEngine.Resources.Load<LiftEngineSettings>(LiftEngineSettings.DefaultResourcePath);
            var models = settings != null
                ? settings.GetAllModelNames()
                : new[] { "banner", "interstitial", "rewarded" };

            return JsonConvert.SerializeObject(new
            {
                models,
                data = service.BuildPayload(format)
            }, Formatting.Indented);
        }

        [System.Obsolete("Use BuildContextPayloadPreview")]
        public static string BuildPredictPayloadPreview(LiftEngineAdFormat format, string installType = "Organic",
            string mediaSource = null) =>
            BuildContextPayloadPreview(format, installType, mediaSource);
    }
}

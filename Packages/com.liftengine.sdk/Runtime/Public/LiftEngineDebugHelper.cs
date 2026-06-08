using Newtonsoft.Json;

namespace LiftEngine
{
    public static class LiftEngineDebugHelper
    {
        public static string BuildPredictPayloadPreview(LiftEngineAdFormat format, string installType = "Organic",
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
    }
}

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
            var model = settings != null ? settings.GetModelName(format) : format.ToString().ToLowerInvariant();

            return JsonConvert.SerializeObject(new
            {
                model,
                data = service.BuildPayload(format)
            }, Formatting.Indented);
        }
    }
}

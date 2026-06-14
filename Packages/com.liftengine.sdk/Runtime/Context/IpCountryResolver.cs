using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace LiftEngine.Context
{
    internal static class IpCountryResolver
    {
        private const float TimeoutSeconds = 5f;

        public static IEnumerator FetchCountryCode(Action<string> onComplete)
        {
            yield return FetchFromCloudflareTrace(onComplete);
        }

        private static IEnumerator FetchFromCloudflareTrace(Action<string> onComplete)
        {
            const string url = "https://www.cloudflare.com/cdn-cgi/trace";
            using var request = UnityWebRequest.Get(url);
            request.timeout = Mathf.CeilToInt(TimeoutSeconds);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                LiftEngineLogger.Log($"IP country lookup failed: {request.error}");
                onComplete?.Invoke(null);
                yield break;
            }

            var code = ParseCloudflareTrace(request.downloadHandler?.text);
            if (!string.IsNullOrEmpty(code))
            {
                onComplete?.Invoke(code);
                yield break;
            }

            LiftEngineLogger.Log("IP country lookup failed: could not parse Cloudflare trace response");
            onComplete?.Invoke(null);
        }

        private static string ParseCloudflareTrace(string body)
        {
            if (string.IsNullOrEmpty(body))
                return null;

            var lines = body.Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (!line.StartsWith("loc=", StringComparison.Ordinal))
                    continue;

                return DeviceCountryProvider.NormalizeCountryCode(line.Substring(4));
            }

            return null;
        }

        internal static string ParseTraceResponse(string body) => ParseCloudflareTrace(body);
    }
}

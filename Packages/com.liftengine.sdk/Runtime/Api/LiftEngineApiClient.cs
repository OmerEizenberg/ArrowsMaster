using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using LiftEngine.Ads;
using LiftEngine.Context;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace LiftEngine.Api
{
    internal sealed class LiftEngineApiClient
    {
        private readonly LiftEngineSettings _settings;
        private readonly MonoBehaviour _host;

        public LiftEngineApiClient(LiftEngineSettings settings, MonoBehaviour host)
        {
            _settings = settings;
            _host = host;
        }

        public void CheckHealth(Action<bool, string> callback)
        {
            _host.StartCoroutine(Get("GET", "/health/live", false, (code, body) =>
            {
                callback?.Invoke(code == 200, body);
            }));
        }

        public void RequestOptimization(string deviceId, LiftEngineAdFormat format, PredictDataPayload data,
            Action<LiftEngineOptimizationResult> onSuccess, Action<LiftEngineError> onFailure)
        {
            // Only the format being prewarmed. data (incl. ecpm_history) is format-specific —
            // batching all models with one payload made banner/IV/RV share the wrong history.
            var targetModel = _settings.GetModelName(format);
            // Defense-in-depth: payload must describe the same format we request.
            if (string.IsNullOrEmpty(data.ad_type))
                data.ad_type = targetModel;
            if (data.ecpm_history == null)
                data.ecpm_history = Array.Empty<float>();

            var body = new PredictRequestBody
            {
                models = new[] { targetModel },
                data = data
            };

            var json = JsonConvert.SerializeObject(body);
            var path = AppendQueryParam($"/api/v1/predict/{deviceId}", "ad_type", targetModel);
            var modelsLabel = targetModel;
            var historyLen = data.ecpm_history?.Length ?? 0;
            LiftEngineLogger.LogClient(
                $"Predict {targetModel} — ad_type={data.ad_type}, ecpm_history_len={historyLen}");
            _host.StartCoroutine(PostOptimization(path, modelsLabel, json, (code, response) =>
            {
                if (code == 204)
                {
                    onFailure?.Invoke(new LiftEngineError(204, "Optimization deadline exceeded"));
                    return;
                }

                if (code != 200)
                {
                    onFailure?.Invoke(new LiftEngineError(code, response));
                    return;
                }

                try
                {
                    var results = JsonConvert.DeserializeObject<List<LiftEngineOptimizationResult>>(response);
                    if (results == null || results.Count == 0)
                    {
                        onFailure?.Invoke(new LiftEngineError(code, "Empty optimization response"));
                        return;
                    }

                    LiftEngineOptimizationResult result = null;
                    foreach (var item in results)
                    {
                        if (item.model == targetModel)
                        {
                            result = item;
                            break;
                        }
                    }

                    if (result == null)
                    {
                        onFailure?.Invoke(new LiftEngineError(code, $"No optimization result for model '{targetModel}'"));
                        return;
                    }

                    result.ResolveOptimizationValue(LiftEngineRuntimeTuning.DefaultOptimizationFallback);

                    onSuccess?.Invoke(result);
                }
                catch (Exception ex)
                {
                    onFailure?.Invoke(new LiftEngineError(code, ex.Message));
                }
            }));
        }

        public void Report(string deviceId, PredictDataPayload data, Action<bool> callback)
        {
            var path = $"/api/v1/report/{deviceId}";
            if (!string.IsNullOrEmpty(data?.ad_type))
                path = AppendQueryParam(path, "ad_type", data.ad_type);

            // Format-scoped reports always send ecpm_history (possibly empty) — never omit.
            if (!string.IsNullOrEmpty(data?.ad_type) && data.ecpm_history == null)
                data.ecpm_history = Array.Empty<float>();

            var json = JsonConvert.SerializeObject(data);
            LiftEngineLogger.LogClient(
                $"Report — ad_type={data?.ad_type ?? "(none)"}, " +
                $"ecpm_history_len={data?.ecpm_history?.Length ?? 0}");
            _host.StartCoroutine(Post(path, json, (code, _) =>
            {
                callback?.Invoke(code == 200);
            }));
        }

        public void TrackInit(string deviceId, string appVersion, string platform)
        {
            var query = BuildTrackQuery(new Dictionary<string, string>
            {
                ["device_id"] = deviceId,
                ["app_version"] = appVersion,
                ["platform"] = platform
            }, null);

            _host.StartCoroutine(Get("GET", "/v1/init" + query, true, null));
        }

        public void TrackView(string bundleId, string deviceId, string adType, string placementId,
            string keyword, string auctionId, long timestamp, int mulIndex)
        {
            var query = BuildTrackQuery(new Dictionary<string, string>
            {
                ["bundle_id"] = bundleId,
                ["device_id"] = deviceId,
                ["app_version"] = Application.version,
                ["ad_type"] = adType ?? string.Empty,
                ["placement_id"] = placementId ?? string.Empty,
                ["plc"] = placementId ?? string.Empty,
                ["keyword"] = keyword ?? string.Empty,
                ["auction_id"] = auctionId ?? string.Empty,
                ["Mulindex"] = mulIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["timestamp"] = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }, null);

            _host.StartCoroutine(Get("GET", "/v1/track/view" + query, true, null));
        }

        public void TrackActiveView(string bundleId, string deviceId, string adType, string placementId,
            string keyword, string auctionId, long timestamp, float rev, int mulIndex)
        {
            var query = BuildTrackQuery(new Dictionary<string, string>
            {
                ["bundle_id"] = bundleId,
                ["device_id"] = deviceId,
                ["app_version"] = Application.version,
                ["ad_type"] = adType,
                ["placement_id"] = placementId ?? string.Empty,
                ["plc"] = placementId ?? string.Empty,
                ["keyword"] = keyword ?? string.Empty,
                ["auction_id"] = auctionId ?? string.Empty,
                ["Mulindex"] = mulIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["timestamp"] = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }, rev);

            _host.StartCoroutine(Get("GET", "/v1/track/activeview" + query, true, null));
        }

        public void TrackError(string bundleId, string deviceId, string auctionId, string errorCode,
            string errorMessage) =>
            TrackError(new LiftEngineTrackErrorParams
            {
                BundleId = bundleId,
                DeviceId = deviceId,
                AuctionId = auctionId,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            });

        public void TrackError(LiftEngineTrackErrorParams request)
        {
            if (request == null)
                return;

            var fields = new Dictionary<string, string>
            {
                ["bundle_id"] = request.BundleId ?? string.Empty,
                ["device_id"] = request.DeviceId ?? string.Empty,
                ["app_version"] = request.AppVersion ?? Application.version,
                ["auction_id"] = request.AuctionId ?? string.Empty,
                ["error_code"] = request.ErrorCode ?? string.Empty,
                ["error_message"] = request.ErrorMessage ?? string.Empty
            };

            if (!string.IsNullOrEmpty(request.AdType))
                fields["ad_type"] = request.AdType;
            if (!string.IsNullOrEmpty(request.PlacementId))
                fields["placement_id"] = request.PlacementId;
            if (!string.IsNullOrEmpty(request.Keyword))
                fields["keyword"] = request.Keyword;
            if (!string.IsNullOrEmpty(request.AdUnitId))
                fields["ad_unit_id"] = request.AdUnitId;
            if (request.Timestamp.HasValue)
                fields["timestamp"] = request.Timestamp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

            var query = BuildTrackQuery(fields, null);
            _host.StartCoroutine(Get("GET", "/v1/track/error" + query, true, null));
        }

        private static string BuildTrackQuery(Dictionary<string, string> fields, float? rev)
        {
            var sb = new StringBuilder("?");
            foreach (var pair in fields)
            {
                sb.Append(Uri.EscapeDataString(pair.Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(pair.Value ?? string.Empty));
                sb.Append('&');
            }

            if (rev.HasValue)
            {
                sb.Append("rev=");
                sb.Append(rev.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else if (sb.Length > 1 && sb[^1] == '&')
            {
                sb.Length--;
            }

            return sb.ToString();
        }

        private IEnumerator Get(string method, string path, bool auth, Action<int, string> callback)
        {
            path = EnsureCommonQueryParams(path);
            var url = _settings.ApiBaseUrl.TrimEnd('/') + path;
            LiftEngineLogger.LogClient($"{method} {path}");

            using var request = UnityWebRequest.Get(url);
            ApplyUserAgent(request);
            if (auth)
                request.SetRequestHeader("Authorization", "Bearer " + _settings.apiKey);

            request.timeout = Mathf.CeilToInt(LiftEngineRuntimeTuning.OptimizationTimeoutSeconds);
            yield return request.SendWebRequest();

            var body = request.downloadHandler?.text ?? string.Empty;
            var code = (int)request.responseCode;
            LogBackendResponse(method, path, code, body);
            callback?.Invoke(code, body);
        }

        private IEnumerator PostOptimization(string path, string modelsLabel, string json, Action<int, string> callback)
        {
            path = EnsureCommonQueryParams(path);
            var url = _settings.ApiBaseUrl.TrimEnd('/') + path;
            LiftEngineLogger.LogClient($"POST {path} models={modelsLabel} ({json.Length} bytes)");

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            ApplyUserAgent(request);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + _settings.apiKey);
            request.timeout = Mathf.CeilToInt(LiftEngineRuntimeTuning.OptimizationTimeoutSeconds);

            yield return request.SendWebRequest();

            var body = request.downloadHandler?.text ?? string.Empty;
            var code = (int)request.responseCode;

            if (code == 200)
            {
                if (ResponseDecoder.TryDecodeResponse(body, out var decrypted))
                {
                    LogBackendResponse("POST", $"{path} models={modelsLabel}", code, decrypted);
                    callback?.Invoke(code, decrypted);
                }
                else
                {
                    LogBackendResponse("POST", $"{path} models={modelsLabel}", code, body);
                    LiftEngineLogger.LogBackendWarning(
                        $"POST {path} models={modelsLabel} → failed to decode optimization response");
                    callback?.Invoke(code, body);
                }
            }
            else
            {
                LogBackendResponse("POST", $"{path} models={modelsLabel}", code, body);
                callback?.Invoke(code, body);
            }
        }

        private IEnumerator Post(string path, string json, Action<int, string> callback)
        {
            path = EnsureCommonQueryParams(path);
            var url = _settings.ApiBaseUrl.TrimEnd('/') + path;
            LiftEngineLogger.LogClient($"POST {path} ({json.Length} bytes)");

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            ApplyUserAgent(request);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + _settings.apiKey);
            request.timeout = Mathf.CeilToInt(LiftEngineRuntimeTuning.OptimizationTimeoutSeconds);

            yield return request.SendWebRequest();

            var body = request.downloadHandler?.text ?? string.Empty;
            var code = (int)request.responseCode;
            LogBackendResponse("POST", path, code, body);
            callback?.Invoke(code, body);
        }

        private static string AppendQueryParam(string path, string key, string value)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(key))
                return path;

            var sep = path.Contains("?") ? "&" : "?";
            return path + sep + Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value ?? string.Empty);
        }

        /// <summary>
        /// Guarantees <c>device_id</c> and <c>app_version</c> on every LiftEngine request URL
        /// (GET and POST). Skips a param when it is already present in the query string.
        /// </summary>
        private static string EnsureCommonQueryParams(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = "/";

            var additions = new List<KeyValuePair<string, string>>(2);
            if (path.IndexOf("device_id=", StringComparison.OrdinalIgnoreCase) < 0)
                additions.Add(new KeyValuePair<string, string>("device_id", DeviceIdProvider.GetDeviceId()));
            if (path.IndexOf("app_version=", StringComparison.OrdinalIgnoreCase) < 0)
                additions.Add(new KeyValuePair<string, string>("app_version", Application.version));

            if (additions.Count == 0)
                return path;

            var sb = new StringBuilder(path);
            sb.Append(path.Contains("?") ? '&' : '?');
            for (var i = 0; i < additions.Count; i++)
            {
                if (i > 0)
                    sb.Append('&');

                sb.Append(Uri.EscapeDataString(additions[i].Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(additions[i].Value ?? string.Empty));
            }

            return sb.ToString();
        }

        private static void ApplyUserAgent(UnityWebRequest request) =>
            request.SetRequestHeader("User-Agent", ClassicUserAgentProvider.Build());

        private static void LogBackendResponse(string method, string path, int code, string body)
        {
            var trimmed = TruncateBody(body);
            if (code >= 200 && code < 300)
                LiftEngineLogger.LogBackend($"{method} {path} → {code}: {trimmed}");
            else
                LiftEngineLogger.LogBackendWarning($"{method} {path} → {code}: {trimmed}");
        }

        private static string TruncateBody(string body, int maxLength = 512)
        {
            if (string.IsNullOrEmpty(body))
                return "(empty)";

            return body.Length <= maxLength ? body : body.Substring(0, maxLength) + "…";
        }
    }
}

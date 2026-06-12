using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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

        public void Predict(string deviceId, LiftEngineAdFormat format, PredictDataPayload data,
            Action<LiftEnginePredictResult> onSuccess, Action<LiftEngineError> onFailure)
        {
            var body = new PredictRequestBody
            {
                models = _settings.GetAllModelNames(),
                data = data
            };

            var json = JsonConvert.SerializeObject(body);
            var path = $"/api/v1/predict/{deviceId}";
            var modelsLabel = string.Join(",", body.models);
            var targetModel = _settings.GetModelName(format);
            _host.StartCoroutine(PostPredict(path, modelsLabel, json, (code, response) =>
            {
                if (code == 204)
                {
                    onFailure?.Invoke(new LiftEngineError(204, "Predict deadline exceeded"));
                    return;
                }

                if (code != 200)
                {
                    onFailure?.Invoke(new LiftEngineError(code, response));
                    return;
                }

                try
                {
                    var results = JsonConvert.DeserializeObject<List<LiftEnginePredictResult>>(response);
                    if (results == null || results.Count == 0)
                    {
                        onFailure?.Invoke(new LiftEngineError(code, "Empty predict response"));
                        return;
                    }

                    LiftEnginePredictResult result = null;
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
                        onFailure?.Invoke(new LiftEngineError(code, $"No predict result for model '{targetModel}'"));
                        return;
                    }

                    if (result.prediction <= 0f)
                        result.prediction = _settings.defaultPredictionFallback;

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
            var json = JsonConvert.SerializeObject(data);
            _host.StartCoroutine(Post($"/api/v1/report/{deviceId}", json, (code, _) =>
            {
                callback?.Invoke(code == 200);
            }));
        }

        public void TrackView(string bundleId, string deviceId, string placementId, string keyword, string auctionId,
            long timestamp, float? rev)
        {
            var query = BuildTrackQuery(new Dictionary<string, string>
            {
                ["bundle_id"] = bundleId,
                ["device_id"] = deviceId,
                ["placement_id"] = placementId,
                ["keyword"] = keyword ?? string.Empty,
                ["auction_id"] = auctionId ?? string.Empty,
                ["timestamp"] = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }, rev);

            _host.StartCoroutine(Get("GET", "/v1/track/view" + query, true, null));
        }

        public void TrackActiveView(string bundleId, string deviceId, string adType, string placementId,
            string keyword, string auctionId, long timestamp, float? rev)
        {
            var query = BuildTrackQuery(new Dictionary<string, string>
            {
                ["bundle_id"] = bundleId,
                ["device_id"] = deviceId,
                ["ad_type"] = adType,
                ["placement_id"] = placementId,
                ["keyword"] = keyword ?? string.Empty,
                ["auction_id"] = auctionId ?? string.Empty,
                ["timestamp"] = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }, rev);

            _host.StartCoroutine(Get("GET", "/v1/track/activeview" + query, true, null));
        }

        public void TrackError(string bundleId, string auctionId, string errorCode, string errorMessage)
        {
            var query = "?bundle_id=" + Uri.EscapeDataString(bundleId)
                        + "&auction_id=" + Uri.EscapeDataString(auctionId ?? string.Empty)
                        + "&error_code=" + Uri.EscapeDataString(errorCode)
                        + "&error_message=" + Uri.EscapeDataString(errorMessage);
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
            var url = _settings.ApiBaseUrl.TrimEnd('/') + path;
            LiftEngineLogger.LogClient($"{method} {path}");

            using var request = UnityWebRequest.Get(url);
            if (auth)
                request.SetRequestHeader("Authorization", "Bearer " + _settings.apiKey);

            request.timeout = Mathf.CeilToInt(_settings.predictTimeoutSeconds);
            yield return request.SendWebRequest();

            var body = request.downloadHandler?.text ?? string.Empty;
            var code = (int)request.responseCode;
            LogBackendResponse(method, path, code, body);
            callback?.Invoke(code, body);
        }

        private IEnumerator PostPredict(string path, string modelsLabel, string json, Action<int, string> callback)
        {
            var url = _settings.ApiBaseUrl.TrimEnd('/') + path;
            LiftEngineLogger.LogClient($"POST {path} models={modelsLabel} ({json.Length} bytes)");

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + _settings.apiKey);
            request.timeout = Mathf.CeilToInt(_settings.predictTimeoutSeconds);

            yield return request.SendWebRequest();

            var body = request.downloadHandler?.text ?? string.Empty;
            var code = (int)request.responseCode;

            if (code == 200)
            {
                if (LiftEngineXorCipher.TryDecodePredictResponse(body, out var decrypted))
                {
                    LogBackendResponse("POST", $"{path} models={modelsLabel}", code, decrypted);
                    callback?.Invoke(code, decrypted);
                }
                else
                {
                    LogBackendResponse("POST", $"{path} models={modelsLabel}", code, body);
                    LiftEngineLogger.LogBackendWarning(
                        $"POST {path} models={modelsLabel} → failed to decrypt predict response");
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
            var url = _settings.ApiBaseUrl.TrimEnd('/') + path;
            LiftEngineLogger.LogClient($"POST {path} ({json.Length} bytes)");

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + _settings.apiKey);
            request.timeout = Mathf.CeilToInt(_settings.predictTimeoutSeconds);

            yield return request.SendWebRequest();

            var body = request.downloadHandler?.text ?? string.Empty;
            var code = (int)request.responseCode;
            LogBackendResponse("POST", path, code, body);
            callback?.Invoke(code, body);
        }

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

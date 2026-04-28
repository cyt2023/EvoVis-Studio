using System;
using System.Collections;
using System.Text;
using ImmersiveTaxiVis.Integration.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace ImmersiveTaxiVis.Integration.Runtime
{
    [Serializable]
    public class BackendDatasetListResponse
    {
        public string status;
        public BackendDatasetInfo[] datasets;
    }

    [Serializable]
    public class BackendDatasetInfo
    {
        public string id;
        public string label;
        public string path;
        public string relativePath;
        public int rowCount;
    }

    [Serializable]
    public class BackendWorkflowRunRequest
    {
        public string task;
        public string dataset;
        public string workflowId = "test3";
        public string viewType = "Auto";
        public bool execute = true;
        public bool requireLlm = true;
        public string taskId;
        public int population = 6;
        public int generations = 3;
        public int eliteSize = 2;
        public int timeoutSeconds = 180;
        public int limit = 1000;
        public bool includeSelectedIds = false;
    }

    public class BackendWorkflowClient
    {
        private readonly string baseUrl;
        private readonly int timeoutSeconds;

        public BackendWorkflowClient(string baseUrl, int timeoutSeconds = 30)
        {
            this.baseUrl = NormalizeBaseUrl(baseUrl);
            this.timeoutSeconds = Mathf.Max(1, timeoutSeconds);
        }

        public IEnumerator CheckHealth(Action<bool, string> onComplete)
        {
            yield return GetText("/api/health", onComplete);
        }

        public IEnumerator FetchDatasets(Action<bool, string> onComplete)
        {
            yield return GetText("/api/datasets", onComplete);
        }

        public IEnumerator FetchUnityRenderJson(string workflowId, Action<bool, string> onComplete, int limit = 1000, bool includeSelectedIds = false)
        {
            var safeWorkflowId = string.IsNullOrWhiteSpace(workflowId) ? "test3" : workflowId.Trim();
            var query = string.Format(
                "?limit={0}&includeSelectedIds={1}",
                Mathf.Max(1, limit),
                includeSelectedIds ? "true" : "false");
            yield return GetText("/api/render/" + UnityWebRequest.EscapeURL(safeWorkflowId) + query, onComplete);
        }

        public IEnumerator FetchRawWorkflowJson(string workflowId, Action<bool, string> onComplete)
        {
            var safeWorkflowId = string.IsNullOrWhiteSpace(workflowId) ? "test3" : workflowId.Trim();
            yield return GetText("/api/workflow/" + UnityWebRequest.EscapeURL(safeWorkflowId), onComplete);
        }

        public IEnumerator RunWorkflowForUnityRender(BackendWorkflowRunRequest runRequest, Action<bool, string> onComplete)
        {
            var request = runRequest ?? new BackendWorkflowRunRequest();
            if (string.IsNullOrWhiteSpace(request.workflowId))
                request.workflowId = "test3";

            yield return PostJson("/api/render/run", JsonUtility.ToJson(request), onComplete);
        }

        public IEnumerator RunWorkflowRaw(BackendWorkflowRunRequest runRequest, Action<bool, string> onComplete)
        {
            var request = runRequest ?? new BackendWorkflowRunRequest();
            if (string.IsNullOrWhiteSpace(request.workflowId))
                request.workflowId = "test3";

            yield return PostJson("/api/workflow/run", JsonUtility.ToJson(request), onComplete);
        }

        public static bool TryParseBackendError(string json, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                var errorEnvelope = JsonUtility.FromJson<BackendErrorEnvelope>(json);
                if (errorEnvelope == null)
                    return false;

                if (!string.Equals(errorEnvelope.status, "failed", StringComparison.OrdinalIgnoreCase))
                    return false;

                var stage = errorEnvelope.error != null ? errorEnvelope.error.stage : string.Empty;
                var errorMessage = errorEnvelope.error != null ? errorEnvelope.error.message : string.Empty;
                var details = errorEnvelope.error != null ? errorEnvelope.error.details : string.Empty;

                var builder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(stage))
                {
                    builder.Append(stage);
                    builder.Append(": " );
                }

                builder.Append(string.IsNullOrWhiteSpace(errorMessage) ? "Backend request failed." : errorMessage);

                if (!string.IsNullOrWhiteSpace(details))
                {
                    builder.Append(" | " );
                    builder.Append(details);
                }

                message = builder.ToString();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryParseRenderResult(string json, out BackendResultRoot result, out string errorMessage)
        {
            result = null;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = "Backend response was empty.";
                return false;
            }

            if (TryParseBackendError(json, out errorMessage))
                return false;

            try
            {
                result = JsonUtility.FromJson<BackendResultRoot>(json);
            }
            catch (Exception ex)
            {
                errorMessage = "Backend JSON parse failed: " + ex.Message;
                return false;
            }

            if (result == null)
            {
                errorMessage = "Backend response could not be parsed as BackendResultRoot.";
                return false;
            }

            if (result.visualizationPayload == null || result.visualizationPayload.views == null || result.visualizationPayload.views.Length == 0)
            {
                errorMessage = "Backend response did not contain any renderable views.";
                return false;
            }

            return true;
        }

        private IEnumerator GetText(string relativePath, Action<bool, string> onComplete)
        {
            using (var request = UnityWebRequest.Get(baseUrl + relativePath))
            {
                request.timeout = timeoutSeconds;
                yield return request.SendWebRequest();

                var responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (TryParseBackendError(responseText, out var backendError))
                    {
                        onComplete?.Invoke(false, request.error + " | " + backendError);
                        yield break;
                    }

                    onComplete?.Invoke(false, request.error + " | " + responseText);
                    yield break;
                }

                if (TryParseBackendError(responseText, out var successPathError))
                {
                    onComplete?.Invoke(false, successPathError);
                    yield break;
                }

                onComplete?.Invoke(true, responseText);
            }
        }

        private IEnumerator PostJson(string relativePath, string jsonBody, Action<bool, string> onComplete)
        {
            using (var request = new UnityWebRequest(baseUrl + relativePath, "POST"))
            {
                var bodyRaw = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(jsonBody) ? "{}" : jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = timeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                var responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (TryParseBackendError(responseText, out var backendError))
                    {
                        onComplete?.Invoke(false, request.error + " | " + backendError);
                        yield break;
                    }

                    onComplete?.Invoke(false, request.error + " | " + responseText);
                    yield break;
                }

                if (TryParseBackendError(responseText, out var successPathError))
                {
                    onComplete?.Invoke(false, successPathError);
                    yield break;
                }

                onComplete?.Invoke(true, responseText);
            }
        }

        private static string NormalizeBaseUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "http://127.0.0.1:8000";

            return value.Trim().TrimEnd('/');
        }
    }
}

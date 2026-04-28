using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace ImmersiveTaxiVis.Integration.Runtime
{
    public class DesktopBackendServiceController : MonoBehaviour
    {
        [Header("Backend Endpoint")]
        public string backendBaseUrl = "http://127.0.0.1:8000";
        public string backendHost = "127.0.0.1";
        public int backendPort = 8000;

        [Header("Lifecycle")]
        public bool autoStartOnAwake = true;
        public bool stopOwnedProcessOnApplicationQuit = true;
        public float startupTimeoutSeconds = 30f;
        public float healthPollIntervalSeconds = 0.5f;

        [Header("Path Resolution")]
        public string backendRootOverride = string.Empty;
        public string pythonExecutableOverride = string.Empty;
        public bool preferLauncherScript = true;
        public string serverScriptRelativePath = "server.py";
        public string unixLauncherRelativePath = "run_backend_server.sh";
        public string windowsLauncherRelativePath = "run_backend_server.bat";
        public string bundledBackendRelativePath = "EvoFlowBackend";

        [Header("Debug")]
        public bool verboseLogging = true;

        private Process backendProcess;
        private bool isStarting;
        private bool isHealthy;
        private string lastStatusMessage = "Idle.";
        private string resolvedBackendRoot = string.Empty;
        private float lastHealthCheckTime = -999f;

        public bool IsHealthy => isHealthy;
        public bool IsStarting => isStarting;
        public bool OwnsRunningProcess => IsProcessAlive(backendProcess);
        public string StatusMessage => lastStatusMessage;
        public string ResolvedBackendRoot => resolvedBackendRoot;
        public string EndpointSummary => string.Format("{0}:{1}", backendHost, backendPort);
        public string ResolutionHint =>
            "Expected layout: Unity project/build beside 'OperatorsDraft', or bundle backend under StreamingAssets/EvoFlowBackend.";

        private void Awake()
        {
            SyncEndpointFromBaseUrl();
            if (autoStartOnAwake)
                StartCoroutine(EnsureBackendReady());
        }

        private void OnApplicationQuit()
        {
            if (stopOwnedProcessOnApplicationQuit)
                StopOwnedBackendProcess();
        }

        [ContextMenu("Start Or Attach Backend")]
        public void StartBackendFromContextMenu()
        {
            StartCoroutine(EnsureBackendReady());
        }

        [ContextMenu("Restart Backend")]
        public void RestartBackendFromContextMenu()
        {
            StartCoroutine(RestartBackend());
        }

        [ContextMenu("Stop Backend")]
        public void StopBackendFromContextMenu()
        {
            StopOwnedBackendProcess();
        }

        public IEnumerator EnsureBackendReady()
        {
            SyncEndpointFromBaseUrl();

            if (isStarting)
            {
                while (isStarting)
                    yield return null;
                yield break;
            }

            isStarting = true;
            lastStatusMessage = "Checking backend availability...";

            if (backendProcess != null && !IsProcessAlive(backendProcess))
                backendProcess = null;

            if (IsBackendPortOpen())
            {
                var healthOk = false;
                yield return CheckHealth((ok, _) => healthOk = ok, true);
                if (healthOk)
                {
                    isHealthy = true;
                    lastStatusMessage = "Backend already available.";
                    isStarting = false;
                    yield break;
                }
            }

            if (!OwnsRunningProcess)
            {
                if (!TryStartBackendProcess(out var startMessage))
                {
                    isHealthy = false;
                    lastStatusMessage = startMessage;
                    isStarting = false;
                    yield break;
                }
            }

            var deadline = Time.realtimeSinceStartup + Mathf.Max(1f, startupTimeoutSeconds);
            while (Time.realtimeSinceStartup < deadline)
            {
                if (backendProcess != null && !IsProcessAlive(backendProcess))
                {
                    isHealthy = false;
                    lastStatusMessage = "Backend process exited before health check succeeded.";
                    backendProcess = null;
                    isStarting = false;
                    yield break;
                }

                if (!IsBackendPortOpen())
                {
                    yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, healthPollIntervalSeconds));
                    continue;
                }

                var ready = false;
                var message = string.Empty;
                yield return CheckHealth((ok, response) =>
                {
                    ready = ok;
                    message = response;
                }, false);

                if (ready)
                {
                    isHealthy = true;
                    lastStatusMessage = "Backend ready: " + message;
                    isStarting = false;
                    yield break;
                }

                yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, healthPollIntervalSeconds));
            }

            isHealthy = false;
            lastStatusMessage = "Backend startup timed out.";
            isStarting = false;
        }

        public IEnumerator RestartBackend()
        {
            StopOwnedBackendProcess();
            yield return EnsureBackendReady();
        }

        public void StopOwnedBackendProcess()
        {
            isHealthy = false;

            if (backendProcess == null)
            {
                lastStatusMessage = "No owned backend process to stop.";
                return;
            }

            try
            {
                if (!backendProcess.HasExited)
                    backendProcess.Kill();
            }
            catch (Exception ex)
            {
                lastStatusMessage = "Failed to stop backend: " + ex.Message;
                Debug.LogWarning(lastStatusMessage);
            }
            finally
            {
                backendProcess.Dispose();
                backendProcess = null;
            }

            lastStatusMessage = "Owned backend process stopped.";
        }

        private bool TryStartBackendProcess(out string message)
        {
            message = string.Empty;
            resolvedBackendRoot = ResolveBackendRoot();
            if (string.IsNullOrWhiteSpace(resolvedBackendRoot))
            {
                message = "Could not resolve OperatorsDraft/backend folder. Configure backendRootOverride or bundle EvoFlowBackend.";
                return false;
            }

            try
            {
                var startInfo = BuildStartInfo(resolvedBackendRoot);
                backendProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                backendProcess.OutputDataReceived += OnBackendOutput;
                backendProcess.ErrorDataReceived += OnBackendError;
                backendProcess.Exited += OnBackendExited;

                if (!backendProcess.Start())
                {
                    backendProcess = null;
                    message = "Failed to start backend process.";
                    return false;
                }

                backendProcess.BeginOutputReadLine();
                backendProcess.BeginErrorReadLine();
                message = "Backend process started.";
                lastStatusMessage = message;

                if (verboseLogging)
                    Debug.Log("Desktop backend launch root: " + resolvedBackendRoot);

                return true;
            }
            catch (Exception ex)
            {
                backendProcess = null;
                message = "Failed to start backend process: " + ex.Message;
                Debug.LogError(message);
                return false;
            }
        }

        private ProcessStartInfo BuildStartInfo(string backendRoot)
        {
            var isWindows = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
            var launcherPath = Path.Combine(backendRoot, isWindows ? windowsLauncherRelativePath : unixLauncherRelativePath);
            var packagedBackendExe = Path.Combine(backendRoot, "EvoFlowBackend.exe");
            var serverPath = Path.Combine(backendRoot, serverScriptRelativePath);
            var arguments = string.Format("--host {0} --port {1}", backendHost, backendPort);

            string fileName;
            string argumentString;

            if (isWindows && File.Exists(packagedBackendExe))
            {
                fileName = packagedBackendExe;
                argumentString = arguments;
            }
            else if (preferLauncherScript && File.Exists(launcherPath))
            {
                if (isWindows)
                {
                    fileName = "cmd.exe";
                    argumentString = string.Format(@"/c ""{0}"" {1}", launcherPath, arguments);
                }
                else
                {
                    fileName = "/bin/bash";
                    argumentString = string.Format(@"""{0}"" {1}", launcherPath, arguments);
                }
            }
            else
            {
                fileName = ResolvePythonExecutable();
                argumentString = string.Format(@"""{0}"" {1}", serverPath, arguments);
            }

            return new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = argumentString,
                WorkingDirectory = backendRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        private string ResolvePythonExecutable()
        {
            if (!string.IsNullOrWhiteSpace(pythonExecutableOverride))
                return pythonExecutableOverride;

            var isWindows = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
            return isWindows ? "python" : "python3";
        }

        private string ResolveBackendRoot()
        {
            var candidates = new List<string>();
            AddCandidate(candidates, backendRootOverride);
            AddCandidate(candidates, Path.Combine(Application.streamingAssetsPath, bundledBackendRelativePath));

            var projectOrBuildRoot = Directory.GetParent(Application.dataPath)?.FullName;
            AddCandidate(candidates, Path.Combine(projectOrBuildRoot ?? string.Empty, "OperatorsDraft"));

            var workspaceRoot = projectOrBuildRoot != null ? Directory.GetParent(projectOrBuildRoot)?.FullName : null;
            AddCandidate(candidates, Path.Combine(workspaceRoot ?? string.Empty, "OperatorsDraft"));

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var normalized = Path.GetFullPath(candidate);
                if (Directory.Exists(normalized) && File.Exists(Path.Combine(normalized, serverScriptRelativePath)))
                    return normalized;
            }

            return string.Empty;
        }

        private void SyncEndpointFromBaseUrl()
        {
            if (string.IsNullOrWhiteSpace(backendBaseUrl))
            {
                backendBaseUrl = string.Format("http://{0}:{1}", backendHost, backendPort);
                return;
            }

            if (!Uri.TryCreate(backendBaseUrl, UriKind.Absolute, out var uri))
                return;

            backendHost = uri.Host;
            backendPort = uri.Port;
        }

        private static void AddCandidate(List<string> candidates, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            candidates.Add(value);
        }

        private IEnumerator CheckHealth(Action<bool, string> onComplete, bool throttle)
        {
            if (throttle && Time.realtimeSinceStartup - lastHealthCheckTime < 0.15f)
            {
                onComplete?.Invoke(isHealthy, lastStatusMessage);
                yield break;
            }

            lastHealthCheckTime = Time.realtimeSinceStartup;
            using (var request = UnityWebRequest.Get(NormalizeBaseUrl(backendBaseUrl) + "/api/health"))
            {
                request.timeout = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1f, healthPollIntervalSeconds) * 2f));
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    isHealthy = false;
                    onComplete?.Invoke(false, request.error);
                    yield break;
                }

                var responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                isHealthy = true;
                onComplete?.Invoke(true, responseText);
            }
        }

        private bool IsBackendPortOpen()
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(backendHost, backendPort, null, null);
                    var connected = result.AsyncWaitHandle.WaitOne(150);
                    if (!connected)
                        return false;

                    client.EndConnect(result);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void OnBackendOutput(object sender, DataReceivedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Data))
                return;

            if (verboseLogging)
                Debug.Log("[EvoFlowBackend] " + args.Data);
        }

        private void OnBackendError(object sender, DataReceivedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Data))
                return;

            Debug.LogWarning("[EvoFlowBackend:stderr] " + args.Data);
        }

        private void OnBackendExited(object sender, EventArgs args)
        {
            isHealthy = false;
            lastStatusMessage = "Owned backend process exited.";
        }

        private static bool IsProcessAlive(Process process)
        {
            try
            {
                return process != null && !process.HasExited;
            }
            catch
            {
                return false;
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

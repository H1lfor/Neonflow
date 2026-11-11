using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace Neonflow
{
    public class Recognition : MonoBehaviour
    {
        public Texture2D PreviewTexture { get; private set; }
        public WebCamTexture CameraTexture { get; private set; }
        public TrackingFrame Latest { get; private set; }

        public string Status = "status.loading", VoiceStatus = "status.loading";
        public bool Ready;
        public AudioDevice[] Microphones = Array.Empty<AudioDevice>();
        public int Epoch;
        public float Neutral = .55f;
        public Func<double> SongTime;
        public Action<TrackingFrame> OnFrame;
        public Action<string> OnVoice;
        Process worker;
        readonly ConcurrentQueue<string> output = new ConcurrentQueue<string>();
        Texture2D capture;
        bool busy;
        float sent, lastFrame;
        double lastTime = double.MinValue;
        Settings settings;
        int generation;
        public void Launch(Settings s)
        {
            settings = s;
            Status = "status.loading";
            VoiceStatus = s.voice ? "status.loading" : "";
            StartCoroutine(Open());
        }

        IEnumerator Open()
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Status = "error.camera_permission";
            }
            else if (WebCamTexture.devices.Length == 0)
            {
                Status = "error.camera_missing";
            }
            else
            {
                string name = Array.Exists(WebCamTexture.devices, d => d.name == settings.camera) ? settings.camera : WebCamTexture.devices[0].name;
                CameraTexture = new WebCamTexture(name, 640, 480, 20);
                CameraTexture.Play();
            }

            if (settings.voice)
                yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            int mic = settings.voice && Application.HasUserAuthorization(UserAuthorization.Microphone) ? settings.microphone : -2;
            if (settings.voice && mic == -2)
                VoiceStatus = "status.voice_unavailable";
            string root = Path.Combine(Application.streamingAssetsPath, "Recognition");
            string pythonRoot = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/Recognition/python")) : Path.Combine(Application.dataPath, "../Recognition/python");
            bool mac = Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
            string architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x86_64";
            string macRuntime = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/Recognition/mac-runtime", architecture, "python/bin/python3.11")) : Path.Combine(root, "mac-runtime", architecture, "python/bin/python3.11");
            string exe = mac ? macRuntime : Path.Combine(pythonRoot, "python.exe");
            bool nativePython = !mac || File.Exists(exe);
            if (mac && !nativePython) exe = Path.Combine(root, "mac/neonflow-recognition");
            try
            {
                int session = generation;
                worker = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = nativePython ? (mac ? "-I -B -u \"" : "-u \"") + Path.Combine(root, "worker.py") + "\" " + mic : mic.ToString(),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                worker.OutputDataReceived += (o, e) =>
                {
                    if (e.Data != null && session == generation)
                        output.Enqueue(e.Data);
                };
                worker.ErrorDataReceived += (o, e) =>
                {
                    if (e.Data != null)
                        System.Diagnostics.Debug.WriteLine(e.Data);
                };
                worker.Start();
                worker.BeginOutputReadLine();
                worker.BeginErrorReadLine();
            }
            catch (Exception e)
            {
                Status = "error.recognition";
                VoiceStatus = settings.voice ? "status.voice_unavailable" : "";
                UnityEngine.Debug.LogError(e.Message);
            }
        }

        public void NewEpoch()
        {
            Epoch++;
            lastTime = double.MinValue;
            Latest = null;
        }

        public void Calibrate()
        {
            if (Latest?.pose?.Length > 12)
            {
                var p = Latest.pose;
                Neutral = ((p[11].y + p[12].y) * .5f - p[0].y) / Mathf.Max(.06f, Vector2.Distance(p[11].XY, p[12].XY));
            }
        }

        void Update()
        {
            while (output.TryDequeue(out var line))
            {
                TrackingFrame f;
                try
                {
                    f = JsonUtility.FromJson<TrackingFrame>(line);
                }
                catch
                {
                    continue;
                }

                if (f.kind == "devices")
                {
                    Microphones = f.devices ?? Array.Empty<AudioDevice>();
                }
                else if (f.kind == "ready")
                {
                    Ready = true;
                    if (CameraTexture)
                        Status = "status.tracking_lost";
                }
                else if (f.kind == "voice_ready")
                    VoiceStatus = "";
                else if (f.kind == "voice_error")
                    VoiceStatus = "status.voice_unavailable";
                else if (f.kind == "voice" && settings.voice)
                    OnVoice?.Invoke(f.command);
                else if (f.kind == "fatal")
                {
                    Ready = false;
                    Status = "error.recognition";
                    UnityEngine.Debug.LogError(f.error);
                }
                else if (f.kind == "frame_error")
                {
                    busy = false;
                    UnityEngine.Debug.LogWarning(f.error);
                }
                else if (f.kind == "frame")
                {
                    busy = false;
                    if (f.epoch != Epoch || f.time <= lastTime)
                        continue;
                    lastTime = f.time;
                    Latest = f;
                    if (!string.IsNullOrEmpty(f.preview))
                    {
                        if (!PreviewTexture)
                            PreviewTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                        PreviewTexture.LoadImage(Convert.FromBase64String(f.preview));
                    }

                    lastFrame = Time.realtimeSinceStartup;
                    Status = f.pose.Length > 0 ? "" : "status.tracking_lost";
                    OnFrame?.Invoke(f);
                }
            }

            if (Ready && CameraTexture && Time.realtimeSinceStartup - lastFrame > 1)
                Status = "status.tracking_lost";
            if (CameraTexture == null || !CameraTexture.isPlaying || CameraTexture.width < 32)
                return;
            if (!Ready || busy || !CameraTexture.didUpdateThisFrame || Time.realtimeSinceStartup - sent < .075f)
                return;
            if (capture == null || capture.width != CameraTexture.width || capture.height != CameraTexture.height)
            {
                if (capture)
                    Destroy(capture);
                capture = new Texture2D(CameraTexture.width, CameraTexture.height, TextureFormat.RGB24, false);
            }

            double time = SongTime?.Invoke() ?? Time.realtimeSinceStartupAsDouble;
            capture.SetPixels32(CameraTexture.GetPixels32());
            capture.Apply();
            var req = new Request
            {
                epoch = Epoch,
                time = time,
                rotation = CameraTexture.videoRotationAngle,
                vertical = CameraTexture.videoVerticallyMirrored,
                image = Convert.ToBase64String(capture.EncodeToJPG(75))
            };
            var data = JsonUtility.ToJson(req);
            busy = true;
            sent = Time.realtimeSinceStartup;
            var destination = worker;
            int requestGeneration = generation;
            Task.Run(() =>
            {
                try
                {
                    destination.StandardInput.WriteLine(data);
                    destination.StandardInput.Flush();
                }
                catch
                {
                    if (requestGeneration == generation)
                        output.Enqueue("{\"kind\":\"fatal\",\"error\":\"Recognition pipe closed\"}");
                }
            });
        }

        public bool CanCalibrate => Tracked && Latest.left?.Length == 21 && Latest.right?.Length == 21 && Latest.pose[11].v > .5f && Latest.pose[12].v > .5f;
        public bool Tracked => Latest?.pose?.Length > 16 && Time.realtimeSinceStartup - lastFrame < .5f;

        public void Restart(Settings s)
        {
            Stop();
            Ready = false;
            busy = false;
            NewEpoch();
            Launch(s);
        }

        void Stop()
        {
            generation++;
            StopAllCoroutines();
            while (output.TryDequeue(out _))
            {
            }

            if (CameraTexture)
            {
                CameraTexture.Stop();
                Destroy(CameraTexture);
                CameraTexture = null;
            }

            try
            {
                if (worker != null && !worker.HasExited)
                {
                    worker.StandardInput.Close();
                    if (!worker.WaitForExit(500))
                        worker.Kill();
                }

                worker?.Dispose();
            }
            catch
            {
            }

            worker = null;
            while (output.TryDequeue(out _))
            {
            }
        }

        void OnDestroy()
        {
            Stop();
            if (capture)
                Destroy(capture);
            if (PreviewTexture)
                Destroy(PreviewTexture);
        }

        [Serializable]
        class Request
        {
            public int epoch, rotation;
            public double time;
            public bool vertical;
            public string image;
        }
    }
}

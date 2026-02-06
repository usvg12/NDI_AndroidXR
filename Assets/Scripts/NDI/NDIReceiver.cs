// NDI SDK for Unity is licensed for non-commercial use only.
// By using this script you agree to the NDI SDK non-commercial license terms.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using UnityEngine;

#if NDI_SDK_ENABLED
using NewTek.NDI;
#endif

namespace NDI
{
    public enum NDIReceiverState
    {
        Idle,
        Connecting,
        Connected,
        Reconnecting,
        Error
    }

    [Serializable]
    public struct NDIFrameMetrics
    {
        public int Width;
        public int Height;
        public float FramesPerSecond;
        public float LatencyMilliseconds;
    }

    public sealed class NDIReceiver : MonoBehaviour
    {
        [SerializeField] private float reconnectDelaySeconds = 2f;
        [SerializeField] private int maxReconnectAttempts = 5;
        [SerializeField] private float noFrameTimeoutSeconds = 2f;
        [SerializeField] private float noFrameGracePeriodSeconds = 1f;

        public event Action<NDIReceiverState> StateChanged;
        public event Action<string> ErrorChanged;
        public event Action<NDIFrameMetrics> MetricsUpdated;
        public event Action<Texture> VideoFrameReady;

        public NDIReceiverState State { get; private set; } = NDIReceiverState.Idle;
        public string ErrorMessage { get; private set; }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public NDIFrameMetrics Metrics { get; private set; }
        public NDISourceInfo SelectedSource { get; private set; }
        public Texture VideoTexture => videoTexture;

        private Coroutine reconnectCoroutine;
        private Texture2D videoTexture;
        private float lastFrameReceivedTime;
        private byte[] frameBuffer;
        private byte[] conversionBuffer;
        private byte[] rowBuffer;
        private Texture2D fallbackTexture;
        private byte[] fallbackBuffer;
        private readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();
        private int mainThreadId;
        private readonly object pendingFrameLock = new object();
        private bool hasPendingFrame;
        private int pendingWidth;
        private int pendingHeight;
        private TextureFormat pendingFormat;
        private byte[] pendingBuffer;

#if NDI_SDK_ENABLED
        private NDIlib.recv_instance_t receiverInstance;
        private NDIlib.recv_create_v3_t receiverConfig;
        private NDIlib.video_frame_v2_t videoFrame;
        private NDIlib.audio_frame_v2_t audioFrame;
        private NDIlib.metadata_frame_t metadataFrame;
#endif

        private void Awake()
        {
            mainThreadId = Environment.CurrentManagedThreadId;
        }

        private void Update()
        {
            while (mainThreadQueue.TryDequeue(out var action))
            {
                action?.Invoke();
            }

            if (hasPendingFrame)
            {
                ApplyPendingFrame();
            }
        }

        private void OnDisable()
        {
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        public void Connect(NDISourceInfo source)
        {
            if (SelectedSource == source && State == NDIReceiverState.Connected)
            {
                return;
            }

            SelectedSource = source;
            Disconnect();
            StartReconnectLoop();
        }

        public void Disconnect()
        {
            StopReconnectLoop();
            CleanupReceiver();
            UpdateState(NDIReceiverState.Idle);
        }

        private void StartReconnectLoop()
        {
            StopReconnectLoop();
            reconnectCoroutine = StartCoroutine(ReconnectLoop());
        }

        private void StopReconnectLoop()
        {
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }
        }

        private IEnumerator ReconnectLoop()
        {
            UpdateState(NDIReceiverState.Connecting);
            ClearError();
            lastFrameReceivedTime = Time.realtimeSinceStartup;

#if !NDI_SDK_ENABLED
            SetError("NDI SDK is not enabled. Define NDI_SDK_ENABLED to activate receiving.");
            UpdateState(NDIReceiverState.Error);
            ShowFallbackTexture("NDI SDK disabled.");
            yield break;
#else
            var attempts = 0;
            while (attempts < maxReconnectAttempts)
            {
                if (TryConnectReceiver())
                {
                    UpdateState(NDIReceiverState.Connected);
                    StartCoroutine(ReceiveLoop());
                    yield break;
                }

                attempts++;
                UpdateState(NDIReceiverState.Reconnecting);
                yield return new WaitForSeconds(reconnectDelaySeconds);
            }

            SetError("Failed to connect to NDI source after multiple attempts.");
            UpdateState(NDIReceiverState.Error);
            ShowFallbackTexture("NDI receiver failed to connect.");
#endif
        }

#if NDI_SDK_ENABLED
        private IEnumerator ReceiveLoop()
        {
            while (State == NDIReceiverState.Connected)
            {
                var result = NDIlib.recv_capture_v2(receiverInstance, ref videoFrame, ref audioFrame, ref metadataFrame, 0);
                if (result == NDIlib.frame_type_e.frame_type_video)
                {
                    var latestFrame = videoFrame;
                    DrainToLatestFrame(ref latestFrame);
                    UpdateMetricsFromFrame(latestFrame);
                    UpdateVideoTextureFromFrame(latestFrame);
                    NDIlib.recv_free_video_v2(receiverInstance, ref latestFrame);
                    lastFrameReceivedTime = Time.realtimeSinceStartup;
                }
                else if (result == NDIlib.frame_type_e.frame_type_audio)
                {
                    NDIlib.recv_free_audio_v2(receiverInstance, ref audioFrame);
                }
                else if (result == NDIlib.frame_type_e.frame_type_metadata)
                {
                    NDIlib.recv_free_metadata(receiverInstance, ref metadataFrame);
                }
                else if (result == NDIlib.frame_type_e.frame_type_none)
                {
                    if (Time.realtimeSinceStartup - lastFrameReceivedTime >= noFrameTimeoutSeconds + noFrameGracePeriodSeconds)
                    {
                        SetError("No NDI frames received.");
                        UpdateState(NDIReceiverState.Reconnecting);
                        StartReconnectLoop();
                        yield break;
                    }
                }

                yield return null;
            }
        }

        private void DrainToLatestFrame(ref NDIlib.video_frame_v2_t latestFrame)
        {
            while (true)
            {
                var result = NDIlib.recv_capture_v2(receiverInstance, ref videoFrame, ref audioFrame, ref metadataFrame, 0);
                if (result == NDIlib.frame_type_e.frame_type_video)
                {
                    NDIlib.recv_free_video_v2(receiverInstance, ref latestFrame);
                    latestFrame = videoFrame;
                    continue;
                }

                if (result == NDIlib.frame_type_e.frame_type_audio)
                {
                    NDIlib.recv_free_audio_v2(receiverInstance, ref audioFrame);
                    continue;
                }

                if (result == NDIlib.frame_type_e.frame_type_metadata)
                {
                    NDIlib.recv_free_metadata(receiverInstance, ref metadataFrame);
                    continue;
                }

                break;
            }
        }
#endif

        private bool TryConnectReceiver()
        {
#if NDI_SDK_ENABLED
            CleanupReceiver();
            ClearError();

            if (!NDIlib.initialize())
            {
                SetError("Failed to initialize NDI SDK.");
                ShowFallbackTexture("NDI SDK initialization failed.");
                return false;
            }

            receiverConfig = new NDIlib.recv_create_v3_t
            {
                source_to_connect_to = new NDIlib.source_t
                {
                    p_ndi_name = SelectedSource.Name,
                    p_url_address = SelectedSource.Address
                },
                color_format = NDIlib.recv_color_format_e.recv_color_format_fastest,
                bandwidth = NDIlib.recv_bandwidth_e.recv_bandwidth_highest,
                allow_video_fields = false
            };

            receiverInstance = NDIlib.recv_create_v3(ref receiverConfig);
            if (receiverInstance == IntPtr.Zero)
            {
                SetError("Failed to create NDI receiver.");
                ShowFallbackTexture("NDI receiver creation failed.");
                return false;
            }

            return true;
#else
            return false;
#endif
        }

        private void CleanupReceiver()
        {
#if NDI_SDK_ENABLED
            if (receiverInstance != IntPtr.Zero)
            {
                NDIlib.recv_destroy(receiverInstance);
                receiverInstance = IntPtr.Zero;
            }
#endif

            if (videoTexture != null)
            {
                Destroy(videoTexture);
                videoTexture = null;
            }

            frameBuffer = null;
            conversionBuffer = null;
            rowBuffer = null;
            pendingBuffer = null;
        }

#if NDI_SDK_ENABLED
        private void UpdateMetricsFromFrame(NDIlib.video_frame_v2_t frame)
        {
            var latencyTicks = NDIlib.util_clock() - frame.timestamp;
            var metrics = new NDIFrameMetrics
            {
                Width = frame.xres,
                Height = frame.yres,
                FramesPerSecond = frame.frame_rate_N > 0 ? frame.frame_rate_N / (float)frame.frame_rate_D : 0f,
                LatencyMilliseconds = Mathf.Max(0f, latencyTicks / 10000f)
            };

            Metrics = metrics;
            MetricsUpdated?.Invoke(metrics);
        }

        private void UpdateVideoTextureFromFrame(NDIlib.video_frame_v2_t frame)
        {
            if (frame.p_data == IntPtr.Zero || frame.xres <= 0 || frame.yres <= 0)
            {
                return;
            }

            if (!TryGetTextureFormat(frame.FourCC, out var textureFormat, out var requiresConversion))
            {
                return;
            }

            if (Environment.CurrentManagedThreadId != mainThreadId)
            {
                if (TryCopyFrameToManagedBuffer(frame, textureFormat, requiresConversion, out var managedBuffer))
                {
                    StorePendingFrame(managedBuffer, frame.xres, frame.yres, textureFormat);
                }
                return;
            }

            if (videoTexture == null
                || videoTexture.width != frame.xres
                || videoTexture.height != frame.yres
                || videoTexture.format != textureFormat)
            {
                if (videoTexture != null)
                {
                    Destroy(videoTexture);
                }

                videoTexture = new Texture2D(frame.xres, frame.yres, textureFormat, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            var bytesPerPixel = 4;
            var rowSize = frame.xres * bytesPerPixel;
            var dataSize = rowSize * frame.yres;

            if (requiresConversion)
            {
                EnsureConversionBuffers(dataSize, frame.line_stride_in_bytes);
                ConvertUYVYToRGBA(frame, conversionBuffer);
                videoTexture.LoadRawTextureData(conversionBuffer);
            }
            else
            {
                if (frame.line_stride_in_bytes == rowSize)
                {
                    videoTexture.LoadRawTextureData(frame.p_data, dataSize);
                }
                else
                {
                    if (frameBuffer == null || frameBuffer.Length != dataSize)
                    {
                        frameBuffer = new byte[dataSize];
                    }

                    for (var row = 0; row < frame.yres; row++)
                    {
                        var offset = row * frame.line_stride_in_bytes;
                        Marshal.Copy(IntPtr.Add(frame.p_data, offset), frameBuffer, row * rowSize, rowSize);
                    }

                    videoTexture.LoadRawTextureData(frameBuffer);
                }
            }
            videoTexture.Apply(false, false);
            VideoFrameReady?.Invoke(videoTexture);
        }

        private static bool TryGetTextureFormat(NDIlib.FourCC_type_e fourCC, out TextureFormat textureFormat, out bool requiresConversion)
        {
            switch (fourCC)
            {
                case NDIlib.FourCC_type_e.FourCC_type_BGRA:
                    textureFormat = TextureFormat.BGRA32;
                    requiresConversion = false;
                    return true;
                case NDIlib.FourCC_type_e.FourCC_type_RGBA:
                    textureFormat = TextureFormat.RGBA32;
                    requiresConversion = false;
                    return true;
                case NDIlib.FourCC_type_e.FourCC_type_UYVY:
                    textureFormat = TextureFormat.RGBA32;
                    requiresConversion = true;
                    return true;
                default:
                    textureFormat = TextureFormat.RGBA32;
                    requiresConversion = false;
                    return false;
            }
        }

        private void EnsureConversionBuffers(int dataSize, int rowStride)
        {
            if (conversionBuffer == null || conversionBuffer.Length != dataSize)
            {
                conversionBuffer = new byte[dataSize];
            }

            if (rowBuffer == null || rowBuffer.Length != rowStride)
            {
                rowBuffer = new byte[rowStride];
            }
        }

        private void ConvertUYVYToRGBA(NDIlib.video_frame_v2_t frame, byte[] destination)
        {
            var width = frame.xres;
            var height = frame.yres;
            var stride = frame.line_stride_in_bytes;

            for (var row = 0; row < height; row++)
            {
                Marshal.Copy(IntPtr.Add(frame.p_data, row * stride), rowBuffer, 0, stride);
                var destRow = row * width * 4;
                var srcIndex = 0;
                for (var col = 0; col < width; col += 2)
                {
                    var u = rowBuffer[srcIndex++] - 128;
                    var y0 = rowBuffer[srcIndex++] - 16;
                    var v = rowBuffer[srcIndex++] - 128;
                    var y1 = rowBuffer[srcIndex++] - 16;

                    WriteRgbFromYuv(y0, u, v, destination, destRow + col * 4);
                    WriteRgbFromYuv(y1, u, v, destination, destRow + (col + 1) * 4);
                }
            }
        }

        private static void WriteRgbFromYuv(int y, int u, int v, byte[] destination, int destIndex)
        {
            var c = y < 0 ? 0 : y;
            var r = (298 * c + 409 * v + 128) >> 8;
            var g = (298 * c - 100 * u - 208 * v + 128) >> 8;
            var b = (298 * c + 516 * u + 128) >> 8;

            destination[destIndex] = ClampToByte(r);
            destination[destIndex + 1] = ClampToByte(g);
            destination[destIndex + 2] = ClampToByte(b);
            destination[destIndex + 3] = 255;
        }

        private static byte ClampToByte(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > 255 ? (byte)255 : (byte)value;
        }

        private bool TryCopyFrameToManagedBuffer(NDIlib.video_frame_v2_t frame, TextureFormat format, bool requiresConversion, out byte[] managedBuffer)
        {
            var bytesPerPixel = 4;
            var rowSize = frame.xres * bytesPerPixel;
            var dataSize = rowSize * frame.yres;

            if (requiresConversion)
            {
                EnsureConversionBuffers(dataSize, frame.line_stride_in_bytes);
                ConvertUYVYToRGBA(frame, conversionBuffer);
                managedBuffer = conversionBuffer;
                return true;
            }

            if (frame.line_stride_in_bytes == rowSize)
            {
                if (frameBuffer == null || frameBuffer.Length != dataSize)
                {
                    frameBuffer = new byte[dataSize];
                }

                Marshal.Copy(frame.p_data, frameBuffer, 0, dataSize);
                managedBuffer = frameBuffer;
                return true;
            }

            if (frameBuffer == null || frameBuffer.Length != dataSize)
            {
                frameBuffer = new byte[dataSize];
            }

            for (var row = 0; row < frame.yres; row++)
            {
                var offset = row * frame.line_stride_in_bytes;
                Marshal.Copy(IntPtr.Add(frame.p_data, offset), frameBuffer, row * rowSize, rowSize);
            }

            managedBuffer = frameBuffer;
            return true;
        }
#endif

        private void UpdateState(NDIReceiverState state)
        {
            if (State == state)
            {
                return;
            }

            var previous = State;
            State = state;
            Debug.Log($"NDI Receiver state changed from {previous} to {state}.");
            StateChanged?.Invoke(state);
        }

        private void SetError(string message)
        {
            if (ErrorMessage == message)
            {
                return;
            }

            ErrorMessage = message;
            Debug.LogWarning($"NDI Receiver error: {message}");
            ErrorChanged?.Invoke(message);
        }

        private void ClearError()
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ErrorMessage = string.Empty;
                ErrorChanged?.Invoke(ErrorMessage);
            }
        }

        private void ShowFallbackTexture(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                reason = "NDI fallback triggered.";
            }

            EnqueueMainThread(() =>
            {
                EnsureFallbackTexture();
                VideoFrameReady?.Invoke(fallbackTexture);
                Debug.LogWarning($"NDI fallback active: {reason}");
            });
        }

        private void EnsureFallbackTexture()
        {
            const int fallbackWidth = 3840;
            const int fallbackHeight = 1080;

            if (fallbackTexture == null)
            {
                fallbackTexture = new Texture2D(fallbackWidth, fallbackHeight, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            var dataSize = fallbackWidth * fallbackHeight * 4;
            if (fallbackBuffer == null || fallbackBuffer.Length != dataSize)
            {
                fallbackBuffer = new byte[dataSize];
                BuildFallbackPattern(fallbackBuffer, fallbackWidth, fallbackHeight);
                fallbackTexture.LoadRawTextureData(fallbackBuffer);
                fallbackTexture.Apply(false, false);
            }
        }

        private void BuildFallbackPattern(byte[] buffer, int width, int height)
        {
            var halfWidth = width / 2;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var offset = (y * width + x) * 4;
                    var isLeftEye = x < halfWidth;
                    var band = (x / 320) % 6;
                    var baseColor = GetTestPatternColor(band, isLeftEye);
                    buffer[offset] = baseColor.r;
                    buffer[offset + 1] = baseColor.g;
                    buffer[offset + 2] = baseColor.b;
                    buffer[offset + 3] = 255;
                }
            }
        }

        private static (byte r, byte g, byte b) GetTestPatternColor(int band, bool leftEye)
        {
            if (leftEye)
            {
                return band switch
                {
                    0 => (255, 0, 0),
                    1 => (0, 255, 0),
                    2 => (0, 0, 255),
                    3 => (255, 255, 0),
                    4 => (0, 255, 255),
                    _ => (255, 0, 255)
                };
            }

            return band switch
            {
                0 => (255, 128, 128),
                1 => (128, 255, 128),
                2 => (128, 128, 255),
                3 => (255, 255, 128),
                4 => (128, 255, 255),
                _ => (255, 128, 255)
            };
        }

        private void EnqueueMainThread(Action action)
        {
            if (Environment.CurrentManagedThreadId == mainThreadId)
            {
                action?.Invoke();
                return;
            }

            mainThreadQueue.Enqueue(action);
        }

        private void ApplyPendingFrame()
        {
            lock (pendingFrameLock)
            {
                if (!hasPendingFrame)
                {
                    return;
                }

                UpdateVideoTextureFromBuffer(pendingBuffer, pendingWidth, pendingHeight, pendingFormat);
                hasPendingFrame = false;
            }
        }

        private void UpdateVideoTextureFromBuffer(byte[] buffer, int width, int height, TextureFormat format)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return;
            }

            if (videoTexture == null
                || videoTexture.width != width
                || videoTexture.height != height
                || videoTexture.format != format)
            {
                if (videoTexture != null)
                {
                    Destroy(videoTexture);
                }

                videoTexture = new Texture2D(width, height, format, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            videoTexture.LoadRawTextureData(buffer);
            videoTexture.Apply(false, false);
            VideoFrameReady?.Invoke(videoTexture);
        }

        private void StorePendingFrame(byte[] buffer, int width, int height, TextureFormat format)
        {
            lock (pendingFrameLock)
            {
                if (pendingBuffer == null || pendingBuffer.Length != buffer.Length)
                {
                    pendingBuffer = new byte[buffer.Length];
                }

                Buffer.BlockCopy(buffer, 0, pendingBuffer, 0, buffer.Length);
                pendingWidth = width;
                pendingHeight = height;
                pendingFormat = format;
                hasPendingFrame = true;
            }
        }
    }
}

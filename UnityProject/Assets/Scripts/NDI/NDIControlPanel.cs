using System.Collections.Generic;
using NDI;
using Rendering;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NDI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class NDIControlPanel : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private NDIDiscovery discovery;
        [SerializeField] private NDIReceiver receiver;
        [SerializeField] private SBSVideoRenderer sbsRenderer;
        [SerializeField] private Transform panelTransform;

        [Header("UI (Auto-Built)")]
        [SerializeField] private bool autoBuildUi = true;
        [SerializeField] private Canvas controlCanvas;
        [SerializeField] private Dropdown sourceDropdown;
        [SerializeField] private InputField manualNameInput;
        [SerializeField] private InputField manualAddressInput;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private Toggle sbsToggle;
        [SerializeField] private Toggle eyeSwapToggle;
        [SerializeField] private Slider scaleSlider;
        [SerializeField] private Text statusText;
        [SerializeField] private Text statsText;
        [SerializeField] private Text errorText;

        private readonly List<NDISourceInfo> sources = new List<NDISourceInfo>();
        private float lastFrameTime;
        private float lastFrameInterval;
        private float lastFps;
        private int lastWidth;
        private int lastHeight;
        private int droppedFrames;

        private void OnEnable()
        {
            if (discovery == null)
            {
                discovery = GetComponent<NDIDiscovery>();
            }

            if (receiver == null)
            {
                receiver = GetComponent<NDIReceiver>();
            }

            if (sbsRenderer == null)
            {
                sbsRenderer = FindObjectOfType<SBSVideoRenderer>();
            }

            if (panelTransform == null && sbsRenderer != null)
            {
                panelTransform = sbsRenderer.transform;
            }

            if (autoBuildUi && controlCanvas == null)
            {
                BuildDefaultUi();
            }

            HookUiEvents();
            HookNdiEvents();
            RefreshUiState();
        }

        private void OnDisable()
        {
            UnhookUiEvents();
            UnhookNdiEvents();
        }

        private void Update()
        {
            UpdateStatsText();
        }

        private void HookNdiEvents()
        {
            if (discovery != null)
            {
                discovery.SourcesUpdated += HandleSourcesUpdated;
            }

            if (receiver != null)
            {
                receiver.StateChanged += HandleReceiverStateChanged;
                receiver.ErrorChanged += HandleReceiverErrorChanged;
                receiver.MetricsUpdated += HandleMetricsUpdated;
                receiver.VideoFrameReady += HandleVideoFrameReady;
            }
        }

        private void UnhookNdiEvents()
        {
            if (discovery != null)
            {
                discovery.SourcesUpdated -= HandleSourcesUpdated;
            }

            if (receiver != null)
            {
                receiver.StateChanged -= HandleReceiverStateChanged;
                receiver.ErrorChanged -= HandleReceiverErrorChanged;
                receiver.MetricsUpdated -= HandleMetricsUpdated;
                receiver.VideoFrameReady -= HandleVideoFrameReady;
            }
        }

        private void HookUiEvents()
        {
            if (connectButton != null)
            {
                connectButton.onClick.AddListener(HandleConnectPressed);
            }

            if (disconnectButton != null)
            {
                disconnectButton.onClick.AddListener(HandleDisconnectPressed);
            }

            if (sbsToggle != null)
            {
                sbsToggle.onValueChanged.AddListener(HandleSbsToggle);
            }

            if (eyeSwapToggle != null)
            {
                eyeSwapToggle.onValueChanged.AddListener(HandleEyeSwapToggle);
            }

            if (scaleSlider != null)
            {
                scaleSlider.onValueChanged.AddListener(HandleScaleChanged);
            }
        }

        private void UnhookUiEvents()
        {
            if (connectButton != null)
            {
                connectButton.onClick.RemoveListener(HandleConnectPressed);
            }

            if (disconnectButton != null)
            {
                disconnectButton.onClick.RemoveListener(HandleDisconnectPressed);
            }

            if (sbsToggle != null)
            {
                sbsToggle.onValueChanged.RemoveListener(HandleSbsToggle);
            }

            if (eyeSwapToggle != null)
            {
                eyeSwapToggle.onValueChanged.RemoveListener(HandleEyeSwapToggle);
            }

            if (scaleSlider != null)
            {
                scaleSlider.onValueChanged.RemoveListener(HandleScaleChanged);
            }
        }

        private void HandleSourcesUpdated(IReadOnlyList<NDISourceInfo> updatedSources)
        {
            sources.Clear();
            if (updatedSources != null)
            {
                sources.AddRange(updatedSources);
            }

            RefreshSourceDropdown();
        }

        private void HandleReceiverStateChanged(NDIReceiverState state)
        {
            RefreshUiState();
        }

        private void HandleReceiverErrorChanged(string error)
        {
            RefreshUiState();
        }

        private void HandleMetricsUpdated(NDIFrameMetrics metrics)
        {
            lastWidth = metrics.Width;
            lastHeight = metrics.Height;
            lastFps = metrics.FramesPerSecond;
        }

        private void HandleVideoFrameReady(Texture texture)
        {
            var now = Time.realtimeSinceStartup;
            if (lastFrameTime > 0f)
            {
                lastFrameInterval = now - lastFrameTime;
                var expectedInterval = GetExpectedFrameInterval();
                if (expectedInterval > 0f && lastFrameInterval > expectedInterval * 1.5f)
                {
                    var missed = Mathf.Max(0, Mathf.FloorToInt(lastFrameInterval / expectedInterval) - 1);
                    droppedFrames += missed;
                }
            }

            lastFrameTime = now;
        }

        private void HandleConnectPressed()
        {
            if (receiver == null)
            {
                return;
            }

            var source = ResolveSelectedSource();
            if (string.IsNullOrWhiteSpace(source.Name) && string.IsNullOrWhiteSpace(source.Address))
            {
                return;
            }

            receiver.Connect(source);
        }

        private void HandleDisconnectPressed()
        {
            receiver?.Disconnect();
        }

        private void HandleSbsToggle(bool enabled)
        {
            sbsRenderer?.SetSbsEnabled(enabled);
        }

        private void HandleEyeSwapToggle(bool enabled)
        {
            sbsRenderer?.SetEyeSwap(enabled);
        }

        private void HandleScaleChanged(float value)
        {
            if (panelTransform == null)
            {
                return;
            }

            panelTransform.localScale = new Vector3(value, value * 0.5625f, value);
        }

        private void RefreshUiState()
        {
            if (statusText != null)
            {
                var stateLabel = receiver != null ? receiver.State.ToString() : "Missing Receiver";
                statusText.text = $"Status: {stateLabel}";
            }

            if (errorText != null)
            {
                var error = receiver != null ? receiver.ErrorMessage : string.Empty;
                if (discovery != null && !string.IsNullOrEmpty(discovery.LastError))
                {
                    error = string.IsNullOrEmpty(error) ? discovery.LastError : $"{error}\n{discovery.LastError}";
                }

                errorText.text = string.IsNullOrEmpty(error) ? "Errors: None" : $"Errors: {error}";
            }

            RefreshSourceDropdown();
        }

        private void RefreshSourceDropdown()
        {
            if (sourceDropdown == null)
            {
                return;
            }

            sourceDropdown.ClearOptions();
            var options = new List<string>();
            if (sources.Count == 0)
            {
                options.Add("No sources found");
            }
            else
            {
                foreach (var source in sources)
                {
                    options.Add(source.ToString());
                }
            }

            sourceDropdown.AddOptions(options);
            sourceDropdown.RefreshShownValue();
        }

        private NDISourceInfo ResolveSelectedSource()
        {
            var manualName = manualNameInput != null ? manualNameInput.text : string.Empty;
            var manualAddress = manualAddressInput != null ? manualAddressInput.text : string.Empty;

            if (!string.IsNullOrWhiteSpace(manualName) || !string.IsNullOrWhiteSpace(manualAddress))
            {
                return new NDISourceInfo(manualName, manualAddress);
            }

            if (sourceDropdown != null && sources.Count > 0 && sourceDropdown.value < sources.Count)
            {
                return sources[sourceDropdown.value];
            }

            return new NDISourceInfo(string.Empty, string.Empty);
        }

        private float GetExpectedFrameInterval()
        {
            if (lastFps > 0f)
            {
                return 1f / lastFps;
            }

            return 1f / 30f;
        }

        private void UpdateStatsText()
        {
            if (statsText == null)
            {
                return;
            }

            var age = lastFrameTime > 0f ? Time.realtimeSinceStartup - lastFrameTime : 0f;
            var fps = lastFps > 0f ? lastFps : (lastFrameInterval > 0f ? 1f / lastFrameInterval : 0f);

            statsText.text =
                $"FPS: {fps:0.0}\n" +
                $"Resolution: {lastWidth}x{lastHeight}\n" +
                $"Last Frame Age: {age:0.000}s\n" +
                $"Dropped Frames: {droppedFrames}";
        }

        private void BuildDefaultUi()
        {
            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var canvasObject = new GameObject("NDI Control Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            controlCanvas = canvasObject.GetComponent<Canvas>();
            controlCanvas.renderMode = RenderMode.WorldSpace;
            controlCanvas.sortingOrder = 1;

            var canvasTransform = controlCanvas.GetComponent<RectTransform>();
            canvasTransform.sizeDelta = new Vector2(800f, 520f);
            canvasTransform.localScale = Vector3.one * 0.0025f;
            canvasTransform.localPosition = new Vector3(0f, 1.3f, 1.6f);
            canvasTransform.localRotation = Quaternion.identity;

            var panel = CreateUiObject("Panel", canvasTransform, new Vector2(800f, 520f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.6f);

            var y = -30f;
            var lineHeight = 40f;

            CreateLabel(panel, font, "NDI Control Panel", new Vector2(20f, y), 26, FontStyle.Bold);
            y -= lineHeight;

            CreateLabel(panel, font, "Source (Auto-Discovery):", new Vector2(20f, y), 20, FontStyle.Normal);
            sourceDropdown = CreateDropdown(panel, font, new Vector2(280f, y - 10f), new Vector2(480f, 30f));
            y -= lineHeight;

            CreateLabel(panel, font, "Manual Name:", new Vector2(20f, y), 20, FontStyle.Normal);
            manualNameInput = CreateInputField(panel, font, new Vector2(280f, y - 10f), new Vector2(480f, 30f));
            y -= lineHeight;

            CreateLabel(panel, font, "Manual Address:", new Vector2(20f, y), 20, FontStyle.Normal);
            manualAddressInput = CreateInputField(panel, font, new Vector2(280f, y - 10f), new Vector2(480f, 30f));
            y -= lineHeight;

            connectButton = CreateButton(panel, font, "Connect", new Vector2(20f, y - 10f), new Vector2(150f, 32f), new Color(0.2f, 0.7f, 0.2f));
            disconnectButton = CreateButton(panel, font, "Disconnect", new Vector2(190f, y - 10f), new Vector2(150f, 32f), new Color(0.7f, 0.2f, 0.2f));
            y -= lineHeight;

            sbsToggle = CreateToggle(panel, font, "SBS Mode", new Vector2(20f, y - 5f));
            eyeSwapToggle = CreateToggle(panel, font, "Swap Eyes", new Vector2(220f, y - 5f));
            y -= lineHeight;

            CreateLabel(panel, font, "Panel Scale:", new Vector2(20f, y), 20, FontStyle.Normal);
            scaleSlider = CreateSlider(panel, new Vector2(280f, y - 10f), new Vector2(480f, 30f), 0.6f, 3f, 1.6f);
            y -= lineHeight + 10f;

            statusText = CreateLabel(panel, font, "Status: Idle", new Vector2(20f, y), 18, FontStyle.Normal);
            y -= lineHeight;
            errorText = CreateLabel(panel, font, "Errors: None", new Vector2(20f, y), 18, FontStyle.Normal);
            y -= lineHeight;
            statsText = CreateLabel(panel, font, "FPS: --\nResolution: --\nLast Frame Age: --\nDropped Frames: 0", new Vector2(20f, y), 18, FontStyle.Normal);

            EnsureEventSystem();
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(null, false);
        }

        private static RectTransform CreateUiObject(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static Text CreateLabel(Transform parent, Font font, string text, Vector2 position, int fontSize, FontStyle style)
        {
            var labelRect = CreateUiObject("Label", parent, new Vector2(760f, 30f));
            labelRect.anchoredPosition = position;
            var label = labelRect.gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = Color.white;
            label.alignment = TextAnchor.UpperLeft;
            return label;
        }

        private static Dropdown CreateDropdown(Transform parent, Font font, Vector2 position, Vector2 size)
        {
            var dropdownRect = CreateUiObject("Dropdown", parent, size);
            dropdownRect.anchoredPosition = position;
            var dropdown = dropdownRect.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = dropdownRect.gameObject.AddComponent<Image>();

            var label = CreateLabel(dropdownRect, font, "None", new Vector2(10f, -6f), 18, FontStyle.Normal);
            label.alignment = TextAnchor.MiddleLeft;
            dropdown.captionText = label;

            var template = CreateUiObject("Template", dropdownRect, new Vector2(size.x, 150f));
            template.anchoredPosition = new Vector2(0f, -size.y);
            var templateImage = template.gameObject.AddComponent<Image>();
            templateImage.color = new Color(0f, 0f, 0f, 0.9f);
            var scrollRect = template.gameObject.AddComponent<ScrollRect>();
            var viewport = CreateUiObject("Viewport", template, size);
            var viewportMask = viewport.gameObject.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            viewport.gameObject.AddComponent<Image>();

            var content = CreateUiObject("Content", viewport, new Vector2(size.x, 150f));
            content.gameObject.AddComponent<VerticalLayoutGroup>();
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = content;
            scrollRect.viewport = viewport;
            scrollRect.horizontal = false;

            var item = CreateUiObject("Item", content, new Vector2(size.x, 30f));
            var itemToggle = item.gameObject.AddComponent<Toggle>();
            itemToggle.targetGraphic = item.gameObject.AddComponent<Image>();
            var itemLabel = CreateLabel(item, font, "Option", new Vector2(10f, -6f), 18, FontStyle.Normal);
            itemLabel.alignment = TextAnchor.MiddleLeft;
            itemToggle.graphic = itemLabel;

            dropdown.template = template;
            dropdown.itemText = itemLabel;
            return dropdown;
        }

        private static InputField CreateInputField(Transform parent, Font font, Vector2 position, Vector2 size)
        {
            var inputRect = CreateUiObject("InputField", parent, size);
            inputRect.anchoredPosition = position;
            var image = inputRect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.1f);
            var input = inputRect.gameObject.AddComponent<InputField>();
            var text = CreateLabel(inputRect, font, string.Empty, new Vector2(10f, -6f), 18, FontStyle.Normal);
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            input.textComponent = text;
            return input;
        }

        private static Button CreateButton(Transform parent, Font font, string text, Vector2 position, Vector2 size, Color color)
        {
            var buttonRect = CreateUiObject(text, parent, size);
            buttonRect.anchoredPosition = position;
            var image = buttonRect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = buttonRect.gameObject.AddComponent<Button>();
            var label = CreateLabel(buttonRect, font, text, new Vector2(10f, -6f), 18, FontStyle.Bold);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static Toggle CreateToggle(Transform parent, Font font, string text, Vector2 position)
        {
            var toggleRect = CreateUiObject(text, parent, new Vector2(180f, 30f));
            toggleRect.anchoredPosition = position;
            var toggle = toggleRect.gameObject.AddComponent<Toggle>();
            var background = CreateUiObject("Background", toggleRect, new Vector2(20f, 20f));
            background.anchoredPosition = new Vector2(10f, -5f);
            var bgImage = background.gameObject.AddComponent<Image>();
            bgImage.color = new Color(1f, 1f, 1f, 0.2f);
            var checkmark = CreateUiObject("Checkmark", background, new Vector2(16f, 16f));
            checkmark.anchoredPosition = new Vector2(2f, -2f);
            var checkImage = checkmark.gameObject.AddComponent<Image>();
            checkImage.color = new Color(0.2f, 0.8f, 0.2f);
            toggle.graphic = checkImage;
            toggle.targetGraphic = bgImage;

            var label = CreateLabel(toggleRect, font, text, new Vector2(40f, -6f), 18, FontStyle.Normal);
            label.alignment = TextAnchor.MiddleLeft;
            return toggle;
        }

        private static Slider CreateSlider(Transform parent, Vector2 position, Vector2 size, float min, float max, float value)
        {
            var sliderRect = CreateUiObject("Slider", parent, size);
            sliderRect.anchoredPosition = position;
            var slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            var background = CreateUiObject("Background", sliderRect, size);
            var bgImage = background.gameObject.AddComponent<Image>();
            bgImage.color = new Color(1f, 1f, 1f, 0.2f);
            slider.targetGraphic = bgImage;

            var fillArea = CreateUiObject("Fill Area", sliderRect, size);
            var fill = CreateUiObject("Fill", fillArea, size);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.6f, 1f);
            slider.fillRect = fill.GetComponent<RectTransform>();

            var handle = CreateUiObject("Handle", sliderRect, new Vector2(20f, size.y));
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Color.white;
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }
    }
}

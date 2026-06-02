using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Minesweeper
{
    public sealed class UIController : MonoBehaviour
    {
        private const float RoomInfoRefreshInterval = 0.25f;

        private GameController _gameController;
        private Canvas _canvas;
        private RectTransform _hudRoot;
        private TextMeshProUGUI _statusLabel;
        private TextMeshProUGUI _roomCodeLabel;
        private TextMeshProUGUI _roomTypeLabel;
        private TextMeshProUGUI _roomAccessLabel;
        private TextMeshProUGUI _roomPingLabel;
        private TextMeshProUGUI _timerLabel;
        private TimerPulseAnimator _timerPulseAnimator;
        private TextMeshProUGUI _flagsLabel;
        private TextMeshProUGUI _bombsLabel;
        private TextMeshProUGUI _stateLabel;
        private Button _restartButton;
        private Toggle _debugRevealBombsToggle;
        private Toggle _debugHighlightMovedBombToggle;
        private float _nextRoomInfoRefreshTime;
        private int _lastElapsedSeconds = -1;
        private int _lastFlaggedCells = -1;
        private int _lastBombsTotal = -1;
        private int _lastBombsOnField = -1;
        private bool _lastCanToggleFlags;
        private bool _lastGameFinished;
        private bool _lastCountdownWarningActive;
        private string _lastRoomCode = string.Empty;
        private string _lastRoomType = string.Empty;
        private string _lastRoomAccess = string.Empty;
        private string _lastPing = string.Empty;
        private bool _lastShowPing;

        private static readonly Color TimerDefaultColor = Color.white;
        private static readonly Color TimerWarningColor = new Color(0.94f, 0.18f, 0.18f);

        public void Init(GameController gameController)
        {
            if (_gameController != null)
            {
                _gameController.HudStateChanged -= RefreshGameplayHud;
            }

            _gameController = gameController;
            EnsureCanvas();
            _gameController.HudStateChanged += RefreshGameplayHud;
            RefreshGameplayHud();
            RefreshRoomInfoHud(true);
            HideState();
        }

        private void Update()
        {
            if (_gameController == null || Time.unscaledTime < _nextRoomInfoRefreshTime)
            {
                return;
            }

            _nextRoomInfoRefreshTime = Time.unscaledTime + RoomInfoRefreshInterval;
            RefreshRoomInfoHud(false);
        }

        private void OnDestroy()
        {
            if (_gameController != null)
            {
                _gameController.HudStateChanged -= RefreshGameplayHud;
            }
        }

        public void ShowWin()
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = "You Win";
                _statusLabel.color = new Color(0.2f, 0.85f, 0.35f);
            }
        }

        public void ShowLose()
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = "You Lose";
                _statusLabel.color = new Color(0.88f, 0.25f, 0.2f);
            }
        }

        public void HideState()
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = string.Empty;
            }
        }

        public void ShowSyncing()
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = "Syncing board...";
                _statusLabel.color = Color.white;
            }
        }

        private void EnsureCanvas()
        {
            if (_canvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("MinesweeperUI");
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            _hudRoot = CreateHudRoot(canvasObject.transform);
            CreateRoomInfoPanel(_hudRoot);
            CreateTimerPanel(_hudRoot);
            CreateIndicatorsPanel(_hudRoot);
            CreateDebugPanel(_hudRoot);
            _statusLabel = CreateStatusLabel(canvasObject.transform);
            _restartButton = CreateRestartButton(canvasObject.transform);
        }

        private RectTransform CreateHudRoot(Transform parent)
        {
            GameObject rootObject = new GameObject("HUDRoot");
            rootObject.transform.SetParent(parent, false);

            RectTransform rectTransform = rootObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return rectTransform;
        }

        private void CreateRoomInfoPanel(Transform parent)
        {
            RectTransform panel = CreateHudPanel(parent, "RoomInfoPanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(330f, 126f));
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 10);
            layout.spacing = 3f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddHudText(panel, "RoomInfoTitle", "Room", 19f, FontStyles.Bold, TextAlignmentOptions.Left);
            _roomCodeLabel = AddHudText(panel, "RoomCode", "Code: --", 17f, FontStyles.Normal, TextAlignmentOptions.Left);
            _roomTypeLabel = AddHudText(panel, "RoomType", "Type: --", 17f, FontStyles.Normal, TextAlignmentOptions.Left);
            _roomAccessLabel = AddHudText(panel, "RoomAccess", "Access: --", 17f, FontStyles.Normal, TextAlignmentOptions.Left);
            _roomPingLabel = AddHudText(panel, "RoomPing", "Ping: --", 17f, FontStyles.Normal, TextAlignmentOptions.Left);
        }

        private void CreateTimerPanel(Transform parent)
        {
            RectTransform panel = CreateHudPanel(parent, "TimerPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(176f, 56f));
            _timerLabel = AddHudText(panel, "TimerValue", "00:00", 32f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchToParent(_timerLabel.rectTransform, Vector2.zero);
            _timerPulseAnimator = _timerLabel.gameObject.AddComponent<TimerPulseAnimator>();
        }

        private void CreateIndicatorsPanel(Transform parent)
        {
            RectTransform panel = CreateHudPanel(parent, "IndicatorsPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(280f, 126f));
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 10);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _flagsLabel = AddHudText(panel, "Flags", "Flags: 0/0", 22f, FontStyles.Bold, TextAlignmentOptions.Right);
            _bombsLabel = AddHudText(panel, "Bombs", "Bombs: 0", 22f, FontStyles.Bold, TextAlignmentOptions.Right);
            _stateLabel = AddHudText(panel, "State", "Ready", 17f, FontStyles.Normal, TextAlignmentOptions.Right);
        }

        private void CreateDebugPanel(Transform parent)
        {
            RectTransform panel = CreateHudPanel(parent, "DebugPanel", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(300f, 184f));
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 10);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddHudText(panel, "DebugTitle", "Test Block", 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            CreateDebugWinButton(panel);
            _debugRevealBombsToggle = CreateDebugRevealToggle(panel);
            _debugHighlightMovedBombToggle = CreateDebugHighlightMovedBombToggle(panel);
        }

        private RectTransform CreateHudPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            GameObject panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);

            RectTransform rectTransform = panelObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = new Vector2(anchorMin.x, anchorMin.y);
            if (anchorMin.x >= 1f)
            {
                rectTransform.pivot = new Vector2(1f, anchorMin.y);
            }
            else if (Mathf.Approximately(anchorMin.x, 0.5f))
            {
                rectTransform.pivot = new Vector2(0.5f, anchorMin.y);
            }

            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;

            Image image = panelObject.AddComponent<Image>();
            image.color = new Color(0.04f, 0.05f, 0.06f, 0.72f);
            image.raycastTarget = false;
            return rectTransform;
        }

        private TextMeshProUGUI AddHudText(Transform parent, string name, string text, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform = textObject.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0f, Mathf.Ceil(fontSize * 1.35f));

            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static void StretchToParent(RectTransform rectTransform, Vector2 padding)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = padding;
            rectTransform.offsetMax = -padding;
        }

        private TextMeshProUGUI CreateStatusLabel(Transform parent)
        {
            GameObject labelObject = new GameObject("StatusLabel");
            labelObject.transform.SetParent(parent, false);

            RectTransform rectTransform = labelObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(0f, -82f);
            rectTransform.sizeDelta = new Vector2(420f, 60f);

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 32f;
            label.text = string.Empty;
            return label;
        }

        private Button CreateRestartButton(Transform parent)
        {
            GameObject buttonObject = new GameObject("RestartButton");
            buttonObject.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-18f, -156f);
            rectTransform.sizeDelta = new Vector2(170f, 46f);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.16f, 0.19f, 0.92f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OnRestartClicked);

            GameObject textObject = new GameObject("Label");
            textObject.transform.SetParent(buttonObject.transform, false);

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 24f;
            label.text = "Restart";
            label.color = Color.white;

            return button;
        }

        private void CreateDebugWinButton(Transform parent)
        {
            GameObject buttonObject = new GameObject("DebugWinButton");
            buttonObject.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(0f, 36f);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.36f, 0.22f, 0.95f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OnDebugWinClicked);

            TextMeshProUGUI label = AddHudText(buttonObject.transform, "Label", "Test Win", 18f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchToParent(label.rectTransform, new Vector2(4f, 2f));
        }

        private Toggle CreateDebugRevealToggle(Transform parent)
        {
            GameObject toggleObject = new GameObject("DebugRevealBombsToggle");
            toggleObject.transform.SetParent(parent, false);
            RectTransform toggleRect = toggleObject.AddComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(0f, 36f);

            Image rowBackground = toggleObject.AddComponent<Image>();
            rowBackground.color = new Color(0f, 0f, 0f, 0f);

            Toggle toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = rowBackground;
            toggle.onValueChanged.AddListener(OnDebugRevealBombsChanged);

            HorizontalLayoutGroup layout = toggleObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            GameObject boxObject = new GameObject("Checkbox");
            boxObject.transform.SetParent(toggleObject.transform, false);
            RectTransform boxRect = boxObject.AddComponent<RectTransform>();
            boxRect.sizeDelta = new Vector2(30f, 30f);

            Image background = boxObject.AddComponent<Image>();
            background.color = new Color(0.2f, 0.22f, 0.27f, 1f);

            GameObject checkObject = new GameObject("Checkmark");
            checkObject.transform.SetParent(boxObject.transform, false);
            RectTransform checkRect = checkObject.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;

            Image checkmark = checkObject.AddComponent<Image>();
            checkmark.color = new Color(0.92f, 0.18f, 0.18f, 1f);

            toggle.graphic = checkmark;

            TextMeshProUGUI label = AddHudText(toggleObject.transform, "Label", "Reveal bombs", 17f, FontStyles.Normal, TextAlignmentOptions.Left);
            label.rectTransform.sizeDelta = new Vector2(190f, 30f);
            label.raycastTarget = true;
            return toggle;
        }

        private Toggle CreateDebugHighlightMovedBombToggle(Transform parent)
        {
            GameObject toggleObject = new GameObject("DebugHighlightMovedBombToggle");
            toggleObject.transform.SetParent(parent, false);
            RectTransform toggleRect = toggleObject.AddComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(0f, 36f);

            Image rowBackground = toggleObject.AddComponent<Image>();
            rowBackground.color = new Color(0f, 0f, 0f, 0f);

            Toggle toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = rowBackground;
            toggle.onValueChanged.AddListener(OnDebugHighlightMovedBombChanged);

            HorizontalLayoutGroup layout = toggleObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            GameObject boxObject = new GameObject("Checkbox");
            boxObject.transform.SetParent(toggleObject.transform, false);
            RectTransform boxRect = boxObject.AddComponent<RectTransform>();
            boxRect.sizeDelta = new Vector2(30f, 30f);

            Image background = boxObject.AddComponent<Image>();
            background.color = new Color(0.2f, 0.22f, 0.27f, 1f);

            GameObject checkObject = new GameObject("Checkmark");
            checkObject.transform.SetParent(boxObject.transform, false);
            RectTransform checkRect = checkObject.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;

            Image checkmark = checkObject.AddComponent<Image>();
            checkmark.color = new Color(1f, 0.84f, 0.05f, 1f);

            toggle.graphic = checkmark;

            TextMeshProUGUI label = AddHudText(toggleObject.transform, "Label", "Highlight moved bomb", 17f, FontStyles.Normal, TextAlignmentOptions.Left);
            label.rectTransform.sizeDelta = new Vector2(230f, 30f);
            label.raycastTarget = true;
            return toggle;
        }

        private void RefreshGameplayHud()
        {
            if (_gameController == null)
            {
                return;
            }

            int elapsedSeconds = _gameController.ElapsedSeconds;
            if (elapsedSeconds != _lastElapsedSeconds)
            {
                _lastElapsedSeconds = elapsedSeconds;
                SetText(_timerLabel, FormatTime(elapsedSeconds));
            }

            int flaggedCells = _gameController.FlaggedCells;
            int bombsTotal = _gameController.BombsTotal;
            int bombsOnField = _gameController.BombsOnField;
            if (flaggedCells != _lastFlaggedCells || bombsTotal != _lastBombsTotal || bombsOnField != _lastBombsOnField)
            {
                _lastFlaggedCells = flaggedCells;
                _lastBombsTotal = bombsTotal;
                _lastBombsOnField = bombsOnField;
                SetText(_flagsLabel, $"Flags: {flaggedCells}/{bombsTotal}");
                SetText(_bombsLabel, $"Bombs: {bombsOnField}");
            }

            bool canToggleFlags = _gameController.CanToggleFlags;
            bool isGameFinished = _gameController.IsGameFinished;
            if (canToggleFlags != _lastCanToggleFlags || isGameFinished != _lastGameFinished)
            {
                _lastCanToggleFlags = canToggleFlags;
                _lastGameFinished = isGameFinished;
                SetText(_stateLabel, GetStateText());
            }

            bool countdownWarningActive = _gameController.IsCountdownWarningActive;
            if (countdownWarningActive != _lastCountdownWarningActive)
            {
                _lastCountdownWarningActive = countdownWarningActive;
                RefreshTimerWarningState(countdownWarningActive);
            }

            if (_debugRevealBombsToggle != null && _debugRevealBombsToggle.isOn != _gameController.DebugRevealBombs)
            {
                _debugRevealBombsToggle.SetIsOnWithoutNotify(_gameController.DebugRevealBombs);
            }

            if (_debugHighlightMovedBombToggle != null && _debugHighlightMovedBombToggle.isOn != _gameController.DebugHighlightMovedBomb)
            {
                _debugHighlightMovedBombToggle.SetIsOnWithoutNotify(_gameController.DebugHighlightMovedBomb);
            }
        }

        private void RefreshRoomInfoHud(bool force)
        {
            CoopPrototypeController controller = CoopPrototypeController.Instance;
            string roomCode = controller != null && !string.IsNullOrWhiteSpace(controller.RoomCode) ? controller.RoomCode : "--";
            string roomType = controller != null ? (controller.IsLocalScenario ? "Local" : "Network") : "--";
            string roomAccess = controller != null ? (controller.IsPrivateRoom ? "Private" : "Public") : "--";
            bool showPing = controller != null && controller.ShowPing;
            string ping = controller != null ? controller.PingDisplay : "--";

            if (force || roomCode != _lastRoomCode)
            {
                _lastRoomCode = roomCode;
                SetText(_roomCodeLabel, "Code: " + roomCode);
            }

            if (force || roomType != _lastRoomType)
            {
                _lastRoomType = roomType;
                SetText(_roomTypeLabel, "Type: " + roomType);
            }

            if (force || roomAccess != _lastRoomAccess)
            {
                _lastRoomAccess = roomAccess;
                SetText(_roomAccessLabel, "Access: " + roomAccess);
            }

            if (_roomPingLabel != null && (force || showPing != _lastShowPing))
            {
                _lastShowPing = showPing;
                _roomPingLabel.gameObject.SetActive(showPing);
            }

            if (showPing && (force || ping != _lastPing))
            {
                _lastPing = ping;
                SetText(_roomPingLabel, "Ping: " + ping);
            }
        }

        private string GetStateText()
        {
            if (_gameController == null)
            {
                return "Ready";
            }

            if (_gameController.IsGameFinished)
            {
                return "Finished";
            }

            return _gameController.CanToggleFlags ? "Playing - 2 min limit" : "Flags locked";
        }

        private static string FormatTime(int totalSeconds)
        {
            int minutes = Mathf.Clamp(totalSeconds / 60, 0, 99);
            int seconds = Mathf.Clamp(totalSeconds % 60, 0, 59);
            return $"{minutes:00}:{seconds:00}";
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null && label.text != value)
            {
                label.text = value;
            }
        }

        private void RefreshTimerWarningState(bool isWarningActive)
        {
            if (_timerLabel != null)
            {
                _timerLabel.color = isWarningActive ? TimerWarningColor : TimerDefaultColor;
            }

            _timerPulseAnimator?.SetPulsing(isWarningActive);
        }

        private void OnRestartClicked()
        {
            _gameController?.RestartGame();
        }

        private void OnDebugWinClicked()
        {
            _gameController?.DebugWinGame();
        }

        private void OnDebugRevealBombsChanged(bool revealBombs)
        {
            _gameController?.SetDebugRevealBombs(revealBombs);
        }

        private void OnDebugHighlightMovedBombChanged(bool highlightMovedBomb)
        {
            _gameController?.SetDebugHighlightMovedBomb(highlightMovedBomb);
        }
    }
}

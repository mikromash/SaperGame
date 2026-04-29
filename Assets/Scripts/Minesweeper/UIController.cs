using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Minesweeper
{
    public sealed class UIController : MonoBehaviour
    {
        private GameController _gameController;
        private Canvas _canvas;
        private TextMeshProUGUI _statusLabel;
        private Button _restartButton;

        public void Init(GameController gameController)
        {
            _gameController = gameController;
            EnsureCanvas();
            HideState();
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
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            _statusLabel = CreateStatusLabel(canvasObject.transform);
            _restartButton = CreateRestartButton(canvasObject.transform);
        }

        private TextMeshProUGUI CreateStatusLabel(Transform parent)
        {
            GameObject labelObject = new GameObject("StatusLabel");
            labelObject.transform.SetParent(parent, false);

            RectTransform rectTransform = labelObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(0f, -24f);
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
            rectTransform.anchoredPosition = new Vector2(-24f, -24f);
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

        private void OnRestartClicked()
        {
            _gameController?.RestartGame();
        }
    }
}

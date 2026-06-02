using UnityEngine;

namespace Minesweeper
{
    // Прибрали RequireComponent(typeof(Renderer)), оскільки рендеритимуться дочірні об'єкти
    public sealed class CellView : MonoBehaviour
    {
        [Header("Візуальні моделі (Призначити в Інспекторі)")]
        [SerializeField] private GameObject _closedVisual;
        [SerializeField] private GameObject _openedEmptyVisual;
        [SerializeField] private GameObject _bombVisual;
        [SerializeField] private GameObject _flagVisual;
        [SerializeField] private GameObject[] _numberVisuals = new GameObject[8]; // Цифри 1-8

        private Cell _cell;
        private bool _revealBombs;
        private bool _highlightMovedBomb;
        private GameObject _movedBombHighlight;
        private static Material _movedBombHighlightMaterial;

        public Cell Cell => _cell;

        public void Init(Cell cell)
        {
            _cell = cell;
            UpdateView();
        }

        public void SetRevealBombs(bool revealBombs)
        {
            _revealBombs = revealBombs;
        }

        public void SetMovedBombHighlight(bool highlight)
        {
            _highlightMovedBomb = highlight;
            UpdateMovedBombHighlight();
        }

        public void UpdateView()
        {
            if (_cell == null)
            {
                return;
            }

            bool showBomb = _cell.hasBomb && (_cell.isOpened || _revealBombs);
            bool showNumber = _cell.isOpened && !_cell.hasBomb && _cell.neighbourBombs > 0;
            bool showFlag = !_cell.isOpened && _cell.isFlagged;
            bool showEmpty = _cell.isOpened && !_cell.hasBomb && _cell.neighbourBombs == 0;

            // 1. Спочатку вимикаємо всі стани
            if (_closedVisual) _closedVisual.SetActive(false);
            if (_openedEmptyVisual) _openedEmptyVisual.SetActive(false);
            if (_bombVisual) _bombVisual.SetActive(false);
            if (_flagVisual) _flagVisual.SetActive(showFlag && !showBomb);

            for (int index = 0; index < _numberVisuals.Length; index++)
            {
                if (_numberVisuals[index]) _numberVisuals[index].SetActive(false);
            }

            // 2. Вмикаємо лише актуальний стан
            if (showBomb)
            {
                if (_bombVisual) _bombVisual.SetActive(true);
            }
            else if (showNumber)
            {
                int numIndex = _cell.neighbourBombs - 1;
                if (numIndex >= 0 && numIndex < _numberVisuals.Length && _numberVisuals[numIndex])
                {
                    _numberVisuals[numIndex].SetActive(true);
                }
            }
            else if (showEmpty)
            {
                if (_openedEmptyVisual) _openedEmptyVisual.SetActive(true);
            }
            else // Клітинка закрита
            {
                if (_closedVisual) _closedVisual.SetActive(true);
            }

            UpdateMovedBombHighlight();
        }

        private void UpdateMovedBombHighlight()
        {
            bool shouldShow = _highlightMovedBomb && _cell != null && _cell.hasBomb;
            if (!shouldShow)
            {
                if (_movedBombHighlight != null)
                {
                    _movedBombHighlight.SetActive(false);
                }

                return;
            }

            EnsureMovedBombHighlight();
            _movedBombHighlight.SetActive(true);
        }

        private void EnsureMovedBombHighlight()
        {
            if (_movedBombHighlight != null)
            {
                return;
            }

            _movedBombHighlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _movedBombHighlight.name = "MovedBombHighlight";
            _movedBombHighlight.transform.SetParent(transform, false);
            _movedBombHighlight.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            _movedBombHighlight.transform.localScale = new Vector3(1.12f, 0.08f, 1.12f);

            Collider highlightCollider = _movedBombHighlight.GetComponent<Collider>();
            if (highlightCollider != null)
            {
                Destroy(highlightCollider);
            }

            Renderer renderer = _movedBombHighlight.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetMovedBombHighlightMaterial();
            }
        }

        private static Material GetMovedBombHighlightMaterial()
        {
            if (_movedBombHighlightMaterial != null)
            {
                return _movedBombHighlightMaterial;
            }

            _movedBombHighlightMaterial = new Material(Shader.Find("Standard"));
            _movedBombHighlightMaterial.name = "MovedBombHighlightMaterial";
            _movedBombHighlightMaterial.color = new Color(1f, 0.84f, 0.05f, 0.72f);
            _movedBombHighlightMaterial.SetFloat("_Mode", 3f);
            _movedBombHighlightMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _movedBombHighlightMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _movedBombHighlightMaterial.SetInt("_ZWrite", 0);
            _movedBombHighlightMaterial.DisableKeyword("_ALPHATEST_ON");
            _movedBombHighlightMaterial.EnableKeyword("_ALPHABLEND_ON");
            _movedBombHighlightMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _movedBombHighlightMaterial.renderQueue = 3000;
            return _movedBombHighlightMaterial;
        }
    }
}

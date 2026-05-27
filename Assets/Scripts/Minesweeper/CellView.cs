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
        }
    }
}
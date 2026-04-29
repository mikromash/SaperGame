using UnityEngine;

namespace Minesweeper
{
    public sealed class GridView : MonoBehaviour
    {
        private Transform _gridRoot;
        private CellView[,] _cellViews;

        public void Build(Cell[,] grid, float cellSize)
        {
            Clear();

            int width = grid.GetLength(0);
            int height = grid.GetLength(1);
            float cellHeight = cellSize * 0.36f;
            _cellViews = new CellView[width, height];

            GameObject rootObject = new GameObject("MinesweeperGrid");
            rootObject.transform.SetParent(transform, false);
            _gridRoot = rootObject.transform;

            Vector3 origin = new Vector3(
                -((width - 1) * cellSize) * 0.5f,
                cellHeight * 0.5f,
                -((height - 1) * cellSize) * 0.5f);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Cell cell = grid[x, y];
                    GameObject cellObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cellObject.name = $"Cell_{x}_{y}";
                    cellObject.transform.SetParent(_gridRoot, false);
                    cellObject.transform.localPosition = origin + new Vector3(x * cellSize, 0f, y * cellSize);
                    cellObject.transform.localScale = new Vector3(cellSize * 0.92f, cellHeight, cellSize * 0.92f);

                    CellView cellView = cellObject.AddComponent<CellView>();
                    cellView.Init(cell);
                    _cellViews[x, y] = cellView;
                }
            }
        }

        public void RefreshAllViews()
        {
            if (_cellViews == null)
            {
                return;
            }

            for (int x = 0; x < _cellViews.GetLength(0); x++)
            {
                for (int y = 0; y < _cellViews.GetLength(1); y++)
                {
                    _cellViews[x, y]?.UpdateView();
                }
            }
        }

        public void SetRevealBombs(bool revealBombs)
        {
            if (_cellViews == null)
            {
                return;
            }

            for (int x = 0; x < _cellViews.GetLength(0); x++)
            {
                for (int y = 0; y < _cellViews.GetLength(1); y++)
                {
                    CellView cellView = _cellViews[x, y];
                    if (cellView == null)
                    {
                        continue;
                    }

                    cellView.SetRevealBombs(revealBombs);
                    cellView.UpdateView();
                }
            }
        }

        public void Clear()
        {
            if (_gridRoot != null)
            {
                Destroy(_gridRoot.gameObject);
                _gridRoot = null;
            }

            _cellViews = null;
        }
    }
}

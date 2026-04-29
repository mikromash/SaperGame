using System.Collections.Generic;

namespace Minesweeper
{
    public sealed class FloodFillSystem
    {
        private static readonly (int x, int y)[] NeighborOffsets =
        {
            (-1, -1),
            (-1, 0),
            (-1, 1),
            (0, -1),
            (0, 1),
            (1, -1),
            (1, 0),
            (1, 1)
        };

        private readonly Cell[,] _grid;

        public FloodFillSystem(Cell[,] grid)
        {
            _grid = grid;
        }

        public void FloodOpen(Cell startCell)
        {
            if (startCell == null || startCell.hasBomb || startCell.neighbourBombs != 0)
            {
                return;
            }

            int width = _grid.GetLength(0);
            int height = _grid.GetLength(1);
            bool[,] visited = new bool[width, height];
            Queue<Cell> queue = new Queue<Cell>();
            queue.Enqueue(startCell);
            visited[startCell.x, startCell.y] = true;

            while (queue.Count > 0)
            {
                Cell current = queue.Dequeue();

                for (int index = 0; index < NeighborOffsets.Length; index++)
                {
                    (int offsetX, int offsetY) = NeighborOffsets[index];
                    int neighborX = current.x + offsetX;
                    int neighborY = current.y + offsetY;

                    if (!IsInsideGrid(neighborX, neighborY) || visited[neighborX, neighborY])
                    {
                        continue;
                    }

                    visited[neighborX, neighborY] = true;
                    Cell neighbor = _grid[neighborX, neighborY];

                    if (neighbor == null || neighbor.hasBomb || neighbor.isFlagged)
                    {
                        continue;
                    }

                    neighbor.isOpened = true;

                    if (neighbor.neighbourBombs == 0)
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        private bool IsInsideGrid(int x, int y)
        {
            return x >= 0 && x < _grid.GetLength(0) && y >= 0 && y < _grid.GetLength(1);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Minesweeper
{
    public sealed class GridGenerator
    {
        private static readonly Vector2Int[] NeighborOffsets =
        {
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, -1),
            new Vector2Int(0, 1),
            new Vector2Int(1, -1),
            new Vector2Int(1, 0),
            new Vector2Int(1, 1)
        };

        private readonly int _width;
        private readonly int _height;
        private readonly int _bombCount;
        private readonly int _seed;
        private readonly System.Random _random;
        private Cell[,] _grid;

        public GridGenerator(int width, int height, int bombCount, int seed)
        {
            _width = width;
            _height = height;
            _bombCount = bombCount;
            _seed = seed;
            _random = new System.Random(seed);
        }

        public Cell[,] CreateGrid()
        {
            _grid = new Cell[_width, _height];
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    _grid[x, y] = new Cell(x, y);
                }
            }

            PlaceBombs();
            CalculateNeighbours();
            return _grid;
        }

        public void PlaceBombs()
        {
            List<Cell> allCells = new List<Cell>(_width * _height);
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    allCells.Add(_grid[x, y]);
                }
            }

            for (int index = allCells.Count - 1; index > 0; index--)
            {
                int swapIndex = _random.Next(index + 1);
                Cell temporary = allCells[index];
                allCells[index] = allCells[swapIndex];
                allCells[swapIndex] = temporary;
            }

            int appliedBombs = Mathf.Min(_bombCount, allCells.Count);
            for (int index = 0; index < appliedBombs; index++)
            {
                allCells[index].hasBomb = true;
            }
        }

        public void CalculateNeighbours()
        {
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    Cell cell = _grid[x, y];
                    if (cell.hasBomb)
                    {
                        cell.neighbourBombs = 0;
                        continue;
                    }

                    int bombCount = 0;
                    for (int index = 0; index < NeighborOffsets.Length; index++)
                    {
                        Vector2Int offset = NeighborOffsets[index];
                        int neighborX = x + offset.x;
                        int neighborY = y + offset.y;

                        if (!IsInsideGrid(neighborX, neighborY))
                        {
                            continue;
                        }

                        if (_grid[neighborX, neighborY].hasBomb)
                        {
                            bombCount++;
                        }
                    }

                    cell.neighbourBombs = bombCount;
                }
            }
        }

        public bool MoveBombFromFirstOpen(Cell firstCell, out Cell movedBombCell)
        {
            movedBombCell = null;

            if (_grid == null || firstCell == null || !firstCell.hasBomb)
            {
                return false;
            }

            List<Cell> validTargets = new List<Cell>(_width * _height);
            for (int x = 0; x < _width; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    Cell candidate = _grid[x, y];
                    if (candidate == null || candidate.hasBomb || candidate.isOpened || candidate.isFlagged || candidate == firstCell)
                    {
                        continue;
                    }

                    validTargets.Add(candidate);
                }
            }

            if (validTargets.Count == 0)
            {
                return false;
            }

            System.Random firstClickRandom = new System.Random(GetFirstClickSeed(firstCell));
            Cell target = validTargets[firstClickRandom.Next(validTargets.Count)];

            firstCell.hasBomb = false;
            target.hasBomb = true;
            movedBombCell = target;
            CalculateNeighbours();
            return true;
        }

        private int GetFirstClickSeed(Cell firstCell)
        {
            unchecked
            {
                int hash = _seed;
                hash = (hash * 397) ^ firstCell.x;
                hash = (hash * 397) ^ firstCell.y;
                hash = (hash * 397) ^ _width;
                hash = (hash * 397) ^ _height;
                return hash;
            }
        }

        private bool IsInsideGrid(int x, int y)
        {
            return x >= 0 && x < _width && y >= 0 && y < _height;
        }
    }
}

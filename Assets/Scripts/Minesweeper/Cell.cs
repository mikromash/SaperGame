namespace Minesweeper
{
    public sealed class Cell
    {
        public bool hasBomb;
        public bool isOpened;
        public bool isFlagged;
        public int neighbourBombs;
        public int x;
        public int y;

        public Cell(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }
}

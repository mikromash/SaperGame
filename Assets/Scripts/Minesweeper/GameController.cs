using UnityEngine;
using UnityEngine.SceneManagement;

namespace Minesweeper
{
    public sealed class GameController : MonoBehaviour
    {
        private const string RestartAction = "restart";
        private const string OpenAction = "open";
        private const string ToggleFlagAction = "flag";
        private const string GameplaySceneName = "GameplayScene";
        private const int GridWidth = 16;
        private const int GridHeight = 16;
        private const int BombCount = 40;
        private const float CellSize = 3f;

        private static bool _sceneHookRegistered;

        private Cell[,] _grid;
        private GridGenerator _gridGenerator;
        private FloodFillSystem _floodFillSystem;
        private GridView _gridView;
        private InputController _inputController;
        private UIController _uiController;
        private bool _isGameFinished;
        private bool _isApplyingNetworkCommand;

        public bool CanHandleInput
        {
            get
            {
                if (_isGameFinished)
                {
                    return false;
                }

                if (CoopPrototypeController.Instance == null)
                {
                    return true;
                }

                return !CoopPrototypeController.Instance.IsPauseMenuOpen;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            if (_sceneHookRegistered)
            {
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            _sceneHookRegistered = true;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, GameplaySceneName, System.StringComparison.Ordinal))
            {
                return;
            }

            if (FindAnyObjectByType<GameController>() != null)
            {
                return;
            }

            GameObject root = new GameObject("MinesweeperGame");
            root.AddComponent<GameController>();
        }

        private void Awake()
        {
            _gridView = gameObject.GetComponent<GridView>();
            if (_gridView == null)
            {
                _gridView = gameObject.AddComponent<GridView>();
            }

            _inputController = gameObject.GetComponent<InputController>();
            if (_inputController == null)
            {
                _inputController = gameObject.AddComponent<InputController>();
            }

            _uiController = gameObject.GetComponent<UIController>();
            if (_uiController == null)
            {
                _uiController = gameObject.AddComponent<UIController>();
            }

            _inputController.Init(this);
            _uiController.Init(this);

            if (IsCoopSynchronizedGame && !IsHostPlayer)
            {
                _uiController.ShowSyncing();
            }
            else
            {
                RestartGame();
            }
        }

        public void OpenCell(Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            if (ShouldSendNetworkCommand)
            {
                CoopPrototypeController.Instance?.SendMinesweeperCommand(OpenAction, cell.x, cell.y);
                return;
            }

            OpenCellInternal(cell);
        }

        public void ToggleFlag(Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            if (ShouldSendNetworkCommand)
            {
                CoopPrototypeController.Instance?.SendMinesweeperCommand(ToggleFlagAction, cell.x, cell.y);
                return;
            }

            ToggleFlagInternal(cell);
        }

        public void RestartGame()
        {
            if (ShouldSendNetworkCommand)
            {
                int seed = Random.Range(int.MinValue, int.MaxValue);
                RestartGameInternal(seed);
                CoopPrototypeController.Instance?.SendMinesweeperCommand(RestartAction, boardSeed: seed);
                return;
            }

            int localSeed = Random.Range(int.MinValue, int.MaxValue);
            RestartGameInternal(localSeed);
        }

        public void HandleNetworkCommand(string action, int cellX, int cellY, int boardSeed)
        {
            _isApplyingNetworkCommand = true;

            try
            {
                if (string.Equals(action, RestartAction, System.StringComparison.Ordinal))
                {
                    RestartGameInternal(boardSeed);
                    return;
                }

                Cell cell = GetCell(cellX, cellY);
                if (cell == null)
                {
                    return;
                }

                if (string.Equals(action, OpenAction, System.StringComparison.Ordinal))
                {
                    OpenCellInternal(cell);
                    return;
                }

                if (string.Equals(action, ToggleFlagAction, System.StringComparison.Ordinal))
                {
                    ToggleFlagInternal(cell);
                }
            }
            finally
            {
                _isApplyingNetworkCommand = false;
            }
        }

        private void OpenCellInternal(Cell cell)
        {
            if (!CanOpenCell(cell))
            {
                return;
            }

            cell.isOpened = true;
            cell.isFlagged = false;

            if (cell.hasBomb)
            {
                GameOver();
                return;
            }

            if (cell.neighbourBombs == 0)
            {
                _floodFillSystem.FloodOpen(cell);
            }

            _gridView.RefreshAllViews();

            if (CheckWin())
            {
                EndGame(true);
            }
        }

        private void ToggleFlagInternal(Cell cell)
        {
            if (_isGameFinished || cell == null || cell.isOpened)
            {
                return;
            }

            cell.isFlagged = !cell.isFlagged;
            _gridView.RefreshAllViews();
        }

        public bool CheckWin()
        {
            if (_grid == null)
            {
                return false;
            }

            for (int x = 0; x < _grid.GetLength(0); x++)
            {
                for (int y = 0; y < _grid.GetLength(1); y++)
                {
                    Cell cell = _grid[x, y];
                    if (!cell.hasBomb && !cell.isOpened)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void GameOver()
        {
            EndGame(false);
        }

        private void RestartGameInternal(int seed)
        {
            _isGameFinished = false;
            CoopPrototypeController.Instance?.ResetPlayersToMinesweeperStart();
            _gridGenerator = new GridGenerator(GridWidth, GridHeight, BombCount, seed);
            _grid = _gridGenerator.CreateGrid();
            _floodFillSystem = new FloodFillSystem(_grid);

            _gridView.Build(_grid, CellSize);
            _gridView.SetRevealBombs(false);
            _uiController.HideState();
        }

        private Cell GetCell(int x, int y)
        {
            if (_grid == null)
            {
                return null;
            }

            if (x < 0 || x >= _grid.GetLength(0) || y < 0 || y >= _grid.GetLength(1))
            {
                return null;
            }

            return _grid[x, y];
        }

        private bool CanOpenCell(Cell cell)
        {
            if (_isGameFinished || cell == null || _grid == null)
            {
                return false;
            }

            if (cell.isOpened || cell.isFlagged)
            {
                return false;
            }

            return true;
        }

        private void EndGame(bool isWin)
        {
            _isGameFinished = true;
            _gridView.SetRevealBombs(!isWin);

            if (isWin)
            {
                _uiController.ShowWin();
            }
            else
            {
                _uiController.ShowLose();
            }
        }

        private bool IsCoopSynchronizedGame
        {
            get
            {
                CoopPrototypeController controller = CoopPrototypeController.Instance;
                return controller != null && controller.IsMinesweeperSyncActive;
            }
        }

        private bool IsHostPlayer
        {
            get
            {
                CoopPrototypeController controller = CoopPrototypeController.Instance;
                return controller != null && controller.IsHost;
            }
        }

        private bool ShouldSendNetworkCommand => IsCoopSynchronizedGame && !_isApplyingNetworkCommand;
    }
}

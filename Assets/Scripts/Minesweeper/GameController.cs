using UnityEngine;
using UnityEngine.SceneManagement;

namespace Minesweeper
{
    public sealed class GameController : MonoBehaviour
    {
        private const string RestartAction = "restart";
        private const string OpenAction = "open";
        private const string ToggleFlagAction = "flag";
        private const string DebugWinAction = "debug_win";
        private const string DebugRevealBombsOnAction = "debug_reveal_bombs_on";
        private const string DebugRevealBombsOffAction = "debug_reveal_bombs_off";
        private const string GameplaySceneName = "GameplayScene";
        private const int GridWidth = 16;
        private const int GridHeight = 16;
        private const int BombCount = 40;
        private const float CellSize = 3f;
        private const int GameDurationSeconds = 120;

        private static bool _sceneHookRegistered;

        private Cell[,] _grid;
        private GridGenerator _gridGenerator;
        private FloodFillSystem _floodFillSystem;
        private GridView _gridView;
        private InputController _inputController;
        private UIController _uiController;
        private bool _isGameFinished;
        private bool _isApplyingNetworkCommand;
        private bool _hasOpenedFirstCell;
        private bool _debugRevealBombs;
        private float _elapsedTime;
        private int _elapsedSeconds;

        public event System.Action HudStateChanged;

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

        public bool HasOpenedFirstCell => _hasOpenedFirstCell;
        public bool CanToggleFlags => _hasOpenedFirstCell && !_isGameFinished;
        public bool IsGameFinished => _isGameFinished;
        public bool DebugRevealBombs => _debugRevealBombs;
        public int BombsTotal => BombCount;
        public int GameTimeLimitSeconds => GameDurationSeconds;
        public int ElapsedSeconds => _elapsedSeconds;
        public int RemainingSeconds => Mathf.Max(0, GameDurationSeconds - _elapsedSeconds);
        public bool IsCountdownWarningActive => _hasOpenedFirstCell && !_isGameFinished && RemainingSeconds <= 10;
        public int FlaggedCells => CountFlaggedCells();

        private void Update()
        {
            if (_isGameFinished || _grid == null || !_hasOpenedFirstCell)
            {
                return;
            }

            _elapsedTime += Time.deltaTime;
            int currentSeconds = Mathf.FloorToInt(_elapsedTime);
            if (currentSeconds == _elapsedSeconds)
            {
                return;
            }

            _elapsedSeconds = currentSeconds;
            NotifyHudStateChanged();

            if (IsCountdownWarningActive && RemainingSeconds > 0)
            {
                AudioController.Play(AudioEvent.TimerCountdownTick);
            }

            if (_elapsedSeconds >= GameDurationSeconds)
            {
                AudioController.PlayAt(AudioEvent.BombExplode, _gridView != null ? _gridView.transform.position : transform.position);
                GameOver();
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

            if (!CanToggleFlags)
            {
                AudioController.Play(AudioEvent.CellBlocked);
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

        public void DebugWinGame()
        {
            if (_isGameFinished || _grid == null)
            {
                return;
            }

            if (ShouldSendNetworkCommand)
            {
                CoopPrototypeController.Instance?.SendMinesweeperCommand(DebugWinAction);
                return;
            }

            EndGame(true);
        }

        public void SetDebugRevealBombs(bool revealBombs)
        {
            if (_grid == null)
            {
                return;
            }

            if (ShouldSendNetworkCommand)
            {
                string action = revealBombs ? DebugRevealBombsOnAction : DebugRevealBombsOffAction;
                CoopPrototypeController.Instance?.SendMinesweeperCommand(action);
                return;
            }

            SetDebugRevealBombsInternal(revealBombs);
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

                if (string.Equals(action, DebugWinAction, System.StringComparison.Ordinal))
                {
                    DebugWinGame();
                    return;
                }

                if (string.Equals(action, DebugRevealBombsOnAction, System.StringComparison.Ordinal))
                {
                    SetDebugRevealBombsInternal(true);
                    return;
                }

                if (string.Equals(action, DebugRevealBombsOffAction, System.StringComparison.Ordinal))
                {
                    SetDebugRevealBombsInternal(false);
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
                AudioController.Play(AudioEvent.CellBlocked);
                return;
            }

            EnsureFirstOpenIsSafe(cell);
            _hasOpenedFirstCell = true;
            cell.isOpened = true;
            cell.isFlagged = false;

            if (cell.hasBomb)
            {
                AudioController.PlayAt(AudioEvent.BombExplode, _gridView != null ? _gridView.transform.position : transform.position);
                GameOver();
                return;
            }

            if (cell.neighbourBombs == 0)
            {
                _floodFillSystem.FloodOpen(cell);
                AudioController.Play(AudioEvent.CellOpenEmpty);
            }
            else
            {
                AudioController.Play(AudioEvent.CellOpen);
            }

            _gridView.RefreshAllViews();
            NotifyHudStateChanged();

            if (CheckWin())
            {
                EndGame(true);
            }
        }

        private void ToggleFlagInternal(Cell cell)
        {
            if (!CanToggleFlags)
            {
                AudioController.Play(AudioEvent.CellBlocked);
                return;
            }

            if (_isGameFinished || cell == null || cell.isOpened)
            {
                return;
            }

            if (!cell.isFlagged && FlaggedCells >= BombCount)
            {
                AudioController.Play(AudioEvent.CellBlocked);
                return;
            }

            cell.isFlagged = !cell.isFlagged;
            AudioController.Play(cell.isFlagged ? AudioEvent.CellFlagOn : AudioEvent.CellFlagOff);
            _gridView.RefreshAllViews();
            NotifyHudStateChanged();
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
            bool hadGrid = _grid != null;
            _isGameFinished = false;
            _hasOpenedFirstCell = false;
            _debugRevealBombs = false;
            _elapsedTime = 0f;
            _elapsedSeconds = 0;
            StopCountdownAudio();
            CoopPrototypeController.Instance?.ResetPlayersToMinesweeperStart();
            _gridGenerator = new GridGenerator(GridWidth, GridHeight, BombCount, seed);
            _grid = _gridGenerator.CreateGrid();
            _floodFillSystem = new FloodFillSystem(_grid);

            _gridView.Build(_grid, CellSize);
            _gridView.SetRevealBombs(false);
            _uiController.HideState();
            NotifyHudStateChanged();

            if (hadGrid)
            {
                AudioController.Play(AudioEvent.GameRestart);
                AudioController.PlayMusicTrack(MusicTrack.Gameplay);
            }
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

        private int CountFlaggedCells()
        {
            if (_grid == null)
            {
                return 0;
            }

            int count = 0;
            for (int x = 0; x < _grid.GetLength(0); x++)
            {
                for (int y = 0; y < _grid.GetLength(1); y++)
                {
                    if (_grid[x, y].isFlagged)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private void EnsureFirstOpenIsSafe(Cell cell)
        {
            if (_hasOpenedFirstCell || cell == null || !cell.hasBomb || _gridGenerator == null)
            {
                return;
            }

            _gridGenerator.MoveBombFromFirstOpen(cell);
        }

        private void SetDebugRevealBombsInternal(bool revealBombs)
        {
            if (_debugRevealBombs == revealBombs || _gridView == null)
            {
                return;
            }

            _debugRevealBombs = revealBombs;
            _gridView.SetRevealBombs(revealBombs);
            NotifyHudStateChanged();
        }

        private void EndGame(bool isWin)
        {
            _isGameFinished = true;
            StopCountdownAudio();
            _gridView.SetRevealBombs(!isWin);
            NotifyHudStateChanged();

            if (isWin)
            {
                AudioController.Play(AudioEvent.GameWin);
                AudioController.PlayMusicTrack(MusicTrack.Win);
                _uiController.ShowWin();
            }
            else
            {
                AudioController.Play(AudioEvent.GameLose);
                AudioController.PlayMusicTrack(MusicTrack.Lose);
                _uiController.ShowLose();
            }
        }

        private void NotifyHudStateChanged()
        {
            HudStateChanged?.Invoke();
        }

        private static void StopCountdownAudio()
        {
            AudioController.Stop(AudioEvent.TimerCountdownTick);
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

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Minesweeper
{
    public sealed class InputController : MonoBehaviour
    {
        private GameController _gameController;

        public void Init(GameController gameController)
        {
            _gameController = gameController;
        }

        private void Update()
        {
            if (_gameController == null || !_gameController.CanHandleInput)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                HandleLeftClick();
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                HandleRightClick();
            }
        }

        public void HandleLeftClick()
        {
            if (TryGetCellUnderCursor(out Cell cell))
            {
                _gameController.OpenCell(cell);
            }
        }

        public void HandleRightClick()
        {
            if (TryGetCellUnderCursor(out Cell cell))
            {
                _gameController.ToggleFlag(cell);
            }
        }

        private bool TryGetCellUnderCursor(out Cell cell)
        {
            cell = null;

            Camera camera = Camera.main;
            Mouse mouse = Mouse.current;
            if (camera == null || mouse == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(ray, 500f);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int index = 0; index < hits.Length; index++)
            {
                if (!hits[index].collider.TryGetComponent(out CellView cellView) || cellView.Cell == null)
                {
                    continue;
                }

                cell = cellView.Cell;
                return true;
            }

            return false;
        }
    }
}

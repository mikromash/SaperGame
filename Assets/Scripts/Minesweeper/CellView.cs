using TMPro;
using UnityEngine;

namespace Minesweeper
{
    [RequireComponent(typeof(Renderer))]
    public sealed class CellView : MonoBehaviour
    {
        private static readonly Color ClosedColor = new Color(0.23f, 0.27f, 0.33f);
        private static readonly Color OpenedColor = new Color(0.78f, 0.8f, 0.83f);
        private static readonly Color FlaggedColor = new Color(0.82f, 0.26f, 0.24f);
        private static readonly Color BombColor = new Color(0.1f, 0.1f, 0.1f);
        private static readonly Color EmptyTextColor = new Color(0f, 0f, 0f, 0f);
        private static readonly Color[] NumberColors =
        {
            EmptyTextColor,
            new Color(0.20f, 0.45f, 0.95f),
            new Color(0.17f, 0.62f, 0.26f),
            new Color(0.88f, 0.22f, 0.2f),
            new Color(0.43f, 0.23f, 0.75f),
            new Color(0.58f, 0.14f, 0.14f),
            new Color(0.12f, 0.59f, 0.66f),
            new Color(0.19f, 0.19f, 0.19f),
            new Color(0.52f, 0.52f, 0.52f)
        };

        private Cell _cell;
        private Renderer _renderer;
        private Material _materialInstance;
        private TextMeshPro _label;
        private bool _revealBombs;

        public Cell Cell => _cell;

        public void Init(Cell cell)
        {
            _cell = cell;
            _renderer = GetComponent<Renderer>();
            _materialInstance = CreateCellMaterial();
            _renderer.sharedMaterial = _materialInstance;
            EnsureLabel();
            UpdateView();
        }

        public void SetRevealBombs(bool revealBombs)
        {
            _revealBombs = revealBombs;
        }

        public void UpdateView()
        {
            if (_cell == null || _renderer == null || _label == null)
            {
                return;
            }

            bool showBomb = _cell.hasBomb && (_cell.isOpened || _revealBombs);
            bool showNumber = _cell.isOpened && !_cell.hasBomb && _cell.neighbourBombs > 0;
            bool showFlag = !_cell.isOpened && _cell.isFlagged;

            if (showBomb)
            {
                _materialInstance.color = BombColor;
                _label.text = "B";
                _label.color = Color.white;
                return;
            }

            if (_cell.isOpened)
            {
                _materialInstance.color = OpenedColor;
                if (showNumber)
                {
                    _label.text = _cell.neighbourBombs.ToString();
                    _label.color = NumberColors[Mathf.Clamp(_cell.neighbourBombs, 0, NumberColors.Length - 1)];
                }
                else
                {
                    _label.text = string.Empty;
                    _label.color = EmptyTextColor;
                }

                return;
            }

            if (showFlag)
            {
                _materialInstance.color = FlaggedColor;
                _label.text = "F";
                _label.color = Color.white;
                return;
            }

            _materialInstance.color = ClosedColor;
            _label.text = string.Empty;
            _label.color = EmptyTextColor;
        }

        private static Material CreateCellMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            return new Material(shader);
        }

        private void EnsureLabel()
        {
            if (_label != null)
            {
                return;
            }

            Transform labelTransform = transform.Find("Label");
            if (labelTransform == null)
            {
                GameObject labelObject = new GameObject("Label");
                labelObject.transform.SetParent(transform, false);
                labelTransform = labelObject.transform;
            }

            _label = labelTransform.GetComponent<TextMeshPro>();
            if (_label == null)
            {
                _label = labelTransform.gameObject.AddComponent<TextMeshPro>();
            }

            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = 8f;
            _label.raycastTarget = false;
            _label.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            _label.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            _label.transform.localScale = Vector3.one * 0.25f;
        }
    }
}

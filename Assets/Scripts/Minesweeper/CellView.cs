using TMPro;
using UnityEngine;

namespace Minesweeper
{
    [RequireComponent(typeof(Renderer))]
    public sealed class CellView : MonoBehaviour
    {
        private static readonly Color ClosedColor = new Color(0.23f, 0.27f, 0.33f);
        private static readonly Color OpenedColor = new Color(0.78f, 0.8f, 0.83f);
        private static readonly Color FlaggedColor = new Color(0.82f, 0.26f, 0.24f); // Більше не використовується для фону, але залишено для сумісності
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
        private GameObject _flagObject; // Посилання на 3D-об'єкт прапорця
        private bool _revealBombs;

        public Cell Cell => _cell;

        public void Init(Cell cell)
        {
            _cell = cell;
            _renderer = GetComponent<Renderer>();
            _materialInstance = CreateCellMaterial();
            _renderer.sharedMaterial = _materialInstance;
            EnsureLabel();
            EnsureFlagObject(); // Ініціалізуємо 3D-прапорець при створенні клітинки
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

            // Керуємо видимістю 3D-моделі прапорця.
            // Прапорець показується тільки якщо він встановлений і ми зараз не показуємо бомбу (кінець гри).
            if (_flagObject != null)
            {
                _flagObject.SetActive(showFlag && !showBomb);
            }

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

            // Якщо клітинка закрита (з флажком або без), вона має стандартний вигляд.
            // Текст "F" та зафарбування у FlaggedColor більше не використовуються.
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

        // Метод для завантаження та створення 3D-об'єкта прапорця
        private void EnsureFlagObject()
        {
            if (_flagObject != null)
            {
                return;
            }

            Transform flagTransform = transform.Find("Flag");

            if (flagTransform == null)
            {
                // Завантажуємо префаб з папки Resources
                GameObject loadedPrefab = Resources.Load<GameObject>("FlagPrefab");
                
                if (loadedPrefab != null)
                {
                    _flagObject = Instantiate(loadedPrefab, transform, false);
                    _flagObject.name = "Flag";

                    // --- ВИПРАВЛЕННЯ СПЛЮЩЕННЯ ---
                    // Отримуємо глобальний масштаб клітинки
                    Vector3 parentScale = transform.lossyScale; 
                    
                    // Задаємо прапорцю такий локальний масштаб, який скасує вплив батька
                    // і поверне прапорцю його оригінальні пропорції з префабу
                    _flagObject.transform.localScale = new Vector3(
                        loadedPrefab.transform.localScale.x / parentScale.x,
                        loadedPrefab.transform.localScale.y / parentScale.y,
                        loadedPrefab.transform.localScale.z / parentScale.z
                    );

                    // Налаштовуємо позицію. Після зміни масштабу можливо доведеться відредагувати це значення.
                    _flagObject.transform.localPosition = new Vector3(0f, 2f, 0f); 

                    // Видаляємо колайдер, щоб кліки мишкою доходили до самої клітинки
                    Collider flagCollider = _flagObject.GetComponent<Collider>();
                    if (flagCollider != null)
                    {
                        Destroy(flagCollider);
                    }
                }
                else
                {
                    Debug.LogWarning("Minesweeper: Не вдалося знайти 'FlagPrefab' у папці Resources!");
                }
            }
            else
            {
                // Якщо об'єкт 'Flag' вже існує
                _flagObject = flagTransform.gameObject;
            }

            // Ховаємо прапорець за замовчуванням
            if (_flagObject != null)
            {
                _flagObject.SetActive(false);
            }
        }
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Components")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private TMP_Text buttonText;

    [Header("Animation Settings")]
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float clickScale = 0.95f;
    [SerializeField] private float animationSpeed = 15f;

    [Header("Visual Settings (Sprites)")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite hoverSprite;
    [SerializeField] private Sprite selectedSprite;

    [Header("Visual Settings (Colors)")]
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private Color hoverTextColor = Color.yellow; // Колір при наведенні
    [SerializeField] private Color clickTextColor = Color.gray; // Колір при натисканні
    [SerializeField] private Color selectedTextColor = Color.green; // Колір, коли кнопка вибрана

    private Vector3 _originalScale;
    private Vector3 _targetScale;
    private bool _isHovering;
    private bool _isSelected;
    private bool _isInitialized;

    private void Awake()
    {
        _originalScale = transform.localScale;
        _targetScale = _originalScale;

        if (buttonImage == null) buttonImage = GetComponent<Image>();
        if (buttonText == null) buttonText = GetComponentInChildren<TMP_Text>();

        RefreshVisuals();
        _isInitialized = true;
    }

    private void OnEnable()
    {
        ResetPointerState();
    }

    private void OnDisable()
    {
        ResetPointerState();
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * animationSpeed);
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        if (_isSelected)
        {
            SetVisuals(selectedSprite, selectedTextColor);
            return;
        }
// Якщо мишка зараз над кнопкою, але вона не вибрана — показуємо ховер-ефект

        if (_isHovering)
        {
            SetVisuals(hoverSprite, hoverTextColor);
        }
        else
        {
            SetVisuals(normalSprite, normalTextColor);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;
        // Якщо вибрано, не міняємо візуал на ховер
        if (_isSelected)
        {
            return;
        }

        _targetScale = _originalScale * hoverScale;
        SetVisuals(hoverSprite, hoverTextColor);
        AudioController.Play(AudioEvent.UiHover);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        if (_isSelected)
        {
            return;
        }

        _targetScale = _originalScale;
        SetVisuals(normalSprite, normalTextColor);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_isSelected)
        {
            return;
        }
// Міняємо колір тексту на "клік"

        _targetScale = _originalScale * clickScale;
        SetVisuals(hoverSprite, clickTextColor);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_isSelected)
        {
            return;
        }

        _targetScale = _isHovering ? _originalScale * hoverScale : _originalScale;
        // Повертаємо візуал до ховеру або нормального стану
        RefreshVisuals();
    }

    private void SetVisuals(Sprite sprite, Color color)
    {
        if (buttonImage != null && sprite != null) buttonImage.sprite = sprite;
        if (buttonText != null) buttonText.color = color;
    }

    private void ResetPointerState()
    {
        if (!_isInitialized)
        {
            return;
        }

        _isHovering = false;
        _targetScale = _originalScale;
        transform.localScale = _originalScale;
        RefreshVisuals();
    }
}

using UnityEngine;

namespace Minesweeper
{
    public sealed class TimerPulseAnimator : MonoBehaviour
    {
        [SerializeField] private float pulseScale = 1.14f;
        [SerializeField] private float pulseSpeed = 6f;

        private Vector3 _baseScale;
        private bool _isPulsing;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (_baseScale == Vector3.zero)
            {
                _baseScale = transform.localScale;
            }
        }

        private void Update()
        {
            if (!_isPulsing)
            {
                return;
            }

            float wave = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            transform.localScale = Vector3.Lerp(_baseScale, _baseScale * pulseScale, wave);
        }

        public void SetPulsing(bool value)
        {
            if (_isPulsing == value)
            {
                return;
            }

            _isPulsing = value;
            if (!_isPulsing)
            {
                transform.localScale = _baseScale;
            }
        }
    }
}

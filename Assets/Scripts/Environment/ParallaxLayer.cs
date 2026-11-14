using UnityEngine;

namespace Ignis.Environment
{
    /// <summary>
    /// Компонент слоя параллакса.
    /// Перемещает слой относительно движения камеры с заданным коэффициентом
    /// и автоматически зацикливает спрайт по горизонтали при выходе за границы.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("Camera & Speed")]
        [Tooltip("Камера, за которой следит параллакс (по умолчанию Camera.main)")]
        [SerializeField] private Transform targetCamera;
        
        [Tooltip("Коэффициент параллакса: 0 = стоит на месте, 0.2 = далекий фон, 0.7 = средний план, >1 = передний план")]
        [SerializeField] private Vector2 parallaxEffectMultiplier = new Vector2(0.5f, 0.2f);

        [Header("Infinite Scrolling (Опционально)")]
        [Tooltip("Включить бесшовное зацикливание спрайта по горизонтали")]
        [SerializeField] private bool infiniteHorizontal = true;

        private Vector3 _lastCameraPosition;
        private float _textureUnitSizeX;

        private void Start()
        {
            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main.transform;
            }

            if (targetCamera != null)
            {
                _lastCameraPosition = targetCamera.position;
            }

            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                _textureUnitSizeX = sr.bounds.size.x;
            }
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;

            Vector3 deltaMovement = targetCamera.position - _lastCameraPosition;
            
            // Двигаем слой пропорционально движению камеры
            transform.position += new Vector3(
                deltaMovement.x * parallaxEffectMultiplier.x,
                deltaMovement.y * parallaxEffectMultiplier.y,
                0f
            );

            _lastCameraPosition = targetCamera.position;

            // Бесшовный бесконечный скроллинг по X
            if (infiniteHorizontal && _textureUnitSizeX > 0.01f)
            {
                float offsetFromCameraX = targetCamera.position.x - transform.position.x;
                if (Mathf.Abs(offsetFromCameraX) >= _textureUnitSizeX)
                {
                    float repeatOffset = (offsetFromCameraX > 0) ? _textureUnitSizeX : -_textureUnitSizeX;
                    transform.position = new Vector3(transform.position.x + repeatOffset, transform.position.y, transform.position.z);
                }
            }
        }
    }
}

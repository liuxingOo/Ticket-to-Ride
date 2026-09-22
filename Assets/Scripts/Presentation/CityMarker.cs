using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TicketToRide.Presentation
{
    /// <summary>
    /// 地图上一个城市的标记：负责“城市在地图上的位置”，本身不参与点击认领，
    /// 但带一块透明射线区域，鼠标悬停时弹出中文城市名。
    /// 城市用英文名作为唯一标识，必须与领域层 Route / DestinationTicket
    /// 中的 StartCity / EndCity 字符串完全一致（如 Vancouver、Salt Lake City）。
    /// 坐标统一使用 Map.png 原图像素坐标（左下角原点，宽 1744、高 1125）。
    /// </summary>
    [ExecuteAlways]
    public class CityMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("城市唯一英文名，与任务卡、路线数据里的名字完全一致，如 Vancouver。")]
        [SerializeField] private string cityId = "NewCity";

        [Tooltip("Map.png 像素坐标，左下角原点，Scene 视图拖黄色手柄即可修改。")]
        [SerializeField] private Vector2 pixelPosition = new Vector2(872f, 562f);

        [Tooltip("悬停提示显示的中文名，英文名在内置 36 城对照表中时自动填入。")]
        [SerializeField] private string chineseName = "";

        public string CityId => cityId;
        public Vector2 PixelPosition => pixelPosition;
        public string ChineseName => chineseName;

        private bool _ensureQueued;

        private void OnEnable()
        {
            ApplyPosition();
            SafeEnsureRaycastArea();
        }

        private void OnValidate()
        {
            SyncChineseName();
            ApplyPosition();
            SafeEnsureRaycastArea();

#if UNITY_EDITOR
            // 让 Hierarchy 里的物体名始终等于城市名，找城市一目了然。
            if (!string.IsNullOrWhiteSpace(cityId) && gameObject.name != cityId)
            {
                gameObject.name = cityId;
            }
#endif
        }

        /// <summary>新建城市时由编辑器工具调用。</summary>
        public void Initialize(string id, Vector2 pixel)
        {
            cityId = string.IsNullOrWhiteSpace(id) ? "NewCity" : id.Trim();
            pixelPosition = pixel;
            SyncChineseName();
            ApplyPosition();
            EnsureRaycastArea();

#if UNITY_EDITOR
            gameObject.name = cityId;
#endif
        }

        private void SyncChineseName()
        {
            // 标准 36 城始终以对照表为准；自定义城市保留手动输入。
            if (CityNames.TryGet(cityId, out var zh))
            {
                chineseName = zh;
            }
        }

        /// <summary>Scene 视图手柄拖动后写回像素坐标。</summary>
        public void SetPixel(Vector2 pixel)
        {
            pixelPosition = pixel;
            ApplyPosition();
        }

        public Vector2 PixelToLocal(Vector2 pixel)
        {
            return MapPixelSpace.PixelToLocal(transform.parent as RectTransform, pixel);
        }

        public Vector2 LocalToPixel(Vector2 local)
        {
            return MapPixelSpace.LocalToPixel(transform.parent as RectTransform, local);
        }

        /// <summary>把物体移到像素坐标对应的 UI 位置。</summary>
        public void ApplyPosition()
        {
            var rect = transform as RectTransform;
            var layer = transform.parent as RectTransform;

            if (rect == null || layer == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            // 比城市圆点略大，鼠标靠近即可触发悬停。
            rect.sizeDelta = new Vector2(44f, 44f);
            rect.anchoredPosition = MapPixelSpace.PixelToLocal(layer, pixelPosition);
        }

        /// <summary>
        /// OnValidate/OnEnable 里不能立刻 AddComponent（Unity 报 SendMessage 警告），
        /// 编辑模式下推迟到编辑器空闲时再补透明射线区域。
        /// </summary>
        private void SafeEnsureRaycastArea()
        {
#if UNITY_EDITOR
            if (!UnityEngine.Application.isPlaying)
            {
                if (_ensureQueued)
                {
                    return;
                }

                _ensureQueued = true;
                UnityEditor.EditorApplication.delayCall += DeferredEnsure;
                return;
            }
#endif
            EnsureRaycastArea();
        }

#if UNITY_EDITOR
        private void DeferredEnsure()
        {
            _ensureQueued = false;

            if (this == null || gameObject == null)
            {
                return;
            }

            // 预制体资源本身不补，只作用于场景/实例。
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                return;
            }

            EnsureRaycastArea();
        }
#endif

        private void EnsureRaycastArea()
        {
            var image = GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
            }

            // alpha=0 完全看不见，但 UGUI 默认仍对整块矩形区域进行射线检测。
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!UnityEngine.Application.isPlaying || string.IsNullOrWhiteSpace(chineseName))
            {
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            CityTooltip.Get(canvas).Show(chineseName);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (CityTooltip.Exists)
            {
                CityTooltip.Instance.Hide();
            }
        }
    }
}

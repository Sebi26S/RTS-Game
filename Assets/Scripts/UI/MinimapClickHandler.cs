using RTS.EventBus;
using RTS.Events;
using RTS.Units;
using RTS.Commands;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem;

namespace RTS.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class MinimapClickHandler : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerMoveHandler, IPointerEnterHandler
    {
        [Header("Refs")]
        [SerializeField] private Camera minimapCamera;
        [SerializeField] private Transform cameraTarget;

        [Header("Raycast")]
        [SerializeField] private LayerMask floorMask;

        [Header("World Bounds (use existing wall colliders)")]
        [Tooltip("Drag your existing 'World Collider Bounds' GameObject here (the one with the 4 wall BoxColliders).")]
        [SerializeField] private Transform worldBoundsRoot;

        [Tooltip("Keep camera target this far away from the inner edge.")]
        [SerializeField] private float edgePadding = 0.5f;

        private bool isMouseDownOnMinimap;
        private RectTransform rectTransform;
        private BaseCommand activeCommand;

        private bool hasWorldLimits;
        private float minX, maxX, minZ, maxZ;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();

            if (minimapCamera == null || cameraTarget == null)
            {
                Debug.LogError("MinimapClickHandler is missing references (minimapCamera/cameraTarget).", this);
                enabled = false;
                return;
            }

            RebuildWorldLimits();

            Bus<CommandSelectedEvent>.OnEvent[Owner.Player1] += HandleCommandSelected;
            Bus<CommandIssuedEvent>.OnEvent[Owner.Player1] += HandleCommandIssued;
        }

        private void OnDestroy()
        {
            Bus<CommandSelectedEvent>.OnEvent[Owner.Player1] -= HandleCommandSelected;
            Bus<CommandIssuedEvent>.OnEvent[Owner.Player1] -= HandleCommandIssued;
        }

        private void HandleCommandSelected(CommandSelectedEvent evt) => activeCommand = evt.Command;
        private void HandleCommandIssued(CommandIssuedEvent evt) => activeCommand = null;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && activeCommand == null)
            {
                isMouseDownOnMinimap = true;
                MoveVirtualCameraTarget(eventData.position);
            }
        }

        public void OnPointerMove(PointerEventData eventData) => MoveVirtualCameraTarget(eventData.position);

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                isMouseDownOnMinimap = false;
                RaiseClickEvent(eventData.position, MouseButton.Left);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                RaiseClickEvent(eventData.position, MouseButton.Right);
            }
        }

        public void OnPointerExit(PointerEventData eventData) => isMouseDownOnMinimap = false;

        private void MoveVirtualCameraTarget(Vector2 screenMousePosition)
        {
            if (!isMouseDownOnMinimap) return;

            if (RaycastFromMousePosition(screenMousePosition, out RaycastHit hit))
            {
                Vector3 pos = ClampToWorldLimits(hit.point);
                cameraTarget.position = pos;
            }
        }

        private bool RaycastFromMousePosition(Vector2 screenMousePosition, out RaycastHit hit)
        {
            hit = default;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    screenMousePosition,
                    null, 
                    out Vector2 localPoint))
                return false;

            Rect r = rectTransform.rect;
            float u = (localPoint.x - r.xMin) / r.width;
            float v = (localPoint.y - r.yMin) / r.height;

            if (u < 0f || u > 1f || v < 0f || v > 1f)
                return false;

            float px = u * minimapCamera.pixelWidth;
            float py = v * minimapCamera.pixelHeight;

            Ray cameraRay = minimapCamera.ScreenPointToRay(new Vector3(px, py, 0f));
            return Physics.Raycast(cameraRay, out hit, float.MaxValue, floorMask);
        }

        private void RaiseClickEvent(Vector2 screenMousePosition, MouseButton button)
        {
            if (RaycastFromMousePosition(screenMousePosition, out RaycastHit hit))
            {
                hit.point = ClampToWorldLimits(hit.point);
                Bus<MinimapClickEvent>.Raise(Owner.Player1, new MinimapClickEvent(button, hit));
            }
        }

        private Vector3 ClampToWorldLimits(Vector3 p)
        {
            if (!hasWorldLimits)
                return p;

            p.x = Mathf.Clamp(p.x, minX + edgePadding, maxX - edgePadding);
            p.z = Mathf.Clamp(p.z, minZ + edgePadding, maxZ - edgePadding);
            return p;
        }

        private void RebuildWorldLimits()
        {
            hasWorldLimits = false;

            if (worldBoundsRoot == null)
            {
                Debug.LogWarning("MinimapClickHandler: worldBoundsRoot not assigned. No clamping will occur.", this);
                return;
            }

            var colliders = worldBoundsRoot.GetComponents<BoxCollider>();
            if (colliders == null || colliders.Length == 0)
                colliders = worldBoundsRoot.GetComponentsInChildren<BoxCollider>();

            if (colliders == null || colliders.Length == 0)
            {
                Debug.LogWarning("MinimapClickHandler: No BoxColliders found under worldBoundsRoot.", this);
                return;
            }

            Bounds outer = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                outer.Encapsulate(colliders[i].bounds);

            float minXSize = float.PositiveInfinity;
            float minZSize = float.PositiveInfinity;
            foreach (var c in colliders)
            {
                minXSize = Mathf.Min(minXSize, c.bounds.size.x);
                minZSize = Mathf.Min(minZSize, c.bounds.size.z);
            }

            float wallThickness = Mathf.Min(minXSize, minZSize);
            if (!float.IsFinite(wallThickness) || wallThickness <= 0f)
                wallThickness = 1f;

            minX = outer.min.x + wallThickness;
            maxX = outer.max.x - wallThickness;
            minZ = outer.min.z + wallThickness;
            maxZ = outer.max.z - wallThickness;

            if (minX >= maxX || minZ >= maxZ)
            {
                Debug.LogWarning(
                    "MinimapClickHandler: Could not infer inner bounds from wall colliders. Assign a dedicated play-area BoxCollider instead.",
                    this
                );
                return;
            }

            hasWorldLimits = true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Mouse.current != null && Mouse.current.leftButton.isPressed && activeCommand == null)
            {
                isMouseDownOnMinimap = true;
                MoveVirtualCameraTarget(eventData.position);
            }
        }
    }
}
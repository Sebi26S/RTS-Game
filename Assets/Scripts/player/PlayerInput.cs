using System.Collections.Generic;
using RTS.EventBus;
using RTS.Events;
using RTS.Units;
using RTS.Commands;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.LowLevel;
using TMPro;

namespace RTS.Player
{
    public class PlayerInput : MonoBehaviour
    {
        [SerializeField] private Rigidbody cameraTarget;
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private new Camera camera;
        [SerializeField] private CameraConfig cameraConfig;
        [SerializeField] private LayerMask selectableUnitsLayers;
        [SerializeField] private LayerMask interactableLayers;
        [SerializeField] private LayerMask floorLayers;
        [SerializeField] private RectTransform selectionBox;

        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)] 
        private Color errorTintColor = Color.red;
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)] 
        private Color errorFresnelColor = new (4, 1.7f, 0, 2);
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)] 
        private Color availableToPlaceTintColor = new (0.2f, 0.65f, 1, 2);
        [SerializeField] [ColorUsage(showAlpha: true, hdr: true)] 
        private Color availableToPlaceFresnelColor = new(4, 1.7f, 0, 2);

        [SerializeField] private Renderer clickIndicator;

        private Vector2 startingMousePosition;

        private BaseCommand activeCommand;
        private GameObject ghostInstance;
        private MeshRenderer ghostRenderer;
        private bool wasMouseDownOnUI;
        private CinemachineFollow cinemachineFollow;
        private float zoomStartTime;
        private float rotationStartTime;
        private Vector3 startingFollowOffset;
        private float maxRotationAmount;
        private HashSet<AbstractUnit> aliveUnits = new(100);
        private HashSet<AbstractUnit> addedUnits = new(24);
        private List<ISelectable> selectedUnits = new(12);

        private const int MaxSelectedUnits = 12;

        private static readonly int TINT = Shader.PropertyToID("_Tint");
        private static readonly int FRESNEL = Shader.PropertyToID("_FresnelColor");
        private static readonly int CLICK_TIME = Shader.PropertyToID("_ClickTime");

        [SerializeField] private float maxBuildDistanceFromWorker = 12f;
        [SerializeField] private BuildRadiusIndicator buildRadiusIndicator;

        [SerializeField] private TextMeshProUGUI buildErrorText;
        [SerializeField] private float buildErrorDuration = 1.2f;

        [SerializeField] private Vector2 buildErrorCursorOffset = new Vector2(800, 20f);

        private Coroutine buildErrorCoroutine;
        private bool isInOverrideCommandMenu;
        private bool IsInCommandMode => activeCommand != null || isInOverrideCommandMenu || ghostInstance != null;

        private void Awake()
        {
            if (!cinemachineCamera.TryGetComponent(out cinemachineFollow))
            {
                Debug.LogError("Cinemachine Camera did not have CinemachineFollow. Zoom functionality will not work!");
            }

            startingFollowOffset = cinemachineFollow.FollowOffset;
            maxRotationAmount = Mathf.Abs(cinemachineFollow.FollowOffset.z);
            
            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] += HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] += HandleUnitDeselected;
            Bus<UnitSpawnEvent>.OnEvent[Owner.Player1] += HandleUnitSpawn;
            Bus<CommandSelectedEvent>.OnEvent[Owner.Player1] += HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] += HandleUnitDeath;
            Bus<MinimapClickEvent>.OnEvent[Owner.Player1] += HandleMinimapClick;
        }

        private void OnDestroy()
        {
            Bus<UnitSelectedEvent>.OnEvent[Owner.Player1] -= HandleUnitSelected;
            Bus<UnitDeselectedEvent>.OnEvent[Owner.Player1] -= HandleUnitDeselected;
            Bus<UnitSpawnEvent>.OnEvent[Owner.Player1] -= HandleUnitSpawn;
            Bus<CommandSelectedEvent>.OnEvent[Owner.Player1] -= HandleActionSelected;
            Bus<UnitDeathEvent>.OnEvent[Owner.Player1] -= HandleUnitDeath;
            Bus<MinimapClickEvent>.OnEvent[Owner.Player1] -= HandleMinimapClick;
        }

        private void HandleMinimapClick(MinimapClickEvent evt)
        {
            if (evt.Button == MouseButton.Right)
            {
                IssueRightClickCommand(evt.Hit);
            }
            else if (evt.Button == MouseButton.Left)
            {
                ActivateAction(evt.Hit);
            }
        }

        private void HandleUnitSelected(UnitSelectedEvent evt) 
        {
            if (!selectedUnits.Contains(evt.Unit))
            {
                selectedUnits.Add(evt.Unit);
            }
        }

        private void HandleUnitDeselected(UnitDeselectedEvent evt) => selectedUnits.Remove(evt.Unit);
        private void HandleUnitSpawn(UnitSpawnEvent evt) => aliveUnits.Add(evt.Unit);
        private void HandleUnitDeath(UnitDeathEvent evt)
        {
            aliveUnits.Remove(evt.Unit);
            selectedUnits.Remove(evt.Unit);
        }

        private void HandleActionSelected(CommandSelectedEvent evt)
        {
            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
                ghostRenderer = null;
            }

            activeCommand = evt.Command;

            if (activeCommand is OverrideCommandsCommand)
            {
                isInOverrideCommandMenu = true;
            }

            Worker worker = GetSelectedWorker();

            if (worker != null && (activeCommand is OverrideCommandsCommand || activeCommand.GhostPrefab != null))
            {
                buildRadiusIndicator.Show(worker, maxBuildDistanceFromWorker);
            }
            else
            {
                buildRadiusIndicator.Hide();
            }

            if (!activeCommand.RequiresClickToActivate)
            {
                ActivateAction(new RaycastHit());
            }
            else if (activeCommand.GhostPrefab != null)
            {
                isInOverrideCommandMenu = false;
                ghostInstance = Instantiate(activeCommand.GhostPrefab);
                ghostRenderer = ghostInstance.GetComponentInChildren<MeshRenderer>();
            }
            else
            {
                isInOverrideCommandMenu = false;
            }
        }

        private void Update()
        {
            HandlePanning();
            HandleZooming();
            HandleRotation();
            HandleGhost();
            HandleRightClick();
            HandleDragSelect();
            UpdateBuildErrorPosition();
        }

        private void HandleGhost()
        {
            if (ghostInstance == null) return;

            if (Keyboard.current.escapeKey.wasReleasedThisFrame)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
                ghostRenderer = null;
                activeCommand = null;
                isInOverrideCommandMenu = false;
                buildRadiusIndicator.Hide();
                return;
            }

            Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, floorLayers))
            {
                Vector3 clampedPoint = ClampPointToWorkerBuildRadius(hit.point);

                ghostInstance.SetActive(true);
                ghostInstance.transform.position = clampedPoint;

                CommandContext previewContext = new(null, hit, 0);
                bool allRestrictionsPass = activeCommand.AllRestrictionsPass(previewContext, clampedPoint);

                ghostRenderer.material.SetColor(
                    TINT,
                    allRestrictionsPass ? availableToPlaceTintColor : errorTintColor
                );

                ghostRenderer.material.SetColor(
                    FRESNEL,
                    allRestrictionsPass ? availableToPlaceFresnelColor : errorFresnelColor
                );
            }
        }

        private void HandleDragSelect()
        {
            if (selectionBox == null) { return; }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandleMouseDown();
            }
            else if (Mouse.current.leftButton.isPressed && !Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandleMouseDrag();
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                HandleMouseUp();
            }
        }

        private void HandleMouseUp()
        {
            if (camera == null)
            {
                selectionBox.gameObject.SetActive(false);
                return;
            }

            Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            bool shouldSelectUnitInsteadOfIssuingCommand = ghostInstance != null || isInOverrideCommandMenu;

            if (!wasMouseDownOnUI
                && shouldSelectUnitInsteadOfIssuingCommand
                && Physics.Raycast(cameraRay, out RaycastHit selectableHit, float.MaxValue, selectableUnitsLayers)
                && selectableHit.collider.TryGetComponent(out ISelectable selectableUnit))
            {
                CancelActiveCommandMode();

                if (!Keyboard.current.shiftKey.isPressed)
                {
                    DeselectAllUnits();
                }

                selectableUnit.Select();
                selectionBox.gameObject.SetActive(false);
                return;
            }

            if (!wasMouseDownOnUI && !IsInCommandMode && !Keyboard.current.shiftKey.isPressed)
            {
                DeselectAllUnits();
            }

            HandleLeftClick();

            List<AbstractUnit> unitsToSelect = GetRandomLimitedSelection(addedUnits, MaxSelectedUnits);

            foreach (AbstractUnit unit in unitsToSelect)
            {
                unit.Select();
            }

            selectionBox.gameObject.SetActive(false);
        }

        private void HandleMouseDrag()
        {
            if (activeCommand != null || wasMouseDownOnUI) return;

            Bounds selectionBoxBounds = ResizeSelectionBox();
            foreach (AbstractUnit unit in aliveUnits.Where(aliveUnits => aliveUnits.gameObject.activeInHierarchy))
            {
                Vector2 unitPosition = camera.WorldToScreenPoint(unit.transform.position);

                if (selectionBoxBounds.Contains(unitPosition))
                {
                    addedUnits.Add(unit);
                }
            }

        }

        private void HandleMouseDown()
        {
            selectionBox.sizeDelta = Vector2.zero;
            selectionBox.gameObject.SetActive(true);
            startingMousePosition = Mouse.current.position.ReadValue();
            addedUnits.Clear();
            wasMouseDownOnUI = EventSystem.current.IsPointerOverGameObject();
        }

        private void DeselectAllUnits()
        {
            ISelectable[] currentlySelectedUnits = selectedUnits.ToArray();
            foreach(ISelectable selectable in currentlySelectedUnits)
            {
                selectable.Deselect();
            }
        }

        private Bounds ResizeSelectionBox()
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            float width = mousePosition.x - startingMousePosition.x;
            float height = mousePosition.y - startingMousePosition.y;

            selectionBox.anchoredPosition = startingMousePosition + new Vector2(width / 2, height / 2);
            selectionBox.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));

            return new Bounds(selectionBox.anchoredPosition, selectionBox.sizeDelta);
        }

        private void HandleRightClick()
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (!Mouse.current.rightButton.wasReleasedThisFrame)
            {
                return;
            }

            if (ghostInstance != null)
            {
                ShowBuildError("you cant give move command while you are in the build menu");
                return;
            }

            if (isInOverrideCommandMenu)
            {
                ShowBuildError("Close the build menu first");
                return;
            }

            if (selectedUnits.Count == 0)
            {
                return;
            }

            Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, interactableLayers | floorLayers))
            {
                IssueRightClickCommand(hit);
            }
        }

        private List<AbstractUnit> GetRandomLimitedSelection(HashSet<AbstractUnit> units, int maxCount)
        {
            if (units == null || units.Count == 0)
                return new List<AbstractUnit>();

            List<AbstractUnit> unitList = units.ToList();

            if (unitList.Count <= maxCount)
                return unitList;

            for (int i = 0; i < unitList.Count; i++)
            {
                int randomIndex = Random.Range(i, unitList.Count);
                (unitList[i], unitList[randomIndex]) = (unitList[randomIndex], unitList[i]);
            }

            return unitList.Take(maxCount).ToList();
        }

        private void IssueRightClickCommand(RaycastHit hit)
        {
            List<AbstractUnit> abstractUnits = new(selectedUnits.Count);
            foreach (ISelectable selectable in selectedUnits)
            {
                if (selectable is AbstractUnit unit)
                {
                    abstractUnits.Add(unit);
                }
            }

            for (int i = 0; i < abstractUnits.Count; i++)
            {
                CommandContext context = new(abstractUnits[i], hit, i, MouseButton.Right);

                foreach (ICommand command in GetAvailableCommands(abstractUnits[i]))
                {
                    if (command.CanHandle(context))
                    {
                        command.Handle(context);
                        if (command.IsSingleUnitCommand)
                        {
                            return;
                        }
                        break;
                    }
                }
            }

            ShowClick(hit.point);
        }

        private void ShowClick(Vector3 position)
        {
            clickIndicator.transform.position = position;
            clickIndicator.material.SetFloat(CLICK_TIME, Time.time);
        }

        private List<BaseCommand> GetAvailableCommands(AbstractUnit unit)
        {
            OverrideCommandsCommand[] overrideCommandsCommands = unit.AvailableCommands
                .Where(command => command is OverrideCommandsCommand)
                .Cast<OverrideCommandsCommand>()
                .ToArray();

            List<BaseCommand> allAvailableCommands = new();
            foreach(OverrideCommandsCommand overrideCommand in overrideCommandsCommands)
            {
                allAvailableCommands.AddRange(overrideCommand.Commands
                    .Where(command => command is not OverrideCommandsCommand)
                );
            }

            allAvailableCommands.AddRange(unit.AvailableCommands
                .Where(command => command is not OverrideCommandsCommand)
            );

            return allAvailableCommands;
        }


        private void HandleLeftClick()
        {
            if (camera == null) return;

            Ray cameraRay = camera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (activeCommand == null
                && !isInOverrideCommandMenu
                && ghostInstance == null
                && addedUnits.Count == 0
                && Physics.Raycast(cameraRay, out RaycastHit hit, float.MaxValue, selectableUnitsLayers)
                && hit.collider.TryGetComponent(out ISelectable selectable))
            {
                selectable.Select();
            }
            else if (activeCommand != null
                && !EventSystem.current.IsPointerOverGameObject()
                && Physics.Raycast(cameraRay, out hit, float.MaxValue, interactableLayers | floorLayers))
            {
                ActivateAction(hit);
                ShowClick(hit.point);
            }
        }

        private void CancelActiveCommandMode(bool hideBuildRadius = true)
        {
            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
                ghostRenderer = null;
            }

            activeCommand = null;
            isInOverrideCommandMenu = false;

            if (hideBuildRadius)
            {
                buildRadiusIndicator.Hide();
            }
        }

        private void ActivateAction(RaycastHit hit)
        {
            var issuedCommand = activeCommand;
            if (issuedCommand == null)
                return;

            bool isBuildPlacement = ghostInstance != null;
            Vector3 targetPoint = hit.point;

            if (isBuildPlacement)
            {
                targetPoint = ClampPointToWorkerBuildRadius(hit.point);
                hit.point = targetPoint;

                CommandContext previewContext = new(null, hit, 0);

                if (!activeCommand.AllRestrictionsPass(previewContext, targetPoint))
                {
                    ShowBuildError("Cannot build here");
                    return;
                }
            }
            
            Vector3 clampedPoint = hit.point;

            if (ghostInstance != null)
            {
                clampedPoint = ClampPointToWorkerBuildRadius(hit.point);
                hit.point = clampedPoint;
            }

            if (ghostInstance != null)
            {
                Destroy(ghostInstance);
                ghostInstance = null;
                ghostRenderer = null;
            }

            List<AbstractCommandable> abstractCommandables = selectedUnits
                .Where(unit => unit is AbstractCommandable)
                .Cast<AbstractCommandable>()
                .ToList();

            for (int i = 0; i < abstractCommandables.Count; i++)
            {
                CommandContext context = new(abstractCommandables[i], hit, i);

                if (activeCommand.CanHandle(context))
                {
                    activeCommand.Handle(context);
                    if (activeCommand.IsSingleUnitCommand)
                    {
                        break;
                    }
                }
            }
            
            Bus<CommandIssuedEvent>.Raise(Owner.Player1, new CommandIssuedEvent(activeCommand));

            bool keepOverrideMenuOpen = issuedCommand is OverrideCommandsCommand;

            if (!keepOverrideMenuOpen)
            {
                buildRadiusIndicator.Hide();
            }

            activeCommand = null;

            if (!keepOverrideMenuOpen)
            {
                isInOverrideCommandMenu = false;
            }
        }

        private void HandleRotation()
        {
            if (ShouldSetRotationStartTime())
            {
                rotationStartTime = Time.time;
            }

            float rotationTime = Mathf.Clamp01((Time.time - rotationStartTime) * cameraConfig.RotationSpeed);

            Vector3 targetFollowOffset;

            if (Keyboard.current.pageDownKey.isPressed)
            {
                targetFollowOffset = new Vector3(
                    maxRotationAmount,
                    cinemachineFollow.FollowOffset.y,
                    0
                );
            }
            else if (Keyboard.current.pageUpKey.isPressed)
            {
                targetFollowOffset = new Vector3(
                    -maxRotationAmount,
                    cinemachineFollow.FollowOffset.y,
                    0
                );
            }
            else
            {
                targetFollowOffset = new Vector3(
                    startingFollowOffset.x,
                    cinemachineFollow.FollowOffset.y,
                    startingFollowOffset.z
                );
            }

            cinemachineFollow.FollowOffset = Vector3.Slerp(
                cinemachineFollow.FollowOffset,
                targetFollowOffset,
                rotationTime
            );
        }

        private bool ShouldSetRotationStartTime()
        {
            return Keyboard.current.pageUpKey.wasPressedThisFrame
                || Keyboard.current.pageDownKey.wasPressedThisFrame
                || Keyboard.current.pageUpKey.wasReleasedThisFrame
                || Keyboard.current.pageDownKey.wasReleasedThisFrame;
        }

        private void HandleZooming()
        {
            if (ShouldSetZoomStartTime())
            {
                zoomStartTime = Time.time;
            }

            float zoomTime = Mathf.Clamp01((Time.time - zoomStartTime) * cameraConfig.ZoomSpeed);
            Vector3 targetFollowOffset;

            if (Keyboard.current.endKey.isPressed)
            {
                targetFollowOffset = new Vector3(
                    cinemachineFollow.FollowOffset.x,
                    cameraConfig.MinZoomDistance,
                    cinemachineFollow.FollowOffset.z
                );
            }
            else
            {
                targetFollowOffset = new Vector3(
                    cinemachineFollow.FollowOffset.x,
                    startingFollowOffset.y,
                    cinemachineFollow.FollowOffset.z
                );
            }

            cinemachineFollow.FollowOffset = Vector3.Slerp(
                cinemachineFollow.FollowOffset,
                targetFollowOffset,
                zoomTime
            );
        }

        private bool ShouldSetZoomStartTime()
        {
            return Keyboard.current.endKey.wasPressedThisFrame
                || Keyboard.current.endKey.wasReleasedThisFrame;
        }

        private void HandlePanning()
        {
            Vector2 moveAmount = GetKeyboardMoveAmount();
            moveAmount += GetMouseMoveAmount();

            cameraTarget.linearVelocity = new Vector3(moveAmount.x, 0, moveAmount.y);
        }

        private Vector2 GetMouseMoveAmount()
        {
            Vector2 moveAmount = Vector2.zero;

            if (!cameraConfig.EnableEdgePan) { return moveAmount; }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;

            if (mousePosition.x <= cameraConfig.EdgePanSize)
            {
                moveAmount.x -= cameraConfig.MousePanSpeed;
            }
            else if (mousePosition.x >= screenWidth - cameraConfig.EdgePanSize)
            {
                moveAmount.x += cameraConfig.MousePanSpeed;
            }

            if (mousePosition.y >= screenHeight - cameraConfig.EdgePanSize)
            {
                moveAmount.y += cameraConfig.MousePanSpeed;
            }
            else if (mousePosition.y <= cameraConfig.EdgePanSize)
            {
                moveAmount.y -= cameraConfig.MousePanSpeed;
            }

            return moveAmount;
        }

        private Vector2 GetKeyboardMoveAmount()
        {
            Vector2 moveAmount = Vector2.zero;

            if (Keyboard.current.upArrowKey.isPressed)
            {
                moveAmount.y += cameraConfig.KeyboardPanSpeed;
            }
            if (Keyboard.current.leftArrowKey.isPressed)
            {
                moveAmount.x -= cameraConfig.KeyboardPanSpeed;
            }
            if (Keyboard.current.downArrowKey.isPressed)
            {
                moveAmount.y -= cameraConfig.KeyboardPanSpeed;
            }
            if (Keyboard.current.rightArrowKey.isPressed)
            {
                moveAmount.x += cameraConfig.KeyboardPanSpeed;
            }

            return moveAmount;
        }

        private bool IsWithinBuildDistance(Vector3 targetPoint)
        {
            if (selectedUnits == null || selectedUnits.Count == 0)
                return false;

            foreach (ISelectable selectable in selectedUnits)
            {
                if (selectable is Worker worker)
                {
                    float distance = Vector3.Distance(worker.transform.position, targetPoint);
                    if (distance <= maxBuildDistanceFromWorker)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Worker GetSelectedWorker()
        {
            foreach (ISelectable selectable in selectedUnits)
            {
                if (selectable is Worker worker)
                {
                    return worker;
                }
            }

            return null;
        }

        private Vector3 ClampPointToWorkerBuildRadius(Vector3 targetPoint)
        {
            Worker worker = GetSelectedWorker();
            if (worker == null)
                return targetPoint;

            Vector3 workerPosition = worker.transform.position;
            Vector3 flatDirection = targetPoint - workerPosition;
            flatDirection.y = 0f;

            float distance = flatDirection.magnitude;

            if (distance <= maxBuildDistanceFromWorker)
                return targetPoint;

            Vector3 clampedDirection = flatDirection.normalized * maxBuildDistanceFromWorker;

            return new Vector3(
                workerPosition.x + clampedDirection.x,
                targetPoint.y,
                workerPosition.z + clampedDirection.z
            );
        }

        private void ShowBuildError(string message)
        {
            if (buildErrorText == null) return;

            if (buildErrorCoroutine != null)
            {
                StopCoroutine(buildErrorCoroutine);
            }

            buildErrorCoroutine = StartCoroutine(ShowBuildErrorRoutine(message));
        }

        private System.Collections.IEnumerator ShowBuildErrorRoutine(string message)
        {
            buildErrorText.text = message;
            buildErrorText.gameObject.SetActive(true);

            yield return new WaitForSeconds(buildErrorDuration);

            buildErrorText.gameObject.SetActive(false);
            buildErrorCoroutine = null;
        }

        private void UpdateBuildErrorPosition()
        {
            if (buildErrorText == null) return;
            if (!buildErrorText.gameObject.activeSelf) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();

            buildErrorText.rectTransform.position = mousePos + buildErrorCursorOffset;
        }
    }
}

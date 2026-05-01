using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class PSVitaControllerMouse : MonoBehaviour
{
    [Header("=== PSVita Controller Mouse v10.0 ===")]
    [Tooltip("Fallback sensitivity if Settings singleton is not found")]
    public float sensitivity = 650f;
    public float deadzone = 0.12f;

    [Header("Input Axes")]
    public string horizontalAxis = "Horizontal";
    public string verticalAxis = "Vertical";
    public string clickButton = "Fire1";

    [Header("Options")]
    public bool invertY = false;
    public bool hideHardwareCursor = true;

    public static bool IsCursorActive { get; private set; }
    
    // Expose position so Inventory.cs can use it for draggingImage
    public static Vector2 CursorScreenPosition { get; private set; }

    private RectTransform rectTransform;
    private EventSystem eventSystem;
    private PointerEventData pointerData;
    private List<RaycastResult> raycastResults = new List<RaycastResult>(32);

    private Vector2 screenPos;
    private Vector2 lastHoverPosition;

    private bool isClicking = false;
    private GameObject currentHovered;
    private GameObject clickDownTarget; // slot we pressed down on
    
    public static PSVitaControllerMouse Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();
        eventSystem = EventSystem.current ?? FindObjectOfType<EventSystem>();
        pointerData = new PointerEventData(eventSystem);

        if (hideHardwareCursor)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.None;
        }

        screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        lastHoverPosition = screenPos;
    }

    private void OnEnable()
    {
        IsCursorActive = true;
        Helper.SetCursorVisibleAndLockState(false, CursorLockMode.None);
    }

    private void OnDisable()
    {
        IsCursorActive = false;
    }

    private void Update()
    {
        HandleAnalogMovement();
        CursorScreenPosition = screenPos;
        rectTransform.position = new Vector3(screenPos.x, screenPos.y, 0f);

        HandleHover();
        HandleClick();
    }

    private void HandleAnalogMovement()
    {
        float h = Input.GetAxisRaw(horizontalAxis);
        float v = Input.GetAxisRaw(verticalAxis);

        if (Mathf.Abs(h) > deadzone || Mathf.Abs(v) > deadzone)
        {
            // Use Settings sensitivity if available, otherwise fall back to inspector value
            float currentSensitivity = (SingletonGeneric<Settings>.Singleton != null)
                ? SingletonGeneric<Settings>.Singleton.CursorSensitivity
                : sensitivity;

            screenPos.x += h * currentSensitivity * Time.deltaTime;
            float yMovement = invertY ? -v : v;
            screenPos.y += yMovement * currentSensitivity * Time.deltaTime;
            screenPos.x = Mathf.Clamp(screenPos.x, 0f, Screen.width);
            screenPos.y = Mathf.Clamp(screenPos.y, 0f, Screen.height);
        }
    }

    private GameObject GetObjectUnderCursor()
    {
        pointerData.position = screenPos;
        raycastResults.Clear();
        eventSystem.RaycastAll(pointerData, raycastResults);
        return raycastResults.Count > 0 ? raycastResults[0].gameObject : null;
    }

    private void HandleHover()
    {
        // Only re-raycast if cursor moved enough
        if (Vector2.Distance(screenPos, lastHoverPosition) < 0.5f) return;
        lastHoverPosition = screenPos;

        GameObject newHovered = GetObjectUnderCursor();

        if (newHovered != currentHovered)
        {
            if (currentHovered != null)
            {
                ExecuteEvents.Execute(currentHovered, pointerData, ExecuteEvents.pointerExitHandler);
                eventSystem.SetSelectedGameObject(null);
            }

            if (newHovered != null)
            {
                ExecuteEvents.Execute(newHovered, pointerData, ExecuteEvents.pointerEnterHandler);
                eventSystem.SetSelectedGameObject(newHovered);
            }

            currentHovered = newHovered;
        }

        pointerData.pointerEnter = currentHovered;
    }

    private void HandleClick()
    {
        if (Input.GetButtonDown(clickButton))
        {
            // Only fire PointerDown, store target
            clickDownTarget = GetObjectUnderCursor();
            if (clickDownTarget != null)
            {
                isClicking = true;
                pointerData.position = screenPos;
                pointerData.pressPosition = screenPos;
                pointerData.pointerPressRaycast = raycastResults.Count > 0 
                    ? raycastResults[0] : new RaycastResult();

                ExecuteEvents.ExecuteHierarchy(
                    clickDownTarget, pointerData, ExecuteEvents.pointerDownHandler);
            }
        }

        if (Input.GetButtonUp(clickButton) && isClicking)
        {
            isClicking = false;

            // Fire PointerUp on whatever is under cursor NOW (could be different slot = valid move)
            pointerData.position = screenPos;
            GameObject upTarget = GetObjectUnderCursor();

            if (upTarget != null)
            {
                ExecuteEvents.ExecuteHierarchy(
                    upTarget, pointerData, ExecuteEvents.pointerUpHandler);
            }
            else if (clickDownTarget != null)
            {
                // Released over empty space - fire on original target so MoveItem can handle drop
                ExecuteEvents.ExecuteHierarchy(
                    clickDownTarget, pointerData, ExecuteEvents.pointerUpHandler);
            }

            clickDownTarget = null;
        }
    }

    public void ShowCursor() { gameObject.SetActive(true); }
    public void HideCursor() { gameObject.SetActive(false); }
    public void ResetToCenter() { screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f); }
}
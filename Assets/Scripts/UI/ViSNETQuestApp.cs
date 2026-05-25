using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ViSNET
{
    public sealed class ViSNETQuestApp : MonoBehaviour
    {
        private const string PrimaryButtonResource = "MetaUISet/Button/PrimaryButton_IconAndLabel_UnityUIButton";
        private const string SecondaryButtonResource = "MetaUISet/Button/SecondaryButton_IconAndLabel_UnityUIButton";
        private const string BorderlessButtonResource = "MetaUISet/Button/BorderlessButton_IconAndLabel_UnityUIButton";
        private const string InputResource = "MetaUISet/TextInputField/TextInputField";

        [SerializeField] private Vector2 canvasSize = new Vector2(1800f, 1050f);
        [SerializeField] private Vector3 viewOffset = new Vector3(0f, -0.02f, 1.05f);
        [SerializeField] private float worldCanvasScale = 0.00082f;

        private Vector2 contentSize;

        private ViSNETApiClient api;
        private GameObject primaryButtonPrefab;
        private GameObject secondaryButtonPrefab;
        private GameObject borderlessButtonPrefab;
        private GameObject inputFieldPrefab;

        private RectTransform contentRoot;
        private GameObject loginPanel;
        private GameObject projectPanel;
        private GameObject floorPanel;
        private TMP_InputField usernameInput;
        private TMP_InputField passwordInput;
        private Button loginButton;
        private RectTransform projectListRoot;
        private RectTransform floorListRoot;
        private Button floorDropdownButton;
        private TMP_Text floorDropdownLabel;
        private TMP_Text floorSubtitle;
        private RectTransform selectionSummaryRoot;
        private TMP_Text summaryProjectText;
        private TMP_Text summaryFloorText;
        private Button passwordVisibilityButton;
        private TMP_Text passwordVisibilityLabel;
        private TMP_Text toastText;
        private CanvasGroup toastGroup;
        private Coroutine toastRoutine;

        private string token;
        private string selectedProjectName;
        private int selectedProjectId;
        private string selectedFloorName = "";
        private bool floorsExpanded;
        private bool passwordVisible;
        private GameObject selectedFloorButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<ViSNETQuestApp>() != null)
            {
                return;
            }

            new GameObject("ViSNET Quest App").AddComponent<ViSNETQuestApp>();
        }

        private void Awake()
        {
            api = gameObject.AddComponent<ViSNETApiClient>();
            LoadTemplates();
            EnsureEventSystem();
            BuildWorldUi();
        }

        private void Start()
        {
            ShowLogin();
        }

        private void LoadTemplates()
        {
            primaryButtonPrefab = Resources.Load<GameObject>(PrimaryButtonResource);
            secondaryButtonPrefab = Resources.Load<GameObject>(SecondaryButtonResource);
            borderlessButtonPrefab = Resources.Load<GameObject>(BorderlessButtonResource);
            inputFieldPrefab = Resources.Load<GameObject>(InputResource);

            if (primaryButtonPrefab == null || secondaryButtonPrefab == null ||
                borderlessButtonPrefab == null || inputFieldPrefab == null)
            {
                Debug.LogError("ViSNET UI resources are missing. Reimport Assets/Resources/MetaUISet.");
            }
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
            }

            foreach (BaseInputModule inputModule in eventSystem.GetComponents<BaseInputModule>())
            {
                if (inputModule.GetType() == typeof(StandaloneInputModule))
                {
                    inputModule.enabled = false;
                    Destroy(inputModule);
                }
            }

            Type inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null && eventSystem.GetComponent(inputSystemModule) == null)
            {
                eventSystem.gameObject.AddComponent(inputSystemModule);
            }

            PointableCanvasModule pointableCanvasModule = eventSystem.GetComponent<PointableCanvasModule>();
            if (pointableCanvasModule == null)
            {
                pointableCanvasModule = eventSystem.gameObject.AddComponent<PointableCanvasModule>();
            }

            pointableCanvasModule.ExclusiveMode = true;
        }

        private void BuildWorldUi()
        {
            DestroyExistingGeneratedUi();

            GameObject shell = new GameObject("ViSNET Horizon OS UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            shell.name = "ViSNET Horizon OS UI";

            Camera uiCamera = FindBestCamera();
            PlaceInFrontOfCamera(shell.transform, uiCamera);

            Canvas canvas = shell.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = uiCamera;
            canvas.sortingOrder = 50;

            GraphicRaycaster raycaster = shell.GetComponent<GraphicRaycaster>();
            raycaster.ignoreReversedGraphics = true;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Center(canvasRect, canvasSize);
            canvasRect.sizeDelta = canvasSize;
            ConfigureMetaRayCanvas(canvas, canvasRect);

            RectTransform backplate = CreateRect("Backplate", canvasRect);
            Center(backplate, canvasSize);
            Image backplateImage = backplate.gameObject.AddComponent<Image>();
            backplateImage.color = new Color(0.055f, 0.058f, 0.058f, 0.94f);
            backplateImage.raycastTarget = true;
            TryAddRoundedCorners(backplate.gameObject, 44f);

            contentSize = new Vector2(canvasSize.x - 180f, canvasSize.y - 150f);
            contentRoot = CreateRect("Content", canvasRect);
            Center(contentRoot, contentSize);
            contentRoot.SetAsLastSibling();

            loginPanel = BuildLoginPanel();
            projectPanel = BuildProjectPanel();
            floorPanel = BuildFloorPanel();
            BuildToast(canvasRect);
        }

        private void ConfigureMetaRayCanvas(Canvas canvas, RectTransform canvasRect)
        {
            PointableCanvas pointableCanvas = canvas.GetComponent<PointableCanvas>() ??
                canvas.gameObject.AddComponent<PointableCanvas>();
            pointableCanvas.InjectAllPointableCanvas(canvas);

            PlaneSurface planeSurface = canvas.GetComponent<PlaneSurface>() ??
                canvas.gameObject.AddComponent<PlaneSurface>();
            planeSurface.Facing = PlaneSurface.NormalFacing.Backward;
            planeSurface.DoubleSided = true;

            BoundsClipper boundsClipper = canvas.GetComponent<BoundsClipper>() ??
                canvas.gameObject.AddComponent<BoundsClipper>();
            Vector2 pointerSurfaceSize = canvasRect.rect.size;
            if (pointerSurfaceSize.x <= 0f || pointerSurfaceSize.y <= 0f)
            {
                pointerSurfaceSize = canvasSize;
            }

            boundsClipper.Position = Vector3.zero;
            boundsClipper.Size = new Vector3(pointerSurfaceSize.x, pointerSurfaceSize.y, 0.01f);

            RectTransformBoundsClipperDriver clipperDriver = canvas.GetComponent<RectTransformBoundsClipperDriver>() ??
                canvas.gameObject.AddComponent<RectTransformBoundsClipperDriver>();
            SetPrivateField(clipperDriver, "_boundsClipper", boundsClipper);

            ClippedPlaneSurface clippedSurface = canvas.GetComponent<ClippedPlaneSurface>() ??
                canvas.gameObject.AddComponent<ClippedPlaneSurface>();
            List<UnityEngine.Object> serializedClippers = new List<UnityEngine.Object> { boundsClipper };
            List<IBoundsClipper> runtimeClippers = new List<IBoundsClipper> { boundsClipper };
            SetPrivateField(clippedSurface, "_planeSurface", planeSurface);
            SetPrivateField(clippedSurface, "_clippers", serializedClippers);
            SetPrivateProperty(clippedSurface, "Clippers", runtimeClippers);

            RayInteractable rayInteractable = canvas.GetComponent<RayInteractable>() ??
                canvas.gameObject.AddComponent<RayInteractable>();
            rayInteractable.InjectAllRayInteractable(clippedSurface);
            rayInteractable.InjectOptionalSelectSurface(clippedSurface);
            rayInteractable.InjectOptionalPointableElement(pointableCanvas);

            Debug.Log("ViSNET UI configured for Meta Interaction SDK ray pointers.");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private static void SetPrivateProperty(object target, string propertyName, object value)
        {
            target.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private void DestroyExistingGeneratedUi()
        {
            foreach (Transform sceneTransform in FindObjectsOfType<Transform>(true))
            {
                if (sceneTransform.name == "ViSNET Horizon OS UI")
                {
                    Destroy(sceneTransform.gameObject);
                }
            }
        }

        private Camera FindBestCamera()
        {
            Camera[] cameras = FindObjectsOfType<Camera>(true);
            foreach (Camera camera in cameras)
            {
                if (camera.isActiveAndEnabled && camera.name == "CenterEyeAnchor")
                {
                    return camera;
                }
            }

            foreach (Camera camera in cameras)
            {
                if (camera.isActiveAndEnabled && camera.CompareTag("MainCamera") && camera.name != "Main Camera")
                {
                    return camera;
                }
            }

            return Camera.main;
        }

        private void PlaceInFrontOfCamera(Transform target, Camera camera)
        {
            if (camera == null)
            {
                target.SetPositionAndRotation(new Vector3(0f, 1.45f, 1.15f), Quaternion.identity);
                target.localScale = Vector3.one * worldCanvasScale;
                return;
            }

            target.SetParent(camera.transform, false);
            target.localPosition = viewOffset;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one * worldCanvasScale;
            Debug.Log($"ViSNET UI attached to {camera.name} at local offset {viewOffset}.");
        }

        private static void TryAddRoundedCorners(GameObject target, float radius)
        {
            Type roundedBoxType = Type.GetType("RoundedBoxUIProperties, Oculus.Interaction.Samples");
            if (roundedBoxType == null)
            {
                return;
            }

            Component roundedBox = target.AddComponent(roundedBoxType);
            roundedBoxType.GetField("borderRadius")?.SetValue(
                roundedBox,
                new Vector4(radius, radius, radius, radius));
        }

        private GameObject BuildLoginPanel()
        {
            GameObject panel = CreatePanel("Login Panel");
            Transform root = panel.transform;

            CreateTextAt(root, "Welcome", "Welcome to", new Vector2(0f, 250f), new Vector2(760f, 42f),
                32, new Color(0.88f, 0.91f, 0.94f), TextAlignmentOptions.Center, FontStyles.Bold);
            CreateTextAt(root, "Title", "API Testing", new Vector2(0f, 182f), new Vector2(820f, 78f),
                56, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            CreateTextAt(root, "Subtitle", "Use the assigned web credentials to continue.", new Vector2(0f, 100f), new Vector2(900f, 42f),
                27, new Color(0.84f, 0.88f, 0.92f), TextAlignmentOptions.Center);

            usernameInput = CreateInputAt(root, "Username", "Username", new Vector2(0f, 14f), 760f, 68f, false);
            usernameInput.text = "testuser";
            passwordInput = CreateInputAt(root, "Password", "Password", new Vector2(0f, -84f), 760f, 68f, true);
            passwordInput.text = "123456";

            loginButton = CreateButtonAt(root, "Login", "Login", new Vector2(0f, -188f), new Vector2(280f, 64f),
                OnLoginPressed, true, TextAlignmentOptions.Center);
            CreateTextAt(root, "Credentials", "Mock credentials: testuser / 123456", new Vector2(0f, -320f), new Vector2(860f, 38f),
                24, new Color(0.78f, 0.82f, 0.86f), TextAlignmentOptions.Center, FontStyles.Bold);
            return panel;
        }

        private GameObject BuildProjectPanel()
        {
            GameObject panel = CreatePanel("Project Panel");
            Transform root = panel.transform;
            CreateHeaderAt(root, "Active Inspections", "04 inspections due", ShowLogin);
            projectListRoot = CreateRectAt("Project List", root, new Vector2(0f, -60f), new Vector2(1220f, 580f));
            return panel;
        }

        private GameObject BuildFloorPanel()
        {
            GameObject panel = CreatePanel("Floor Panel");
            Transform root = panel.transform;
            CreateHeaderAt(root, "Floor Selection", "Choose a project floor", ShowProjects, out floorSubtitle);

            floorDropdownButton = CreateButtonAt(root, "Floor Dropdown", "Select Floor v", new Vector2(0f, 230f),
                new Vector2(900f, 76f), ToggleFloorDropdown, false, TextAlignmentOptions.Left);
            floorDropdownLabel = FindPreferredText(floorDropdownButton.gameObject);

            floorListRoot = CreateRectAt("Floor Dropdown List", root, new Vector2(0f, 32f), new Vector2(900f, 320f));
            Image listBackground = floorListRoot.gameObject.AddComponent<Image>();
            listBackground.color = new Color(0.075f, 0.095f, 0.13f, 0.97f);
            TryAddRoundedCorners(floorListRoot.gameObject, 28f);
            floorListRoot.gameObject.SetActive(false);

            selectionSummaryRoot = CreateRectAt("Selection Summary", root, new Vector2(0f, -285f), new Vector2(900f, 190f));
            Image summaryBackground = selectionSummaryRoot.gameObject.AddComponent<Image>();
            summaryBackground.color = new Color(0.10f, 0.13f, 0.18f, 0.92f);
            TryAddRoundedCorners(selectionSummaryRoot.gameObject, 28f);
            CreateTextAt(selectionSummaryRoot, "Summary Title", "Selected Objects", new Vector2(0f, 58f), new Vector2(820f, 38f),
                26, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            summaryProjectText = CreateTextAt(selectionSummaryRoot, "Summary Project", "Project: -", new Vector2(0f, 12f),
                new Vector2(820f, 34f), 24, new Color(0.84f, 0.9f, 1f), TextAlignmentOptions.Left);
            summaryFloorText = CreateTextAt(selectionSummaryRoot, "Summary Floor", "Floor: -", new Vector2(0f, -34f),
                new Vector2(820f, 34f), 24, new Color(0.84f, 0.9f, 1f), TextAlignmentOptions.Left);
            return panel;
        }

        private GameObject CreatePanel(string name)
        {
            RectTransform panel = CreateRect(name, contentRoot);
            panel.gameObject.SetActive(false);
            Center(panel, contentSize);
            return panel.gameObject;
        }

        private GameObject CreatePanel(string name, TextAnchor alignment, RectOffset padding, float spacing)
        {
            return CreatePanel(name);
        }

        private void CreateHeaderAt(Transform parent, string title, string subtitle, UnityAction backAction)
        {
            CreateHeaderAt(parent, title, subtitle, backAction, out _);
        }

        private void CreateHeaderAt(Transform parent, string title, string subtitle, UnityAction backAction, out TMP_Text subtitleText)
        {
            CreateButtonAt(parent, "Back", "<", new Vector2(-710f, 360f), new Vector2(72f, 56f),
                backAction, false, TextAlignmentOptions.Center);
            CreateTextAt(parent, "Header Title", title, new Vector2(-240f, 372f), new Vector2(720f, 48f),
                34, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            subtitleText = CreateTextAt(parent, "Header Subtitle", subtitle, new Vector2(-240f, 326f), new Vector2(720f, 34f),
                22, new Color(0.76f, 0.82f, 0.9f), TextAlignmentOptions.Left);
        }

        private RectTransform CreateRectAt(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent);
            Center(rect, size);
            rect.anchoredPosition = position;
            return rect;
        }

        private TextMeshProUGUI CreateTextAt(Transform parent, string name, string value, Vector2 position, Vector2 size,
            int fontSize, Color color, TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
        {
            RectTransform rect = CreateRectAt(name, parent, position, size);
            return ConfigureText(rect.gameObject.AddComponent<TextMeshProUGUI>(), value, fontSize, color, alignment, style, true);
        }

        private Button CreateButtonAt(Transform parent, string name, string label, Vector2 position, Vector2 size,
            UnityAction action, bool primary, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRectAt(name + " Button", parent, position, size);
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = primary ? new Color(0.24f, 0.48f, 0.92f, 1f) : new Color(0.14f, 0.22f, 0.34f, 0.96f);
            TryAddRoundedCorners(rect.gameObject, size.y * 0.45f);

            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = background.color;
            colors.highlightedColor = primary ? new Color(0.34f, 0.58f, 1f, 1f) : new Color(0.22f, 0.34f, 0.52f, 1f);
            colors.pressedColor = primary ? new Color(0.15f, 0.34f, 0.72f, 1f) : new Color(0.09f, 0.16f, 0.26f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.2f, 0.22f, 0.26f, 0.55f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.targetGraphic = background;
            button.onClick.AddListener(action);

            RectTransform labelRect = CreateRectAt("Label", rect, Vector2.zero, new Vector2(size.x - 44f, size.y - 8f));
            if (alignment == TextAlignmentOptions.Left)
            {
                labelRect.anchoredPosition = new Vector2(18f, 0f);
            }

            TextMeshProUGUI labelText = ConfigureText(labelRect.gameObject.AddComponent<TextMeshProUGUI>(), label, 28,
                Color.white, alignment, FontStyles.Bold, false);
            labelText.margin = alignment == TextAlignmentOptions.Left ? new Vector4(20f, 0f, 20f, 0f) : Vector4.zero;
            return button;
        }

        private TMP_InputField CreateInputAt(Transform parent, string name, string placeholder, Vector2 position,
            float width, float height, bool isPassword)
        {
            RectTransform root = CreateRectAt(name + " Input", parent, position, new Vector2(width, height));
            Image background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0.14f, 0.20f, 0.30f, 0.98f);
            TryAddRoundedCorners(root.gameObject, height * 0.4f);

            TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.keyboardType = TouchScreenKeyboardType.Default;
            input.shouldHideMobileInput = false;
            input.shouldHideSoftKeyboard = false;
            input.onSelect.AddListener(_ => ActivateQuestKeyboard(input));

            RectTransform textViewport = CreateRectAt("Text Area", root, Vector2.zero, new Vector2(width - (isPassword ? 154f : 72f), height));
            textViewport.anchorMin = Vector2.zero;
            textViewport.anchorMax = Vector2.one;
            textViewport.offsetMin = new Vector2(34f, 0f);
            textViewport.offsetMax = new Vector2(isPassword ? -120f : -28f, 0f);
            textViewport.pivot = new Vector2(0.5f, 0.5f);
            input.textViewport = textViewport;

            RectTransform placeholderRect = CreateRectAt("Placeholder", textViewport, Vector2.zero, textViewport.sizeDelta);
            Fill(placeholderRect, Vector2.zero, Vector2.zero);
            TextMeshProUGUI placeholderText = ConfigureText(placeholderRect.gameObject.AddComponent<TextMeshProUGUI>(),
                placeholder, 27, new Color(0.62f, 0.70f, 0.82f), TextAlignmentOptions.MidlineLeft);

            RectTransform textRect = CreateRectAt("Text", textViewport, Vector2.zero, textViewport.sizeDelta);
            Fill(textRect, Vector2.zero, Vector2.zero);
            TextMeshProUGUI inputText = ConfigureText(textRect.gameObject.AddComponent<TextMeshProUGUI>(),
                "", 30, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, false);

            input.placeholder = placeholderText;
            input.textComponent = inputText;

            if (isPassword)
            {
                AddPasswordVisibilityToggle(input);
            }

            return input;
        }

        private void AddTitle(Transform parent, string eyebrow, string title)
        {
            CreateText("Eyebrow", parent, eyebrow, 32, new Color(0.9f, 0.92f, 0.9f), TextAlignmentOptions.Center, 48f);
            CreateText("Title", parent, title, 52, Color.white, TextAlignmentOptions.Center, 74f, FontStyles.Bold, false);
        }

        private void AddHeader(Transform parent, string title, string subtitle, UnityAction backAction)
        {
            AddHeader(parent, title, subtitle, backAction, out _);
        }

        private void AddHeader(Transform parent, string title, string subtitle, UnityAction backAction, out TMP_Text subtitleText)
        {
            RectTransform row = CreateRect("Header", parent);
            LayoutElement rowElement = row.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredWidth = contentSize.x - 280f;
            rowElement.preferredHeight = 92f;
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 28;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateButton(borderlessButtonPrefab, row, "Back", backAction, 150f, 64f);

            RectTransform textStack = CreateRect("Header Text", row);
            LayoutElement textStackElement = textStack.gameObject.AddComponent<LayoutElement>();
            textStackElement.preferredWidth = contentSize.x - 480f;
            textStackElement.flexibleWidth = 1f;
            VerticalLayoutGroup textLayout = textStack.gameObject.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 4;
            textLayout.childAlignment = TextAnchor.MiddleLeft;

            CreateText("Title", textStack, title, 36, Color.white, TextAlignmentOptions.Left, 48f, FontStyles.Bold, false);
            subtitleText = CreateText("Subtitle", textStack, subtitle, 23, new Color(0.8f, 0.83f, 0.82f), TextAlignmentOptions.Left, 34f, FontStyles.Normal, false);
        }

        private void AddBodyText(Transform parent, string text)
        {
            CreateText("Body", parent, text, 24, new Color(0.84f, 0.86f, 0.84f), TextAlignmentOptions.Center, 40f);
        }

        private TMP_InputField CreateInput(Transform parent, string title, string placeholder, bool isPassword)
        {
            GameObject instance = Instantiate(inputFieldPrefab, parent);
            instance.name = title + " Input";
            LayoutElement layout = instance.GetComponent<LayoutElement>() ?? instance.AddComponent<LayoutElement>();
            layout.minWidth = 760f;
            layout.preferredWidth = 760f;
            layout.flexibleWidth = 0f;
            layout.minHeight = 68f;
            layout.preferredHeight = 68f;
            layout.flexibleHeight = 0f;

            RectTransform rootRect = instance.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(760f, 68f);
            rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 760f);
            rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 68f);

            TMP_InputField input = instance.GetComponentInChildren<TMP_InputField>(true);
            RectTransform inputRect = input.GetComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(760f, 68f);
            LayoutElement inputLayout = input.GetComponent<LayoutElement>() ?? input.gameObject.AddComponent<LayoutElement>();
            inputLayout.minWidth = 760f;
            inputLayout.preferredWidth = 760f;
            inputLayout.minHeight = 68f;
            inputLayout.preferredHeight = 68f;

            input.contentType = isPassword ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.keyboardType = TouchScreenKeyboardType.Default;
            input.shouldHideMobileInput = false;
            input.shouldHideSoftKeyboard = false;
            input.onSelect.AddListener(_ => ActivateQuestKeyboard(input));

            foreach (TMP_Text text in instance.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Title")
                {
                    text.text = "";
                    text.gameObject.SetActive(false);
                }
                else if (text.name == "HelperText")
                {
                    text.text = "";
                    text.gameObject.SetActive(false);
                }
                else if (text.name == "Placeholder")
                {
                    text.text = placeholder;
                }

                text.fontSize = text.name == "Placeholder" ? 22f : 24f;
                text.fontSizeMin = 16f;
                text.fontSizeMax = 28f;
                text.enableAutoSizing = true;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }

            TintInputField(instance);

            if (isPassword)
            {
                AddPasswordVisibilityToggle(input);
            }

            return input;
        }

        private void ActivateQuestKeyboard(TMP_InputField input)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(input.gameObject);
            }

            input.ActivateInputField();
        }

        private void AddPasswordVisibilityToggle(TMP_InputField input)
        {
            RectTransform toggleRect = CreateRect("Password Visibility Toggle", input.transform);
            toggleRect.anchorMin = new Vector2(1f, 0.5f);
            toggleRect.anchorMax = new Vector2(1f, 0.5f);
            toggleRect.pivot = new Vector2(1f, 0.5f);
            toggleRect.anchoredPosition = new Vector2(-12f, 0f);
            toggleRect.sizeDelta = new Vector2(96f, 48f);

            Image background = toggleRect.gameObject.AddComponent<Image>();
            background.color = new Color(0.20f, 0.32f, 0.50f, 0.96f);
            TryAddRoundedCorners(toggleRect.gameObject, 20f);

            passwordVisibilityButton = toggleRect.gameObject.AddComponent<Button>();
            ColorBlock colors = passwordVisibilityButton.colors;
            colors.normalColor = background.color;
            colors.highlightedColor = new Color(0.30f, 0.46f, 0.70f, 1f);
            colors.pressedColor = new Color(0.12f, 0.22f, 0.36f, 1f);
            colors.selectedColor = colors.highlightedColor;
            passwordVisibilityButton.colors = colors;
            passwordVisibilityButton.targetGraphic = background;
            passwordVisibilityButton.onClick.AddListener(TogglePasswordVisibility);

            passwordVisibilityLabel = CreateTextAt(toggleRect, "Password Visibility Label", "Show", Vector2.zero,
                new Vector2(86f, 42f), 20, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        private void TogglePasswordVisibility()
        {
            passwordVisible = !passwordVisible;
            passwordInput.contentType = passwordVisible ?
                TMP_InputField.ContentType.Standard :
                TMP_InputField.ContentType.Password;
            passwordInput.ForceLabelUpdate();

            if (passwordVisibilityLabel != null)
            {
                passwordVisibilityLabel.text = passwordVisible ? "Hide" : "Show";
            }

            ActivateQuestKeyboard(passwordInput);
        }

        private Button CreateButton(GameObject prefab, Transform parent, string label, UnityAction action, float width, float height)
        {
            GameObject instance = Instantiate(prefab, parent);
            instance.name = label + " Button";

            LayoutElement layout = instance.GetComponent<LayoutElement>() ?? instance.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;

            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            Transform icon = FindChild(instance.transform, "Icon");
            if (icon != null)
            {
                icon.gameObject.SetActive(false);
            }

            SetButtonLabel(instance, label);
            NormalizeButtonText(instance, width);
            TintButton(instance, prefab == primaryButtonPrefab);

            Button button = instance.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            return button;
        }

        private RectTransform CreateList(Transform parent, float preferredHeight)
        {
            RectTransform list = CreateRect("List", parent);
            LayoutElement element = list.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = contentSize.x - 280f;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = 1f;

            VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return list;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, string value, int size, Color color,
            TextAlignmentOptions alignment, float preferredHeight, FontStyles style = FontStyles.Normal, bool wrap = true)
        {
            RectTransform rect = CreateRect(name, parent);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = contentSize.x - 320f;
            layout.preferredHeight = preferredHeight;
            layout.flexibleWidth = 1f;
            rect.sizeDelta = new Vector2(contentSize.x - 320f, preferredHeight);

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontSizeMin = Mathf.Max(12f, size * 0.55f);
            text.fontSizeMax = size;
            text.enableAutoSizing = true;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.enableWordWrapping = wrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static TextMeshProUGUI ConfigureText(TextMeshProUGUI text, string value, int size, Color color,
            TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal, bool wrap = true)
        {
            text.text = value;
            text.fontSize = size;
            text.fontSizeMin = Mathf.Max(12f, size * 0.65f);
            text.fontSizeMax = size;
            text.enableAutoSizing = true;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.enableWordWrapping = wrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void Fill(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private void BuildToast(RectTransform canvasRect)
        {
            RectTransform toast = CreateRect("Toast", canvasRect);
            toast.anchorMin = new Vector2(0.5f, 0f);
            toast.anchorMax = new Vector2(0.5f, 0f);
            toast.pivot = new Vector2(0.5f, 0f);
            toast.anchoredPosition = new Vector2(0f, 16f);
            toast.sizeDelta = new Vector2(760f, 76f);

            Image background = toast.gameObject.AddComponent<Image>();
            background.color = new Color(0.04f, 0.04f, 0.04f, 0.86f);
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;

            toastText = CreateText("Toast Text", toast, "", 26, Color.white, TextAlignmentOptions.Center, 68f);
            Fill(toastText.rectTransform, new Vector2(18f, 0f), new Vector2(-18f, 0f));
        }

        private void OnLoginPressed()
        {
            if (loginButton != null)
            {
                loginButton.interactable = false;
            }

            StartCoroutine(LoginRoutine());
        }

        private IEnumerator LoginRoutine()
        {
            string username = usernameInput.text.Trim();
            string password = passwordInput.text;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowToast("Username and password are required");
                loginButton.interactable = true;
                yield break;
            }

            ShowToast("Signing in...");
            ApiResult<LoginResponse> result = default;
            yield return api.Login(username, password, value => result = value);
            loginButton.interactable = true;

            if (!result.Success || result.Data == null || !result.Data.success)
            {
                ShowToast(result.NetworkFailure ? FriendlyApiMessage(result.Error) : "Invalid username or password");
                yield break;
            }

            token = result.Data.token;
            ShowToast(result.FromMock ? "Login Successful (mock API)" : "Login Successful");
            ShowProjects();
            StartCoroutine(LoadProjectsRoutine());
        }

        private IEnumerator LoadProjectsRoutine()
        {
            ClearChildren(projectListRoot);
            CreateTextAt(projectListRoot, "Loading", "Loading projects...", Vector2.zero, new Vector2(1180f, 52f),
                26, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            ApiResult<ProjectListResponse> result = default;
            yield return api.GetProjects(value => result = value);
            ClearChildren(projectListRoot);

            if (!result.Success || result.Data?.projects == null)
            {
                CreateTextAt(projectListRoot, "Error", FriendlyApiMessage(result.Error), Vector2.zero, new Vector2(1180f, 52f),
                    26, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                ShowToast(FriendlyApiMessage(result.Error));
                yield break;
            }

            float startY = 235f;
            for (int i = 0; i < result.Data.projects.Length; i++)
            {
                ProjectDto project = result.Data.projects[i];
                int id = project.id;
                string name = project.name;
                CreateButtonAt(projectListRoot, "Project " + id, name, new Vector2(0f, startY - i * 92f),
                    new Vector2(1180f, 78f), () => SelectProject(id, name), false, TextAlignmentOptions.Left);
            }
        }

        private void SelectProject(int id, string name)
        {
            selectedProjectId = id;
            selectedProjectName = name;
            ShowToast("Project Selected: " + name);
            ShowFloorSelection();
            UpdateSelectionSummary();
            StartCoroutine(LoadFloorsRoutine());
        }

        private IEnumerator LoadFloorsRoutine()
        {
            floorsExpanded = false;
            selectedFloorName = "";
            selectedFloorButton = null;
            floorSubtitle.text = selectedProjectName;
            floorListRoot.gameObject.SetActive(false);
            SetButtonLabel(floorDropdownButton.gameObject, "Select Floor v");
            UpdateSelectionSummary();
            ClearChildren(floorListRoot);

            ApiResult<FloorListResponse> result = default;
            yield return api.GetFloors(selectedProjectId, value => result = value);

            if (!result.Success || result.Data?.floors == null)
            {
                ShowToast(FriendlyApiMessage(result.Error));
                yield break;
            }

            float startY = 112f;
            for (int i = 0; i < result.Data.floors.Length; i++)
            {
                string floor = result.Data.floors[i];
                GameObject floorButtonObject = null;
                Button button = CreateButtonAt(floorListRoot, "Floor " + floor, floor, new Vector2(0f, startY - i * 72f),
                    new Vector2(836f, 58f), () => SelectFloor(floor, floorButtonObject), false, TextAlignmentOptions.Left);
                floorButtonObject = button.gameObject;
            }
        }

        private void ToggleFloorDropdown()
        {
            floorsExpanded = !floorsExpanded;
            floorListRoot.gameObject.SetActive(floorsExpanded);
            string closedLabel = string.IsNullOrWhiteSpace(selectedFloorName) ? "Select Floor v" : selectedFloorName + " v";
            floorDropdownLabel.text = floorsExpanded ? "Select Floor ^" : closedLabel;
        }

        private void SelectFloor(string floor, GameObject buttonObject)
        {
            selectedFloorButton = buttonObject;
            selectedFloorName = floor;
            SetButtonLabel(floorDropdownButton.gameObject, floor + " v");
            floorsExpanded = false;
            floorListRoot.gameObject.SetActive(false);
            UpdateSelectionSummary();
            ShowToast("Selected Floor: " + floor);
        }

        private void UpdateSelectionSummary()
        {
            if (summaryProjectText != null)
            {
                summaryProjectText.text = string.IsNullOrWhiteSpace(selectedProjectName)
                    ? "Project: -"
                    : "Project: " + selectedProjectName;
            }

            if (summaryFloorText != null)
            {
                summaryFloorText.text = string.IsNullOrWhiteSpace(selectedFloorName)
                    ? "Floor: -"
                    : "Floor: " + selectedFloorName;
            }
        }

        private void ShowLogin()
        {
            token = "";
            SetActivePanel(loginPanel);
        }

        private void ShowProjects()
        {
            SetActivePanel(projectPanel);
        }

        private void ShowFloorSelection()
        {
            SetActivePanel(floorPanel);
        }

        private void SetActivePanel(GameObject active)
        {
            loginPanel.SetActive(active == loginPanel);
            projectPanel.SetActive(active == projectPanel);
            floorPanel.SetActive(active == floorPanel);
        }

        private void ShowToast(string message)
        {
            if (toastRoutine != null)
            {
                StopCoroutine(toastRoutine);
            }

            toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        private static string FriendlyApiMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "Request failed";
            }

            if (message.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
            {
                return "Invalid username or password";
            }

            return message.StartsWith("{", StringComparison.Ordinal)
                ? "Request failed"
                : message;
        }

        private IEnumerator ToastRoutine(string message)
        {
            toastText.text = message;
            yield return FadeToast(1f, 0.14f);
            yield return new WaitForSeconds(2.25f);
            yield return FadeToast(0f, 0.28f);
        }

        private IEnumerator FadeToast(float target, float duration)
        {
            float start = toastGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                toastGroup.alpha = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }

            toastGroup.alpha = target;
        }

        private static void ClearChildren(Transform parent)
        {
            List<GameObject> children = new List<GameObject>();
            foreach (Transform child in parent)
            {
                children.Add(child.gameObject);
            }

            foreach (GameObject child in children)
            {
                Destroy(child);
            }
        }

        private static void SetButtonLabel(GameObject button, string label)
        {
            TMP_Text text = FindPreferredText(button);
            if (text != null)
            {
                text.text = label;
                text.fontSize = Mathf.Max(text.fontSize, 28f);
                text.fontSizeMin = 18f;
                text.fontSizeMax = Mathf.Max(text.fontSize, 32f);
                text.enableAutoSizing = true;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Overflow;
            }
        }

        private static void TintButton(GameObject buttonObject, bool primary)
        {
            Color normal = primary ?
                new Color(0.25f, 0.45f, 0.82f, 1f) :
                new Color(0.16f, 0.25f, 0.38f, 0.94f);
            Color highlighted = primary ?
                new Color(0.34f, 0.55f, 0.94f, 1f) :
                new Color(0.22f, 0.34f, 0.5f, 0.98f);
            Color pressed = primary ?
                new Color(0.16f, 0.32f, 0.65f, 1f) :
                new Color(0.11f, 0.19f, 0.31f, 1f);

            foreach (Image image in buttonObject.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Background")
                {
                    image.color = normal;
                }
            }

            Button button = buttonObject.GetComponent<Button>();
            if (button != null)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = normal;
                colors.highlightedColor = highlighted;
                colors.pressedColor = pressed;
                colors.selectedColor = highlighted;
                colors.disabledColor = new Color(0.2f, 0.23f, 0.28f, 0.45f);
                colors.colorMultiplier = 1f;
                button.colors = colors;
            }

            foreach (TMP_Text text in buttonObject.GetComponentsInChildren<TMP_Text>(true))
            {
                text.color = Color.white;
            }
        }

        private static void TintInputField(GameObject inputObject)
        {
            foreach (Image image in inputObject.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Background" || image.name == "TextField")
                {
                    image.color = new Color(0.16f, 0.23f, 0.34f, 0.95f);
                }
                else if (image.name == "Icon")
                {
                    image.color = new Color(0.76f, 0.84f, 0.96f, 1f);
                }
            }
        }

        private static void NormalizeButtonText(GameObject button, float buttonWidth)
        {
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                text.enableAutoSizing = true;
                text.enableWordWrapping = false;
                text.alignment = TextAlignmentOptions.Center;
                text.overflowMode = TextOverflowModes.Overflow;
                RectTransform rect = text.rectTransform;
                rect.sizeDelta = new Vector2(Mathf.Max(120f, buttonWidth - 96f), rect.sizeDelta.y);
            }
        }

        private static TMP_Text FindPreferredText(GameObject root)
        {
            TMP_Text fallback = null;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Label")
                {
                    return text;
                }

                if (fallback == null && text.gameObject.activeInHierarchy)
                {
                    fallback = text;
                }
            }

            return fallback;
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform result = FindChild(child, childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}

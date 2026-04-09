using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CoopPrototypeCanvasUI : MonoBehaviour
{
    private enum MenuWindow { Main, Settings, ConnectionType, Create, Join, InGame }

    [Header("Global UI Elements")]
    [SerializeField] private GameObject darkBackgroundOverlay; 

    [Header("Screens (Root Panels)")]
    [SerializeField] private GameObject mainMenuScreen;       
    [SerializeField] private GameObject settingsScreen;       
    [SerializeField] private GameObject connectionTypeScreen; 
    [SerializeField] private GameObject createRoomScreen;     
    [SerializeField] private GameObject joinRoomScreen;       

    [Header("1. Main Menu")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsMenuButton;
    [SerializeField] private Button quitButton;

    [Header("2. Settings")]
    [SerializeField] private Slider bgSoundsSlider;
    [SerializeField] private Slider interactionSoundsSlider;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Button settingsBackButton; 

    [Header("3. Connection Type")]
    [SerializeField] private Button localConnButton;
    [SerializeField] private Button onlineConnButton; 
    [SerializeField] private Button createRoomModeButton;
    [SerializeField] private Button connectRoomModeButton;
    [SerializeField] private Button connTypeBackButton;

    [Header("4. Create Room")]
    [SerializeField] private TMP_Text createStatusText;    
    [SerializeField] private TMP_Text createModeLabelText; 
    [SerializeField] private TMP_InputField createPlayerNameInput;
    [SerializeField] private TMP_InputField createRoomNameInput;   
    [SerializeField] private TMP_InputField createRoomCodeInput;   
    [SerializeField] private TMP_InputField createPasswordInput;
    [SerializeField] private Toggle publicRoomToggle;              
    [SerializeField] private Toggle passwordRoomToggle;            
    [SerializeField] private Button startRoomButton;
    [SerializeField] private Button createBackButton;

    [Header("5. Join Room")]
    [SerializeField] private TMP_Text joinStatusText;      
    [SerializeField] private TMP_Text joinModeLabelText;
    [SerializeField] private TMP_InputField joinPlayerNameInput;
    [SerializeField] private TMP_InputField joinRoomCodeInput;     
    [SerializeField] private TMP_InputField joinPasswordInput;     
    [SerializeField] private Button connectButton;
    [SerializeField] private Button joinBackButton;

    private CoopPrototypeController _controller;

    private void Awake()
    {
        BindListeners();
        InitializeToggles(); 
    }

    private void Start()
    {
        TryBindController();
        ChangeWindow(MenuWindow.Main); 
    }

    private void Update()
    {
        TryBindController();
        
        if (_controller != null)
        {
            UpdateStatusText();

            if (_controller.IsInGame || _controller.IsWaitingRoom)
            {
                ChangeWindow(MenuWindow.InGame);
            }
        }
    }

    private void TryBindController()
    {
        if (_controller != null) return;
        
        _controller = CoopPrototypeController.Instance;
        if (_controller == null) _controller = FindAnyObjectByType<CoopPrototypeController>();
        
        if (_controller != null) _controller.AttachCanvasUi();
    }

    private void OnDestroy()
    {
        if (_controller != null) _controller.DetachCanvasUi();
    }

    // --- ІНІЦІАЛІЗАЦІЯ ---

    private void InitializeToggles()
    {
        if (publicRoomToggle != null) publicRoomToggle.isOn = false;
        if (passwordRoomToggle != null) passwordRoomToggle.isOn = false;
        
        SetActive(createPasswordInput, false);
    }

    // --- НАВІГАЦІЯ ТА ВІДОБРАЖЕННЯ ---

    private void ChangeWindow(MenuWindow window)
    {
        SetActive(mainMenuScreen, window == MenuWindow.Main);
        SetActive(settingsScreen, window == MenuWindow.Settings);
        SetActive(connectionTypeScreen, window == MenuWindow.ConnectionType);
        SetActive(createRoomScreen, window == MenuWindow.Create);
        SetActive(joinRoomScreen, window == MenuWindow.Join);

        bool showOverlay = window == MenuWindow.Settings || 
                           window == MenuWindow.ConnectionType || 
                           window == MenuWindow.Create || 
                           window == MenuWindow.Join;
                           
        SetActive(darkBackgroundOverlay, showOverlay); 

        if (window == MenuWindow.Create || window == MenuWindow.Join)
        {
            RefreshDynamicFields();
        }
    }

    private void RefreshDynamicFields()
    {
        if (_controller == null) return;

        bool isLocal = _controller.IsLocalScenario;

        if (createModeLabelText) 
            createModeLabelText.text = isLocal ? "Mode: Local (Create Room)" : "Mode: Online (Create Room)";
            
        if (joinModeLabelText) 
            joinModeLabelText.text = isLocal ? "Mode: Local (Connect to Room)" : "Mode: Online (Connect to Room)";

        // CREATE ROOM
        SetActive(createRoomNameInput, !isLocal);  
        SetActive(createRoomCodeInput, isLocal);   
        SetActive(publicRoomToggle, !isLocal);     
        SetActive(passwordRoomToggle, !isLocal);

        // JOIN ROOM
        SetActive(joinPasswordInput, !isLocal);

        UpdatePasswordVisibility();
    }

    private void UpdatePasswordVisibility()
    {
        if (_controller == null) return;

        bool isLocal = _controller.IsLocalScenario;
        
        if (isLocal)
        {
            SetActive(createPasswordInput, false);
        }
        else
        {
            bool showPassword = passwordRoomToggle != null && passwordRoomToggle.isOn;
            SetActive(createPasswordInput, showPassword);
        }
    }

    private void UpdateStatusText()
    {
        string statusMessage = "Status: " + _controller.StatusText;
        
        if (createStatusText != null) createStatusText.text = statusMessage;
        if (joinStatusText != null) joinStatusText.text = statusMessage;
    }

    // --- ПРИВ'ЯЗКА ПОДІЙ ---

    private void BindListeners()
    {
        // 1. Налаштування кнопок-перемикачів (Local / Online) з аніматором
        ButtonAnimator localAnim = localConnButton != null ? localConnButton.GetComponent<ButtonAnimator>() : null;
        ButtonAnimator onlineAnim = onlineConnButton != null ? onlineConnButton.GetComponent<ButtonAnimator>() : null;

        BindButton(localConnButton, () => {
            _controller?.SelectLocalScenario();
            if (localAnim) localAnim.SetSelected(true);
            if (onlineAnim) onlineAnim.SetSelected(false);
        });

        BindButton(onlineConnButton, () => {
            _controller?.SelectNetworkScenario();
            if (onlineAnim) onlineAnim.SetSelected(true);
            if (localAnim) localAnim.SetSelected(false);
        });

        // 2. Стандартні кнопки
        BindButton(playButton, () => ChangeWindow(MenuWindow.ConnectionType));
        BindButton(settingsMenuButton, () => ChangeWindow(MenuWindow.Settings));
        BindButton(quitButton, Application.Quit);

        BindButton(settingsBackButton, () => ChangeWindow(MenuWindow.Main));
        BindButton(createRoomModeButton, () => ChangeWindow(MenuWindow.Create));
        BindButton(connectRoomModeButton, () => ChangeWindow(MenuWindow.Join));
        BindButton(connTypeBackButton, () => ChangeWindow(MenuWindow.Main));

        BindButton(startRoomButton, OnStartRoomClicked);
        BindButton(createBackButton, () => ChangeWindow(MenuWindow.ConnectionType));

        BindButton(connectButton, OnJoinRoomClicked);
        BindButton(joinBackButton, () => ChangeWindow(MenuWindow.ConnectionType));

        // 3. Слайдери та галочки (Toggles)
        if (bgSoundsSlider) bgSoundsSlider.onValueChanged.AddListener(v => Debug.Log("BG Vol: " + v));
        if (interactionSoundsSlider) interactionSoundsSlider.onValueChanged.AddListener(v => Debug.Log("Int Vol: " + v));
        if (volumeSlider) volumeSlider.onValueChanged.AddListener(v => Debug.Log("Master Vol: " + v));

        if (publicRoomToggle != null)
        {
            publicRoomToggle.onValueChanged.AddListener((isOn) => {
                if (isOn && passwordRoomToggle != null) passwordRoomToggle.isOn = false;
            });
        }

        if (passwordRoomToggle != null)
        {
            passwordRoomToggle.onValueChanged.AddListener((isOn) => {
                if (isOn && publicRoomToggle != null) publicRoomToggle.isOn = false;
                UpdatePasswordVisibility();
            });
        }
    }

    // --- ЛОГІКА МЕРЕЖІ ---

    private void OnStartRoomClicked()
    {
        if (_controller == null) return;

        _controller.SetPlayerName(createPlayerNameInput != null ? createPlayerNameInput.text : "Player");
        _controller.SetRoomPassword(createPasswordInput != null ? createPasswordInput.text : "");
        
        if (_controller.IsLocalScenario)
        {
            _controller.SetRoomCode(createRoomCodeInput != null ? createRoomCodeInput.text : "");
            _controller.SetPrivateRoom(false); 
        }
        else
        {
            _controller.SetRoomName(createRoomNameInput != null ? createRoomNameInput.text : "My Room");
            _controller.SetPrivateRoom(passwordRoomToggle != null && passwordRoomToggle.isOn);
        }

        _controller.TryCreateRoomFromUi();
    }

    private void OnJoinRoomClicked()
    {
        if (_controller == null) return;

        _controller.SetPlayerName(joinPlayerNameInput != null ? joinPlayerNameInput.text : "Player");
        _controller.SetRoomCode(joinRoomCodeInput != null ? joinRoomCodeInput.text : "");
        _controller.SetRoomPassword(joinPasswordInput != null ? joinPasswordInput.text : "");
        
        _controller.TryJoinRoomFromUi();
    }

    // --- БЕЗПЕЧНІ ДОПОМІЖНІ МЕТОДИ (Тут були помилки CS0103) ---

    private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button != null && callback != null)
        {
            button.onClick.AddListener(callback);
        }
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null && target.activeSelf != value)
        {
            target.SetActive(value);
        }
    }

    private static void SetActive(Component target, bool value)
    {
        if (target != null && target.gameObject != null && target.gameObject.activeSelf != value)
        {
            target.gameObject.SetActive(value);
        }
    }
}
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CoopCustomMenuBridge : MonoBehaviour
{
    [Header("Optional Inputs")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private Toggle networkConnectionToggle;
    [SerializeField] private Toggle privateRoomToggle;

    public void OpenLobby()
    {
        CoopPrototypeController.Instance?.OpenLobbyFromCustomMenu();
    }

    public void HostGame()
    {
        if (IsNetworkConnectionSelected())
        {
            CoopPrototypeController.Instance?.HostNetworkGameFromCustomMenu(
                ReadInput(playerNameInput),
                ReadInput(roomNameInput),
                privateRoomToggle != null && privateRoomToggle.isOn,
                ReadInput(passwordInput));
            return;
        }

        CoopPrototypeController.Instance?.HostLocalGameFromCustomMenu(
            ReadInput(playerNameInput),
            ReadInput(roomNameInput),
            ReadInput(passwordInput));
    }

    public void JoinGame()
    {
        if (IsNetworkConnectionSelected())
        {
            CoopPrototypeController.Instance?.JoinNetworkGameFromCustomMenu(
                ReadInput(roomCodeInput),
                ReadInput(playerNameInput),
                ReadInput(passwordInput));
            return;
        }

        CoopPrototypeController.Instance?.JoinLocalGameFromCustomMenu(
            ReadInput(roomCodeInput),
            ReadInput(playerNameInput),
            ReadInput(passwordInput));
    }

    public void ExitGame()
    {
        CoopPrototypeController.Instance?.ExitGameFromCustomMenu();
    }

    private bool IsNetworkConnectionSelected()
    {
        return networkConnectionToggle != null && networkConnectionToggle.isOn;
    }

    private static string ReadInput(TMP_InputField input)
    {
        return input == null ? string.Empty : input.text ?? string.Empty;
    }
}

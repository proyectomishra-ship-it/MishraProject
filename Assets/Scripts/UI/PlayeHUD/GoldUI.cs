using TMPro;
using Unity.Netcode;
using UnityEngine;

public class GoldUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text goldText;
    private GoldController goldController;

    private void OnEnable()
    {
        TryInitialize();
    }

    private void OnDisable()
    {
        if (goldController != null)
        {
            goldController.OnGoldChanged -= HandleGoldChanged;
            goldController = null;
        }
    }

    private void Update()
    {
        if (goldController == null)
        {
            TryInitialize();
        }
    }

    private void TryInitialize()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsClient)
            return;

        if (NetworkManager.Singleton.LocalClient == null)
            return;

        if (NetworkManager.Singleton.LocalClient.PlayerObject == null)
            return;

        GoldController controller =
            NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<GoldController>();

        if (controller == null)
            return;

        if (goldController == controller)
            return;

        if (goldController != null)
        {
            goldController.OnGoldChanged -= HandleGoldChanged;
        }

        goldController = controller;
        goldController.OnGoldChanged += HandleGoldChanged;

        UpdateGoldText();
    }

    private void HandleGoldChanged(int oldValue, int newValue)
    {
        UpdateGoldText();
    }

    private void UpdateGoldText()
    {
        if (goldText == null)
            return;

        if (goldController == null)
        {
            goldText.text = "Oro: 0";
            return;
        }

        goldText.text = $"Oro: {goldController.Gold}";
    }
}
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BuildingPurchasePopup : MonoBehaviour
{
    [Header("Texty")]
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text needText;
    [SerializeField] private TMP_Text guestSlotsText;
    [SerializeField] private TMP_Text workerSlotsText;
    [SerializeField] private TMP_Text requirementText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text balanceText;
    [SerializeField] private TMP_Text feedback;

    [Header("Tlačidlá")]
    [SerializeField] private Button purchaseButton;
    [SerializeField] private TMP_Text purchaseButtonLabel;
    [SerializeField] private Button closeButton;

    [Header("Farby")]
    [SerializeField] private Color normalPriceColor = new Color(0.25f, 0.17f, 0.08f);
    [SerializeField] private Color notAffordableColor = new Color(0.75f, 0.15f, 0.15f);

    private GameController gameController;

    public event Action<BuildingType> Purchased;

    public RectTransform PopupRect => (RectTransform)transform;
    public BuildingDefinition Definition { get; private set; }
    public int Level => ResortSession.PurchaseLevel;
    public int PriceCoins => Definition.GetCostCoins(Level);
    public int AvailableCoins => gameController.Data.coins;
    public bool CanAfford => AvailableCoins >= PriceCoins;

    private void OnEnable()
    {
        if (purchaseButton != null) purchaseButton.onClick.AddListener(Purchase);
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
    }

    private void OnDisable()
    {
        if (purchaseButton != null) purchaseButton.onClick.RemoveListener(Purchase);
        if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
    }

    public void Show(BuildingDefinition definition, GameController controller)
    {
        Definition = definition;
        gameController = controller;
        gameObject.SetActive(true);
        Refresh(gameController.GetBuildingPurchaseBlockReason(definition.Type));
    }

    public void Purchase()
    {
        if (gameController == null || Definition == null) return;

        if (gameController.TryPurchaseBuilding(Definition.Type, out string error))
        {
            Purchased?.Invoke(Definition.Type);
            Hide();
        }
        else Refresh(error);
    }

    public void Hide() => gameObject.SetActive(false);

    private void Refresh(string blockReason)
    {
        string need = Definition.ServicedNeed == NeedType.OceanView
            ? "Ocean View" : Definition.ServicedNeed.ToString();

        Set(title, Definition.DisplayName);
        Set(needText, $"Serves: {need}");
        Set(guestSlotsText, $"{Definition.GetCapacity(Level)} guest slots");
        Set(workerSlotsText, $"{Definition.GetWorkerSlots(Level)} Worker slot");
        Set(requirementText, $"Available after Wave {Definition.RequiredCompletedWave}");
        Set(priceText, $"{PriceCoins:N0}");
        Set(balanceText, $"You have: {AvailableCoins:N0}");
        Set(feedback, blockReason ?? string.Empty);

        if (priceText != null) priceText.color = CanAfford ? normalPriceColor : notAffordableColor;
        if (purchaseButton != null) purchaseButton.interactable = blockReason == null;
        if (purchaseButtonLabel != null) purchaseButtonLabel.text = blockReason == null ? "Buy" : "Can't buy";
    }

    private static void Set(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }
}
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BuildingPurchasePopup : MonoBehaviour
{
    [SerializeField] private Text title;
    [SerializeField] private Text details;
    [SerializeField] private Text feedback;
    [SerializeField] private Button purchaseButton;

    private GameController gameController;

    public RectTransform PopupRect => (RectTransform)transform;
    public BuildingDefinition Definition { get; private set; }
    public int Level => ResortSession.PurchaseLevel;
    public int PriceCoins => Definition.GetCostCoins(Level);
    public int AvailableCoins => gameController.Data.coins;
    public bool CanAfford => AvailableCoins >= PriceCoins;

    public void Show(BuildingDefinition definition, GameController controller)
    {
        Definition = definition;
        gameController = controller;
        if (feedback == null) feedback = transform.Find("Purchase feedback")?.GetComponent<Text>();
        if (purchaseButton == null) RestorePurchaseButton();
        title.text = definition.DisplayName;
        Refresh(gameController.GetBuildingPurchaseBlockReason(definition.Type));
        gameObject.SetActive(true);
    }

    public void Purchase()
    {
        if (!isActiveAndEnabled || gameController == null) return;

        if (gameController.TryPurchaseBuilding(Definition.Type, out string error)) Hide();
        else Refresh(error);
    }

    private void Refresh(string blockReason)
    {
        string need = Definition.ServicedNeed == NeedType.OceanView ? "Ocean View" : Definition.ServicedNeed.ToString();
        string spacer = feedback == null && blockReason != null ? "\n" : "\n\n";
        details.text = $"Level {Level}  ·  {need}\n" +
            $"{Definition.GetCapacity(Level)} guest slots  ·  {Definition.GetWorkerSlots(Level)} Worker slot\n" +
            $"Available after Wave {Definition.RequiredCompletedWave}{spacer}" +
            $"Price   {PriceCoins:N0} Coins\n" +
            $"Balance   {AvailableCoins:N0} Coins";
        if (feedback != null) feedback.text = blockReason ?? string.Empty;
        else if (blockReason != null) details.text += "\n" + blockReason;
        purchaseButton.interactable = blockReason == null;
    }

    private void RestorePurchaseButton()
    {
        purchaseButton = transform.Find("Purchase")?.GetComponent<Button>();
        if (purchaseButton == null)
        {
            var buttonObject = DefaultControls.CreateButton(default);
            buttonObject.name = "Purchase";
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(30, 24);
            rect.sizeDelta = new Vector2(140, 46);
            buttonObject.layer = gameObject.layer;
            Text label = buttonObject.GetComponentInChildren<Text>(true);
            label.gameObject.layer = gameObject.layer;
            label.text = "Purchase";
            label.fontSize = 22;
            label.raycastTarget = false;
            purchaseButton = buttonObject.GetComponent<Button>();
        }
        if (purchaseButton.onClick.GetPersistentEventCount() == 0)
            purchaseButton.onClick.AddListener(Purchase);
    }

    public void Hide() => gameObject.SetActive(false);
}

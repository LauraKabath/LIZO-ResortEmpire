using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BuildingPurchasePanel : MonoBehaviour
{
    private enum PurchaseState { Locked, Available, NotAffordable, Unlocked }

    [System.Serializable]
    public class Card
    {
        public BuildingType type;               // Pool alebo Bungalows
        public TMP_Text nameText;
        public TMP_Text priceText;
        public TMP_Text conditionText;          // podmienky odomknutia
        public TMP_Text workerText;             // info o novom Workerovi
        public TMP_Text statusText;             // Locked / Available / ...
        public Button buyButton;
        public TMP_Text buyButtonLabel;
        [Tooltip("Objekt budovy na ostrove, ktorý sa zapne po kúpe.")]
        public GameObject islandVisual;
        [Tooltip("Zamknutá oblasť (napr. Pool_locked), ktorá sa po kúpe vypne.")]
        public GameObject lockedAreaVisual;
    }

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private Card[] cards;

    [Header("Texty tlačidla")]
    [SerializeField] private string buyLabel = "Buy";
    [SerializeField] private string lockedLabel = "Locked";
    [SerializeField] private string notAffordableLabel = "Not enough coins";
    [SerializeField] private string unlockedLabel = "Owned";

    private GameController game;

    private void Awake()
    {
        game = FindFirstObjectByType<GameController>();

        if (closeButton != null) closeButton.onClick.AddListener(Close);

        foreach (Card card in cards)
        {
            if (card.buyButton == null) continue;
            BuildingType type = card.type;
            card.buyButton.onClick.AddListener(() => OnBuyClicked(type));
        }

        panelRoot.SetActive(false);
    }

    private void Start()
    {
        // Budovy, ktoré už hráč vlastní, majú byť na ostrove viditeľné hneď.
        RefreshIslandVisuals();
    }

    private void OnEnable()
    {
        if (game == null) game = FindFirstObjectByType<GameController>();
        if (game != null) game.PhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        if (game != null) game.PhaseChanged -= HandlePhaseChanged;
    }

    // Toto napoj na tlačidlo, ktoré panel otvára (OnClick).
    public void Open()
    {
        panelRoot.SetActive(true);
        Refresh();
    }

    // Návrat do hry.
    public void Close() => panelRoot.SetActive(false);

    private void HandlePhaseChanged(ResortPhase phase)
    {
        RefreshIslandVisuals();
        if (panelRoot.activeSelf) Refresh();
    }

    private void OnBuyClicked(BuildingType type)
    {
        if (game == null) return;

        if (game.TryPurchaseBuilding(type, out string error))
        {
            Refresh();
            RefreshIslandVisuals();
        }
        else
        {
            Debug.LogWarning($"Purchase failed: {error}", this);
            Refresh();
        }
    }

    private void Refresh()
    {
        if (game == null || !game.IsInitialized) return;

        // PREDPOKLAD: ResortData má pole/property coins
        if (coinsText != null) coinsText.text = $"Coins: {game.Data.coins}";

        foreach (Card card in cards)
            RefreshCard(card);
    }

    private void RefreshCard(Card card)
    {
        BuildingDefinition def = game.GetBuildingDefinition(card.type);
        BuildingData data = FindData(card.type);
        if (def == null || data == null) return;

        PurchaseState state = GetState(card.type, def, data);

        if (card.nameText != null) card.nameText.text = def.DisplayName;
        if (card.priceText != null) card.priceText.text = $"{def.GetCostCoins(1)} Coins";

        if (card.conditionText != null)
            card.conditionText.text = def.RequiredCompletedWave > 0
                ? $"Complete wave {def.RequiredCompletedWave}"
                : "No requirement";

        if (card.workerText != null)
        {
            int slots = def.GetWorkerSlots(1);
            card.workerText.text = slots == 1
                ? "Unlocks 1 new Worker slot"
                : $"Unlocks {slots} new Worker slots";
        }

        string status = state switch
        {
            PurchaseState.Locked => "Locked",
            PurchaseState.Available => "Available",
            PurchaseState.NotAffordable => "Not enough coins",
            _ => "Unlocked"
        };
        if (card.statusText != null) card.statusText.text = status;

        if (card.buyButton != null)
            card.buyButton.interactable = state == PurchaseState.Available;

        if (card.buyButtonLabel != null)
        {
            card.buyButtonLabel.text = state switch
            {
                PurchaseState.Locked => lockedLabel,
                PurchaseState.Available => buyLabel,
                PurchaseState.NotAffordable => notAffordableLabel,
                _ => unlockedLabel
            };
        }

        if (card.islandVisual != null) card.islandVisual.SetActive(data.owned);
        if (card.lockedAreaVisual != null) card.lockedAreaVisual.SetActive(!data.owned);
    }

    private PurchaseState GetState(BuildingType type, BuildingDefinition def, BuildingData data)
    {
        if (data.owned) return PurchaseState.Unlocked;
        if (!data.availableToPurchase) return PurchaseState.Locked;
        if (game.Data.coins < def.GetCostCoins(1)) return PurchaseState.NotAffordable;

        // Posledná poistka: GameController vie o ďalších dôvodoch (napr. fáza hry).
        string reason = game.GetBuildingPurchaseBlockReason(type);
        return string.IsNullOrEmpty(reason) ? PurchaseState.Available : PurchaseState.Locked;
    }

    private BuildingData FindData(BuildingType type)
    {
        foreach (BuildingData b in game.Data.buildings)
            if (b.type == type) return b;
        return null;
    }

    private void RefreshIslandVisuals()
    {
        if (game == null || !game.IsInitialized) return;

        foreach (Card card in cards)
        {
            BuildingData data = FindData(card.type);
            if (data == null) continue;
            if (card.islandVisual != null) card.islandVisual.SetActive(data.owned);
            if (card.lockedAreaVisual != null) card.lockedAreaVisual.SetActive(!data.owned);
        }
    }
}
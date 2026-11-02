using TMPro;
using UnityEngine;

// Displays resort resources without modifying gameplay data.
[DisallowMultipleComponent]
public sealed class TopBarUI : MonoBehaviour
{
    [Header("Data source")]
    [SerializeField] private GameController gameController;

    [Header("TopBar value labels")]
    [SerializeField] private TMP_Text coinsValue;
    [SerializeField] private TMP_Text energyValue;
    [SerializeField] private TMP_Text resortValueValue;
    [SerializeField] private TMP_Text reputationValue;
    [SerializeField] private TMP_Text gemsValue;
    [SerializeField] private TMP_Text materialsValue;

    private ResortData lastData;
    private bool hasSnapshot;
    private int lastCoins;
    private int lastEnergy;
    private int lastResortValue;
    private int lastReputation;
    private int lastGems;
    private int lastMaterials;

    private void Awake()
    {
        if (gameController == null)
            Debug.LogWarning("TopBarUI: Assign GameController in the Inspector.", this);
    }

    private void OnEnable()
    {
        hasSnapshot = false;
    }

    private void LateUpdate()
    {
        // GameController creates its ResortData in Start().
        // LateUpdate allows initialization to finish before the first UI refresh.
        if (gameController == null || !gameController.IsInitialized)
            return;

        ResortData data = gameController.Data;
        if (data == null)
            return;

        bool newData = !ReferenceEquals(lastData, data);

        RefreshIfChanged(coinsValue, data.coins, ref lastCoins, newData);
        RefreshIfChanged(energyValue, data.energy, ref lastEnergy, newData);
        RefreshIfChanged(resortValueValue, data.resortValue, ref lastResortValue, newData);
        RefreshIfChanged(reputationValue, data.reputation, ref lastReputation, newData);
        RefreshIfChanged(gemsValue, data.gems, ref lastGems, newData);
        RefreshIfChanged(materialsValue, data.materials, ref lastMaterials, newData);

        lastData = data;
        hasSnapshot = true;
    }

    private void RefreshIfChanged(TMP_Text label, int value, ref int previous, bool newData)
    {
        if (label != null && (!hasSnapshot || newData || value != previous))
            label.text = value.ToString();

        previous = value;
    }
}

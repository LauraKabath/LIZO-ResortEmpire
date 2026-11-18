using TMPro;
using UnityEngine;

public class RewardsPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text resortValueText;

    public void Show(WaveRewardResult reward)
    {
        coinsText.text = $"Coins: {reward.Coins}";
        energyText.text = $"Energy: {reward.Energy}";
        resortValueText.text = $"Resort Value: +{reward.ResortValue}";
        gameObject.SetActive(true);
    }
}
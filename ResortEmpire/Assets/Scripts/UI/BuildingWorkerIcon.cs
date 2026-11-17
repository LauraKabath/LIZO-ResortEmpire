using UnityEngine;

// Shows an icon on a building while a worker is assigned to it.
public sealed class BuildingWorkerIcon : MonoBehaviour
{
    [SerializeField] private GameController gameController;
    [SerializeField] private BuildingType buildingType;
    [SerializeField] private GameObject icon;

    private WorkerRoster roster;

    private void Awake()
    {
        if (icon != null) icon.SetActive(false);
    }

    private void Update()
    {
        // GameController creates the roster in Start(), same as in the panel.
        if (roster == null) Connect();
    }

    private void OnDisable()
    {
        if (roster != null) roster.Changed -= Refresh;
        roster = null;
    }

    private void Connect()
    {
        if (gameController == null || !gameController.IsInitialized || gameController.Workers == null)
            return;
        roster = gameController.Workers;
        roster.Changed += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        if (icon != null)
            icon.SetActive(roster.GetOccupiedSlots(buildingType) > 0);
    }
}
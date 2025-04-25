using UnityEngine;

[System.Serializable]
public class RoomProperties {
    [SerializeField] private RoomType roomType;
    [SerializeField] private bool hasEnemies;
    [SerializeField] private bool hasItemChest;
    [SerializeField] private bool hasMerchant;
    [SerializeField] private bool hasBoss;

    public RoomType RoomType {
        get => roomType;
        set => roomType = value;
    }

    public bool HasEnemies {
        get => hasEnemies;
        set => hasEnemies = value;
    }

    public bool HasItemChest {
        get => hasItemChest;
        set => hasItemChest = value;
    }

    public bool HasMerchant {
        get => hasMerchant;
        set => hasMerchant = value;
    }

    public bool HasBoss {
        get => hasBoss;
        set => hasBoss = value;
    }

    public RoomProperties(RoomType type) {
        roomType = type;
        hasEnemies = false;
        hasItemChest = false;
        hasMerchant = false;
        hasBoss = false;
    }
} 
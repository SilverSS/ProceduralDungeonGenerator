using UnityEngine;

[ExecuteInEditMode]
public class RoomVisualizer : MonoBehaviour {
    [SerializeField] private RoomType roomType = RoomType.Normal;
    [SerializeField] private bool hasEnemies = false;
    [SerializeField] private bool hasItemChest = false;
    [SerializeField] private bool hasMerchant = false;
    [SerializeField] private bool hasBoss = false;

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

    private void OnDrawGizmos() {
        // 방 타입에 따른 색상 설정
        Color roomColor = Color.white;
        switch (roomType) {
            case RoomType.Start:
                roomColor = Color.green;
                break;
            case RoomType.Exit:
                roomColor = Color.red;
                break;
            case RoomType.Normal:
                roomColor = Color.gray;
                break;
        }

        // 방의 속성에 따른 아이콘 표시
        Vector3 position = transform.position;
        float iconSize = 0.5f;

        // 적 출현 아이콘
        if (hasEnemies) {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(position + new Vector3(-iconSize, 0, 0), iconSize);
        }

        // 아이템 상자 아이콘
        if (hasItemChest) {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(position + new Vector3(iconSize, 0, 0), Vector3.one * iconSize);
        }

        // 상인 아이콘
        if (hasMerchant) {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(position + new Vector3(0, 0, iconSize), iconSize);
        }

        // 보스 아이콘
        if (hasBoss) {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(position + new Vector3(0, 0, -iconSize), iconSize);
        }

        // 방 타입 색상으로 전체 방 표시
        Gizmos.color = roomColor;
        Gizmos.DrawWireCube(position, transform.localScale);
    }
} 
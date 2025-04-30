using UnityEngine;

[System.Serializable]
public class DungeonGenerationOptions {
    [Range(0, 100)]
    [SerializeField] private int enemySpawnChance = 50;    // 적 출현 확률 (0~100%)
    [Range(0, 100)]
    [SerializeField] private int itemChestSpawnChance = 30; // 아이템 상자 출현 확률 (0~100%)
    [SerializeField] private bool spawnMerchant = false;    // 상인 출현 여부
    [SerializeField] private bool spawnBoss = false;        // 보스 출현 여부

    public int EnemySpawnChance => enemySpawnChance;
    public int ItemChestSpawnChance => itemChestSpawnChance;
    public bool SpawnMerchant => spawnMerchant;
    public bool SpawnBoss => spawnBoss;

    public DungeonGenerationOptions(
        int enemyChance = 50,
        int itemChestChance = 30,
        bool merchant = false,
        bool boss = false
    ) {
        enemySpawnChance = Mathf.Clamp(enemyChance, 0, 100);
        itemChestSpawnChance = Mathf.Clamp(itemChestChance, 0, 100);
        spawnMerchant = merchant;
        spawnBoss = boss;
    }
} 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
// (Đã xóa using UnityEngine.AI;)

//SINH SẢN QUÁI
public enum EndCause {
    Win,
    PlayerLoss,
    OilDrillLoss
}

public class LevelManager : MonoBehaviour
{
    public static EndCause endCause;

    #region overhead

    [System.Serializable]
    public struct EnemyData{
        public GameObject enemyPrefab;
        [Range(1, 20)] public int spawnWeight;

        [HideInInspector]
        public int trueSpawnWeight; 
    }

    [Header("Core")]
    public float initialPrepTime = 90f;
    public float timeBetweenWaves = 45f;
    public int numberOfWaves = 10;
    [SerializeField] EnemyData[] enemyData;
    [SerializeField] int enemiesPerWave = 10;
    [SerializeField] int enemyMax = 60;

    [Header("Scene Managment")]
    [SerializeField] string endScene = "EndScene";

    [Header("Enemy Level Scale")]
    public int enemyLevel = 1;
    [Range(1, 2)][SerializeField] float waveIncreasePerLevel = 1.2f;
    [Range(1, 2)][SerializeField] float hpIncreasePerLevel = 1.1f;
    [Range(1, 2)][SerializeField] float defenseIncreasePerLevel = 1.05f;
    [Range(1, 2)][SerializeField] float attackIncreasePerLevel = 1.2f;

    // (Đã xóa Header "Spawning" và biến 'navMeshSearchRadius')

    float currentwaveIncrease = 1f;
    float currentHpIncrease = 1f;
    float defenseIncrease = 1f;
    float attackIncreaseIncrease = 1f;

    //management stuff
    Queue<GameObject> enemyQue;
    bool enqueuingNextWave = true;
    bool spawningNewEnemies = false;
    bool firstEnemyOfWave = true;
    int sumWeight = 0;

    GridController grid = null; // Đây là GridController, nhưng logic dưới dùng 'flowField'
    Vector2 centerPos = Vector2.zero;

    //enemy variables
    OilDrill oilDrill;
    GridController flowField; // <-- Sẽ dùng biến này để kiểm tra
    PlayerMovement player;
    Player playerStats;
    
    [HideInInspector]
    public int waveCount = 0;
    [HideInInspector]
    public int enemyCount = 0;

    SupplyDropSpawner supplyDropSpawner;

    #endregion

    // Start is called before the first frame update
    void Start(){
        grid = GameObject.FindObjectOfType<GridController>();
        centerPos = (Vector2) grid.gridSize * grid.cellRadius;

        oilDrill          = GameObject.FindObjectOfType<OilDrill>();
        flowField         = GameObject.FindObjectOfType<GridController>(); // Gán 'flowField'
        player            = GameObject.FindObjectOfType<PlayerMovement>();
        playerStats       = GameObject.FindObjectOfType<Player>();
        supplyDropSpawner = GameObject.FindObjectOfType<SupplyDropSpawner>();

        //create enemy que
        enemyQue = new Queue<GameObject>();

        //get max weight
        UpdateMaxWeight();

        StartCoroutine(spawnEnemyTimer(initialPrepTime));
    }

    void Update(){
        CheckGameStates();
        EnqueueEnemies();
        SpawnEnemies(); // <-- Hàm này đã được cập nhật
    }

    IEnumerator spawnEnemyTimer(float time){
        yield return new WaitForSeconds(time);
        if (spawningNewEnemies) StartCoroutine(spawnEnemyTimer(1f));
        else{
            spawningNewEnemies = true;
            firstEnemyOfWave = true;
            if (waveCount + 1 < numberOfWaves) StartCoroutine(spawnEnemyTimer(timeBetweenWaves));
        }
    }

    // ##### HÀM ĐÃ ĐƯỢC CẬP NHẬT ĐỂ DÙNG GRID CONTROLLER #####
    void SpawnEnemies(){
        // Nếu không phải lúc sinh quái, hoặc số lượng quái đã đạt tối đa, thì dừng
        if (!spawningNewEnemies || enemyCount > enemyMax) return;

        // Nếu hàng đợi rỗng, kết thúc đợt sinh quái hiện tại
        if (enemyQue.Count == 0){
            spawningNewEnemies = false;
            enqueuingNextWave = true;
            return;
        }

        // 1. Đảm bảo GridController (flowField) đã khởi tạo xong
        // (Trong GridController, biến 'initialized' được đặt thành true)
        if (flowField == null || !flowField.initialized || flowField.curFlowField == null) {
            return; // Grid chưa sẵn sàng, đợi frame sau
        }

        // 2. Vẫn tính toán vị trí sinh ngẫu nhiên (newPos) ở rìa bản đồ
        float randomDirection = Random.Range(0, 2 * Mathf.PI);
        Vector3 randomSpawnPos = new Vector3(Mathf.Cos(randomDirection) * centerPos.x, 0, Mathf.Sin(randomDirection) * centerPos.y) + new Vector3(centerPos.x, 5, centerPos.y);

        // 3. Kiểm tra vị trí này với "GridController" (FlowField)
        // (Hàm 'GetCellFromWorldPos' phải tồn tại trong class FlowField của bạn)
        Cell cell = flowField.curFlowField.GetCellFromWorldPos(randomSpawnPos);

        // 4. Chỉ sinh quái nếu ô đó "đi được" (cost thấp)
        // (Dựa trên logic OnDrawGizmos, vật cản có cost >= 255)
        if (cell != null && cell.cost < 255) 
        {
            // Vị trí an toàn! Bắt đầu sinh quái
            GameObject enemyPrefab = enemyQue.Dequeue(); // Lấy quái ra khỏi hàng đợi
            
            // Đặt quái xuống mặt đất (y=0) tại vị trí ngẫu nhiên đã được xác nhận là hợp lệ
            randomSpawnPos.y = 0; 
            
            var inst = Instantiate(enemyPrefab, randomSpawnPos, Quaternion.identity);
            inst.transform.parent = null;

            // Gán giá trị cho quái
            Enemy enemy       = inst.GetComponent<Enemy>();
            enemy.target      = oilDrill.transform.gameObject;
            enemy.flowField   = flowField; // Gán GridController cho quái
            enemy.playerStats = playerStats;
            enemy.player      = player;
            enemy.levelManager = this;
            enemy.SetHealth(enemy.currentHealth * currentHpIncrease);
            enemy.attackPower   *= attackIncreaseIncrease;
            enemy.defense       *= defenseIncrease;

            enemyCount++;

            // Logic cho quái đầu tiên (Giữ nguyên)
            if (firstEnemyOfWave){
                waveCount++;
                Player.WavesCompleted += 1;
                enemyLevel++;

                currentwaveIncrease    *= waveIncreasePerLevel;
                currentHpIncrease      *= hpIncreasePerLevel;
                defenseIncrease        *= defenseIncreasePerLevel;
                attackIncreaseIncrease *= attackIncreasePerLevel;

                firstEnemyOfWave = false;

                supplyDropSpawner.SpawnBeforeWave();
            }
        }
        else
        {
            // 5. NẾU KHÔNG TÌM THẤY VỊ TRÍ HỢP LỆ (ô này là vật cản)
            // Bỏ qua và thử lại ở frame sau với 1 vị trí ngẫu nhiên khác.
            // Quái vật vẫn an toàn trong Queue vì ta chưa gọi Dequeue.
            return;
        }
    }
    // ##### KẾT THÚC HÀM CẬP NHẬT #####


    void EnqueueEnemies(){
        if (!enqueuingNextWave) return;

        bool found = false;
        int maxLoop = Mathf.Min(enemyData.Length, waveCount + 1);
        int tempSumWeight = sumWeight;

        for (int i = enemyData.Length - 1; i >= maxLoop; --i){ // Sửa: i > maxLoop thành i >= maxLoop để an toàn hơn
            tempSumWeight -= enemyData[i].spawnWeight;
        }

        // Đảm bảo tempSumWeight không âm nếu có lỗi logic
        if (tempSumWeight <= 0 && enemyData.Length > 0) {
            tempSumWeight = enemyData[0].spawnWeight;
        } else if (tempSumWeight <= 0) {
            return; // Không có enemyData nào
        }

        int ranEnemyRange = Random.Range(0, tempSumWeight);

        for (int i = 0; i < maxLoop; ++i){
            if (ranEnemyRange < enemyData[i].trueSpawnWeight){
                enemyQue.Enqueue(enemyData[i].enemyPrefab);
                found = true;
                break;
            }
        }

        if (!found && enemyData.Length > 0) enemyQue.Enqueue(enemyData[0].enemyPrefab);
        if (enemyQue.Count >= enemiesPerWave * currentwaveIncrease) enqueuingNextWave = false;
    }

    void CheckGameStates(){
        if (enemyCount == 0 && waveCount >= numberOfWaves && !spawningNewEnemies) { // Win
            playerStats.CleanUp();
            endCause = EndCause.Win;
            Player.WavesCompleted += 1;
            SceneManager.LoadScene(endScene);
        }
        else if (playerStats != null && playerStats.currentHealth <= 0) { // Lose via Player Health
            playerStats.CleanUp();
            endCause = EndCause.PlayerLoss;
            SceneManager.LoadScene(endScene);
        }
        else if (oilDrill != null && oilDrill.currentHealth <= 0) { // Lose via Oil Drill
            if(playerStats != null) playerStats.CleanUp();
            endCause = EndCause.OilDrillLoss;
            SceneManager.LoadScene(endScene);
        }
    }

    void UpdateMaxWeight(){
        sumWeight = 0;
        for (int i = 0; i < enemyData.Length; ++i){
            sumWeight += enemyData[i].spawnWeight;
            enemyData[i].trueSpawnWeight = sumWeight;
        }
    }

    public void EnemyKilled(){
        enemyCount--;

        if (enemyCount <= 0) {
            supplyDropSpawner.SpawnAfterWave();
        }
    }
}
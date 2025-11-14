using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Photon.Pun; // <-- PHOTON

public class CrawlerEnemyRanged : Enemy
{
    [Header("Agro Vars")]
    [SerializeField]float agroDistance = 10f;
    [SerializeField]float attackDistance = 1f;
    [SerializeField]float damping = 0.98f;

    NavMeshAgent navMeshAgent;
    Rigidbody rb;
    Vector3 acculmulatedSpeed = Vector3.zero;

    float agroRangeSqr = 0;
    float attackRangeSqr = 0;
    public float coolDown = 0;
    public float coolDownMax = 2f;

    public float currentTargetDist = 0;
    float currentPlayerDist = 0;
    bool isOil;

    [Header("Projectile")]
    // <-- PHOTON: CẦN PREFAB CỦA ĐẠN (Phải nằm trong thư mục Resources)
    [SerializeField] GameObject projectilePrefab;
    [SerializeField]float projectileSpeed = 3f;
    [SerializeField]float steeringSpeed = 1f;
    [SerializeField]Vector2 randomSpawnTimerRange = Vector2.one;

    LayerMask impassableMask;

    // <-- PHOTON: Biến để lưu trữ người chơi
    private PlayerMovement[] allPlayers;

    new void Start()
    {
        base.Start();
        
        rb = GetComponent<Rigidbody>();
        agroRangeSqr = agroDistance * agroDistance;
        attackRangeSqr = attackDistance * attackDistance;
        
        navMeshAgent = GetComponentInChildren<NavMeshAgent>();
        navMeshAgent.speed = moveSpeed;

        // <-- PHOTON: Chỉ Master Client mới bật NavMesh
        navMeshAgent.enabled = photonView.IsMine;

        impassableMask = LayerMask.GetMask("Impassible Terrain");
        
        // <-- PHOTON: Chỉ Master Client mới bắt đầu Coroutine
        if (photonView.IsMine)
        {
            StartCoroutine(RandomShot(Random.Range(randomSpawnTimerRange.x, randomSpawnTimerRange.y)));
        }
    }

    private void Update() {
        // <-- PHOTON: CÂU LỆNH VÀNG
        if (!photonView.IsMine)
        {
            return;
        }

        base.Update();
        
        // <-- PHOTON: Tìm Player gần nhất
        FindClosestPlayer();

        //update distances (thêm kiểm tra null)
        if (target != null)
            currentTargetDist = (target.transform.position - transform.position).sqrMagnitude;
        if (player != null)
            currentPlayerDist = (player.transform.position - transform.position).sqrMagnitude;
        else
            currentPlayerDist = float.MaxValue;


        if (player == null && ((state == STATE.AGRO_PLAYER) || (state == STATE.ATTACKING_PLAYER))){
            state = STATE.AGRO_OIL;
        }

        switch(state){
            case STATE.AGRO_DISTRACTION:    MoveTowardsAttractItem(); break;
            case STATE.AGRO_OIL:            MoveTowardsTarget(); break;
            case STATE.AGRO_PLAYER:         MoveTowardsPlayer(); break;
            case STATE.ATTACKING_OIL:       AttackOilDrill(); break;
            case STATE.ATTACKING_PLAYER:    AttackPlayer(); break;
            case STATE.DEAD:                Stop(); break;
        }

        if (coolDown >= 0)
            coolDown -= Time.deltaTime;
    }

    // <-- PHOTON: HÀM TÌM PLAYER GẦN NHẤT
    void FindClosestPlayer()
    {
        allPlayers = FindObjectsOfType<PlayerMovement>(); 
        float closestDistance = float.MaxValue;
        PlayerMovement closestPlayer = null;

        foreach (PlayerMovement p in allPlayers)
        {
            if (p != null && p.enabled) 
            {
                float distance = (p.transform.position - transform.position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlayer = p;
                }
            }
        }

        if (closestPlayer != null) {
            this.player = closestPlayer;
            this.playerStats = closestPlayer.GetComponent<Player>();
        }
        else {
            this.player = null;
            this.playerStats = null;
        }
    }

    void AttackPlayer(){
        Stop();
        isOil = false;

        if (coolDown < 0) {
            //exit condition first
            if (currentPlayerDist > attackRangeSqr && currentPlayerDist < agroRangeSqr)
                state = STATE.AGRO_PLAYER;
            else if (currentPlayerDist > agroRangeSqr)
                state = STATE.AGRO_OIL;
        }

        if (player == null) return; // <-- PHOTON check

        Vector3 lookVector = new Vector3(player.transform.position.x - transform.position.x, 0, player.transform.position.z - transform.position.z);
        transform.rotation = Quaternion.LookRotation(lookVector, Vector3.up);
    }

    void AttackOilDrill(){
        Stop();
        isOil = true;

        if (coolDown < 0) {
            //exit condition first
            if (isDistracted && currentTargetDist > agroRangeSqr){
                isDistracted = false;
                state = STATE.AGRO_DISTRACTION;
            }

            if (currentTargetDist > agroRangeSqr)
                state = STATE.AGRO_OIL;
        }

        Vector3 lookVector = new Vector3(target.transform.position.x - transform.position.x, 0, target.transform.position.z - transform.position.z);
        transform.rotation = Quaternion.LookRotation(lookVector, Vector3.up);
    }

    void MoveTowardsTarget(){
        //exit condition
        if (currentPlayerDist < agroRangeSqr)
            state = STATE.AGRO_PLAYER;
        else if (target != null && currentTargetDist < attackRangeSqr)
            state = STATE.ATTACKING_OIL;
        else if (flowField != null && flowField.initialized){
            Cell occupideCell = flowField.curFlowField.GetCellFromWorldPos(transform.position);
            Vector3 moveDirection;

            navMeshAgent.speed = 0;
            moveDirection = new Vector3(occupideCell.bestDirection.x, 0, occupideCell.bestDirection.y);
            Vector3 lookVector = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            transform.rotation = Quaternion.LookRotation(lookVector, Vector3.up);

            acculmulatedSpeed *= damping;
            acculmulatedSpeed += moveDirection * moveSpeed * Time.fixedDeltaTime;
            acculmulatedSpeed = Vector3.ClampMagnitude(acculmulatedSpeed, maxMoveSpeed);
            rb.linearVelocity = new Vector3(acculmulatedSpeed.x, rb.linearVelocity.y, acculmulatedSpeed.z);
        }
        else{
            Debug.Log("Flow Field not Initialized");
        }
    }
    
    void MoveTowardsAttractItem() {
        if (target != null && currentTargetDist < attackRangeSqr){
            isDistracted = true;
            state = STATE.ATTACKING_OIL;
        }
        else{
            navMeshAgent.speed = moveSpeed;
            navMeshAgent.destination = target.transform.position;
            rb.linearVelocity = Vector3.zero;
            acculmulatedSpeed = Vector3.zero;

            Vector3 lookVector = new Vector3(target.transform.position.x - transform.position.x, 0, target.transform.position.z - transform.position.z);
            transform.rotation = Quaternion.LookRotation(lookVector, Vector3.up);
        }
    }

    void MoveTowardsPlayer(){
        if (player == null) { // <-- PHOTON check
            state = STATE.AGRO_OIL;
            return;
        }

        //exit condition
        if (currentPlayerDist > agroRangeSqr)
            state = STATE.AGRO_OIL;
        else if (currentPlayerDist < attackRangeSqr)
            state = STATE.ATTACKING_PLAYER;
        else{
            navMeshAgent.speed = moveSpeed;
            navMeshAgent.destination = player.transform.position;
            rb.linearVelocity = Vector3.zero;
            acculmulatedSpeed = Vector3.zero;

            Vector3 lookVector = new Vector3(player.transform.position.x - transform.position.x, 0, player.transform.position.z - transform.position.z);
            transform.rotation = Quaternion.LookRotation(lookVector, Vector3.up);
        }
    }

    // <-- PHOTON: GỌI HÀM NÀY TỪ ANIMATION EVENT
    public void FireProjectile()
    {
        if (!photonView.IsMine) return; // Chỉ Master mới bắn
        if (projectilePrefab == null) {
            Debug.LogError("Chưa gán Prefab Đạn (Projectile Prefab)!");
            return;
        }

        // Tạo viên đạn qua mạng
        GameObject projectileGO = PhotonNetwork.Instantiate(
            projectilePrefab.name, 
            transform.position + (transform.forward * 1.5f), // Vị trí spawn
            transform.rotation
        );
        
        // Gọi hàm Setup để gán mục tiêu, v.v.
        SetupProjectile(projectileGO);
    }

    // <-- PHOTON: Hàm này setup viên đạn sau khi nó được Instantiate
    public void SetupProjectile(GameObject projectile){
        EnemyProjectile ep = projectile.GetComponent<EnemyProjectile>();
        if (ep == null) return;

        // Quyết định ID mục tiêu
        int targetViewID = -1;
        if (isOil && target != null)
        {
            PhotonView targetPV = target.GetComponent<PhotonView>();
            if (targetPV != null) targetViewID = targetPV.ViewID;
        }
        else if (!isOil && player != null)
        {
            PhotonView targetPV = player.GetComponent<PhotonView>();
            if (targetPV != null) targetViewID = targetPV.ViewID;
        }

        // Gửi RPC cho viên đạn để set mục tiêu
        if (targetViewID != -1)
        {
            PhotonView projectilePV = projectile.GetComponent<PhotonView>();
            projectilePV.RPC("RPC_SetTarget", RpcTarget.All, targetViewID, attackPower, projectileSpeed, steeringSpeed);
        }
        else
        {
            PhotonNetwork.Destroy(projectile); // Không có mục tiêu hợp lệ, hủy đạn
        }

        coolDown = coolDownMax;

        //change state
        if (isDistracted && currentTargetDist < agroRangeSqr)
            state = STATE.ATTACKING_OIL;
        else if (player != null && currentPlayerDist < agroRangeSqr)
            state = STATE.AGRO_PLAYER;
    }

    void Stop(){
        if (rb) rb.linearVelocity = Vector3.zero;
        acculmulatedSpeed = Vector3.zero;
        if (navMeshAgent.enabled) navMeshAgent.speed = 0;
    }

    IEnumerator RandomShot(float time){
        // <-- PHOTON: Thêm kiểm tra
        if (!photonView.IsMine) yield break;

        yield return new WaitForSeconds(time);

        if (state == STATE.AGRO_PLAYER && player != null){
            Vector3 dir = player.transform.position - transform.position;
            if (!Physics.Raycast(transform.position, dir, dir.magnitude, impassableMask)){
                state = STATE.ATTACKING_PLAYER;
                coolDown = coolDownMax * 2;
            }
        }

        StartCoroutine(RandomShot(Random.Range(randomSpawnTimerRange.x, randomSpawnTimerRange.y)));
    }
}
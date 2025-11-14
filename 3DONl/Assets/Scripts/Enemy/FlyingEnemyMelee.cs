using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Photon.Pun; // <-- PHOTON

public class FlyingEnemyMelee : Enemy
{
    [Header("Flying Vars")]
    [SerializeField]float agroDistance = 0f;
    [SerializeField]float attackDistance = 4f;
    [SerializeField]float attackDistanceOil = 7f;
    [SerializeField]float damping = 0.98f;
    [SerializeField]float maxFlyHeight = 10f;
    [SerializeField]float minFlyHeight = 3f;
    [SerializeField]float startUpwardDist = 20f;
    [SerializeField]float useNavMeshDist = 3f;

    [SerializeField] LayerMask groundMask;
    [SerializeField] Vector3 flyUpOffset = Vector3.up;

    public float currentTargetDist = 0;

    NavMeshAgent navMeshAgent;
    Rigidbody rb;
    BoxCollider bc;
    Vector3 acculmulatedSpeed = Vector3.zero;

    float agroRangeSqr = 0;
    float attackRangeSqr = 0;
    float coolDown = 0;
    float coolDownMax = 1f;
    float attackRangeOilSqr = 0;
    
    int impassableMask;
    float currentPlayerDist = 0;
    bool isOil;

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
        
        // <-- PHOTON: Chỉ Master Client (chủ sở hữu) mới bật NavMesh
        navMeshAgent.enabled = photonView.IsMine; 
        
        attackRangeOilSqr = attackDistanceOil * attackDistanceOil;
        bc = GetComponent<BoxCollider>();
        impassableMask = LayerMask.GetMask("Impassible Terrain");
    }

    private void Update() {
        // <-- PHOTON: CÂU LỆNH VÀNG
        // Nếu tôi không phải là Master Client, DỪNG LẠI.
        if (!photonView.IsMine)
        {
            return;
        }

        base.Update();
        
        // <-- PHOTON: Master Client phải liên tục tìm Player gần nhất
        FindClosestPlayer();

        //update distances (thêm kiểm tra null)
        if (target != null)
            currentTargetDist = (target.transform.position - transform.position).sqrMagnitude;
        if (player != null)
            currentPlayerDist = (player.transform.position - transform.position).sqrMagnitude;
        else
            currentPlayerDist = float.MaxValue; // Nếu không có player, coi như vô tận

        if (player == null && ((state == STATE.AGRO_PLAYER) || (state == STATE.ATTACKING_PLAYER))){
            state = STATE.AGRO_OIL;
        }

        switch(state){
            case STATE.AGRO_DISTRACTION:    MoveTowardsTarget(); break;
            case STATE.AGRO_OIL:            MoveTowardsTarget(); break;
            case STATE.AGRO_PLAYER:         MoveTowardsPlayer(); break;
            case STATE.ATTACKING_OIL:       AttackOilDrill(); break;
            case STATE.ATTACKING_PLAYER:    AttackPlayer(); break;
            case STATE.DEAD:                if (rb) rb.useGravity = true; break;
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
            if (p != null && p.enabled) // (Giả sử Player còn sống có Component 'PlayerMovement' enabled)
            {
                float distance = (p.transform.position - transform.position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlayer = p;
                }
            }
        }

        if (closestPlayer != null)
        {
            this.player = closestPlayer; // Gán player mục tiêu
            this.playerStats = closestPlayer.GetComponent<Player>(); // Gán stats mục tiêu
        }
        else
        {
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

        if (player == null) return; // <-- PHOTON: Thêm kiểm tra null

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
        if (currentPlayerDist < agroRangeSqr && state != STATE.AGRO_DISTRACTION && !isDistracted)
            state = STATE.AGRO_PLAYER;
        else if (target != null && currentTargetDist < attackDistanceOil * attackDistanceOil && coolDown < 0){
            if (state == STATE.AGRO_DISTRACTION) isDistracted = true;
            if (isDistracted && currentTargetDist > attackRangeSqr){MoveTowardsTargetNoStateCheck(); return;}
            state = STATE.ATTACKING_OIL;
        }
        else{
            MoveTowardsTargetNoStateCheck();
        }
    }

    void MoveTowardsTargetNoStateCheck(){
        Vector3 dir = (target.transform.position - transform.position).normalized;
        RaycastHit hit;
        Physics.Raycast(transform.position, Vector3.down, out hit, 1000, groundMask);
        bool toClosetoSolid = Physics.CheckBox(transform.position + bc.center, bc.size * 2.5f, Quaternion.identity, impassableMask);

        if (Physics.Raycast(transform.position, dir, startUpwardDist, impassableMask) || hit.distance < minFlyHeight || toClosetoSolid){
            dir.y = 1f;
            if (toClosetoSolid) transform.position += flyUpOffset * Time.deltaTime;
        }
        else if (hit.distance > maxFlyHeight){
            dir.y = -1f;
        }

        navMeshAgent.speed = 0;
        acculmulatedSpeed += dir * moveSpeed * Time.deltaTime;
        acculmulatedSpeed = Vector3.ClampMagnitude(acculmulatedSpeed, maxMoveSpeed);
        rb.linearVelocity = acculmulatedSpeed;

        Vector3 lookVector = new Vector3(target.transform.position.x - transform.position.x, 0, target.transform.position.z - transform.position.z);
        transform.rotation = Quaternion.LookRotation(lookVector, Vector3.up);
    }

    void MoveTowardsPlayer(){
        //state change
        if (player == null) { // <-- PHOTON check
            state = STATE.AGRO_OIL;
            return;
        }

        if (currentPlayerDist > agroRangeSqr)
            state = STATE.AGRO_OIL;
        else if (currentPlayerDist < attackRangeSqr && coolDown < 0)
            state = STATE.ATTACKING_PLAYER;
        else{
            Vector3 dir = (player.transform.position - transform.position).normalized;
            RaycastHit hit;

            Physics.Raycast(transform.position, Vector3.down, out hit, 1000, groundMask);
            bool toClosetoSolid = Physics.CheckBox(transform.position + bc.center, bc.size * 2.5f, Quaternion.identity, impassableMask);

            if (Physics.Raycast(transform.position, dir, startUpwardDist, impassableMask) || hit.distance < minFlyHeight || toClosetoSolid){
                if (toClosetoSolid) transform.position += flyUpOffset * Time.deltaTime;
                dir.y = 1f;
            }
            else if (hit.distance > maxFlyHeight){
                dir.y = -1f;
            }

            navMeshAgent.speed = 0;
            acculmulatedSpeed += dir * moveSpeed * Time.deltaTime;
            acculmulatedSpeed = Vector3.ClampMagnitude(acculmulatedSpeed, maxMoveSpeed);
            rb.linearVelocity = acculmulatedSpeed;

            Vector3 lookVector = new Vector3(player.transform.position.x - transform.position.x, 0, player.transform.position.z - transform.position.z);
            transform.rotation = Quaternion.LookRotation(lookVector, Vector3.up);
        }
    }

    public void DealDamage(){
        if (isOil && target != null){
            target.GetComponent<OilDrill>()?.TakeDamage(attackPower);
            target.GetComponent<Enemy>()?.TakeDamage(attackPower);
            state = STATE.AGRO_OIL;
        }
        else if (!isOil && player != null && currentPlayerDist < attackRangeSqr){
            // <-- PHOTON: THAY ĐỔI CÁCH GÂY SÁT THƯƠNG
            PhotonView playerPhotonView = player.GetComponent<PhotonView>();
            if (playerPhotonView != null)
            {
                // Script Player của bạn PHẢI có hàm [PunRPC] public void RPC_PlayerTakeDamage(float damage)
                playerPhotonView.RPC("RPC_PlayerTakeDamage", playerPhotonView.Owner, attackPower);
            }
            state = STATE.AGRO_PLAYER;
        }

        coolDown = coolDownMax;
    }

    void Stop(){
        if (rb) rb.linearVelocity = Vector3.zero;
        acculmulatedSpeed = Vector3.zero;
        if (navMeshAgent.enabled) navMeshAgent.speed = 0;
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun; // <-- PHOTON

// <-- PHOTON: Kế thừa từ MonoBehaviourPun
public class EnemyProjectile : MonoBehaviourPun
{
    public float projectileSpeed;
    public GameObject target; // Sẽ được set bằng RPC
    public Vector3 moveDirection;
    public float steeringSpeed = 1f;
    public float damage = 10f;

    Rigidbody rb;
    private float selfDestructTimer = 10f; // Tự hủy sau 10s

    void Start(){
        rb = GetComponent<Rigidbody>();

        // <-- PHOTON: Client không chạy vật lý, chỉ nhận vị trí
        if (!photonView.IsMine)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    // <-- PHOTON: Hàm này được gọi bởi Kẻ thù sau khi spawn
    [PunRPC]
    public void RPC_SetTarget(int targetViewID, float damage, float speed, float steering)
    {
        PhotonView targetPV = PhotonView.Find(targetViewID);
        if (targetPV != null)
        {
            this.target = targetPV.gameObject;
            this.damage = damage;
            this.projectileSpeed = speed;
            this.steeringSpeed = steering;
            this.moveDirection = (this.target.transform.position - transform.position).normalized;
        }
        else
        {
            // Không tìm thấy mục tiêu, tự hủy (chỉ Master mới có quyền)
            if (photonView.IsMine)
            {
                PhotonNetwork.Destroy(this.gameObject);
            }
        }
    }


    void Update()
    {
        // <-- PHOTON: CÂU LỆNH VÀNG
        // Chỉ Master Client mới tính toán đường đi của đạn
        if (!photonView.IsMine)
        {
            return;
        }

        // Đếm ngược tự hủy
        selfDestructTimer -= Time.deltaTime;
        if (selfDestructTimer <= 0)
        {
            PhotonNetwork.Destroy(this.gameObject);
            return;
        }

        if (target == null)
        {
            // Mục tiêu đã chết hoặc mất kết nối
            // Cho đạn bay thẳng
            rb.linearVelocity = moveDirection * projectileSpeed;
            return;
        }

        Vector3 playerPosition = target.transform.position;
        playerPosition.y += 0.75f; // Target player waist, not feet
        Vector3 directionToPlayer = playerPosition - transform.position;

        if (target != null){
            moveDirection = Vector3.RotateTowards(moveDirection, directionToPlayer, steeringSpeed * Time.deltaTime, 1f);
            moveDirection.Normalize();
        }

        rb.linearVelocity = moveDirection * projectileSpeed;
    }

    // <-- PHOTON: XỬ LÝ VA CHẠM (CHỈ TRÊN MASTER CLIENT)
    // Prefab đạn phải có Collider (IsTrigger = true) và Rigidbody
    private void OnTriggerEnter(Collider other)
    {
        // Chỉ Master Client (chủ sở hữu) mới xử lý va chạm
        if (!photonView.IsMine)
        {
            return;
        }

        // Kiểm tra xem có phải Player không
        // (Giả sử Player có script "Player" hoặc "PlayerMovement")
        Player playerHit = other.GetComponent<Player>(); 
        if (playerHit != null)
        {
            // Lấy PhotonView của Player bị trúng đạn
            PhotonView playerPV = other.GetComponent<PhotonView>();
            if (playerPV != null)
            {
                // Script Player của bạn PHẢI có hàm [PunRPC] public void RPC_PlayerTakeDamage(float damage)
                playerPV.RPC("RPC_PlayerTakeDamage", playerPV.Owner, damage);
            }
            
            // Phá hủy viên đạn này (cho mọi người)
            PhotonNetwork.Destroy(this.gameObject);
        }
        else if (other.gameObject.CompareTag("Environment")) // <-- VÍ DỤ: Nếu va vào tường
        {
            // Phá hủy đạn
            PhotonNetwork.Destroy(this.gameObject);
        }
    }
}
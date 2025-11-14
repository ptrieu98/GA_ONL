using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun; // <-- PHOTON: Thêm vào

public class EnemyCrawlerRangedAnimation : MonoBehaviour
{
    [SerializeField] CrawlerEnemyRanged enemy;
    
    // [SerializeField] GameObject projectile; // <-- PHOTON: Không cần nữa
    // [SerializeField] GameObject projectileSpawnPoint; // <-- PHOTON: Không cần nữa

    [SerializeField] AudioSource SpewSFX;
    [SerializeField] AudioSource DeathSFX;

    Animator animator;
    private PhotonView photonView; // <-- PHOTON: Thêm vào

    int state;
    bool attacking = false;
    bool dying = false;
    
    void Start(){
        animator = GetComponent<Animator>();
        photonView = enemy.GetComponent<PhotonView>(); // <-- PHOTON: Thêm vào
    }

    void LateUpdate() {
        // Code animator của bạn giữ nguyên
        if (enemy.state == Enemy.STATE.DEAD || enemy.currentHealth <= 0){
            animator.SetInteger("State", 2);

            if (!dying){
                dying = true;
                DeathSFX.pitch = Random.Range(0.9f, 1.1f);
                DeathSFX.Play();
            }
        }
        else if (enemy.state == Enemy.STATE.AGRO_OIL || enemy.state == Enemy.STATE.AGRO_PLAYER || enemy.state == Enemy.STATE.AGRO_DISTRACTION)
            animator.SetInteger("State", 0);
        else if (enemy.state == Enemy.STATE.ATTACKING_OIL || enemy.state == Enemy.STATE.ATTACKING_PLAYER)
            animator.SetInteger("State", 1);
    }

    public void AttackKeyFrame(){
        // <-- PHOTON: SỬA LỖI
        // Không tự Instantiate ở đây.
        // Chỉ cần gọi hàm FireProjectile() từ script chính.
        enemy.FireProjectile();

        SpewSFX.pitch = Random.Range(0.9f, 1.1f);
        SpewSFX.Play();
    }

    public void EndAttackKeyFrame(){
        attacking = false;
        
        // <-- PHOTON: Thêm kiểm tra
        if (photonView.IsMine)
        {
            // (Bạn không đổi state ở đây, nên không cần check)
        }
    }

    public void Disapear(){
        LevelManager levelManager = GameObject.FindObjectOfType<LevelManager>();
        if (levelManager != null) levelManager.EnemyKilled();
        
        // <-- PHOTON: SỬA LỖI
        // Chỉ Master Client mới có quyền hủy đối tượng
        if (photonView != null && photonView.IsMine) 
        {
            PhotonNetwork.Destroy(enemy.gameObject);
        }
    }
}
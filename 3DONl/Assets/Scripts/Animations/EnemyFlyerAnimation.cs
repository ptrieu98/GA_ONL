using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun; // <-- PHOTON: Thêm vào

public class EnemyFlyerAnimation : MonoBehaviour
{
    
    [SerializeField] FlyingEnemy enemy;
    [SerializeField] GameObject projectile; // (Biến này không còn cần thiết)
    [SerializeField] GameObject projectileSpawnPoint; // (Biến này không còn cần thiết)
    [SerializeField] AudioSource FlapSFX;
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
        // Code logic Animator của bạn giữ nguyên
        if (enemy.state == Enemy.STATE.DEAD || enemy.currentHealth <= 0){
            animator.SetInteger("State", 2);

            if (!dying){
                dying = true;
                DeathSFX.pitch = Random.Range(1.9f, 2.1f);
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
        // Không tự tạo đạn (Instantiate) ở đây
        // Chỉ cần gọi hàm FireProjectile() từ script chính
        enemy.FireProjectile();
    }

    public void EndAttackKeyFrame(){
        SpewSFX.pitch = Random.Range(0.9f, 1.1f);
        SpewSFX.Play();
        attacking = false;
        
        // <-- PHOTON: Chỉ Master Client mới có quyền đổi state
        if (photonView.IsMine) 
        {
            enemy.state = Enemy.STATE.AGRO_OIL;
        }
    }

    public void Flap(){
        FlapSFX.pitch = Random.Range(0.9f, 1.1f);
        FlapSFX.Play();
    }

    public void Disapear(){
        LevelManager levelManager = GameObject.FindObjectOfType<LevelManager>();
        if (levelManager != null) levelManager.EnemyKilled();
        
        // <-- PHOTON: SỬA LỖI
        // Chỉ Master Client (chủ sở hữu) mới có quyền hủy đối tượng
        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(enemy.gameObject);
        }
    }
}
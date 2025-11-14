using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun; // <-- PHOTON: Thêm vào

public class EnemyCrawlerAnimation : MonoBehaviour
{
    [SerializeField] CrawlerEnemy enemy; // Giả sử đây là script của quái cận chiến
    [SerializeField] AudioSource SpewSFX;
    [SerializeField] AudioSource DeathSFX;

    Animator animator;
    private PhotonView photonView; // <-- PHOTON: Thêm vào

    int state;
    bool attacking = false;
    bool dying = false;
    
    void Start()
    {
        animator = GetComponent<Animator>();
        state = (int)enemy.state;
        photonView = enemy.GetComponent<PhotonView>(); // <-- PHOTON: Thêm vào
    }

    void LateUpdate() {
        state = (int)enemy.state;

        // Code animator của bạn giữ nguyên
        if (enemy.state == Enemy.STATE.DEAD || enemy.currentHealth <= 0){
            int ran = Random.Range(0, 2);
            animator.SetInteger("Variation", ran);
            animator.SetInteger("State", 4);

            if (!dying){
                dying = true;
                DeathSFX.pitch = Random.Range(0.9f, 1.1f);
                DeathSFX.Play();
            }
        }
        else if (state < 3){
            int ran = Random.Range(0, 2);
            animator.SetInteger("Variation", ran);
            animator.SetInteger("State", 1);
        }
        else if (state < 5 && enemy.coolDown < 0){
            int ran = Random.Range(0, 2);
            animator.SetInteger("Variation", ran);
            animator.SetInteger("State", 3);
        }
    }

    public void AttackKeyFrame(){
        SpewSFX.pitch = Random.Range(0.9f, 1.1f);
        SpewSFX.Play();
        
        // Giữ nguyên, vì đây là quái cận chiến (Melee)
        enemy.DealDamage(); 
    }

    public void EndAttackKeyFrame(){
        attacking = false;
        animator.SetInteger("Variation", 2);
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
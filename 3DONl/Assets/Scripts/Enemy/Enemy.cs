using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun; // <-- PHOTON

// <-- PHOTON: Kế thừa từ MonoBehaviourPun và thêm IPunObservable
public class Enemy : MonoBehaviourPun, IPunObservable
{
    public enum STATE {
        AGRO_OIL = 0,
        AGRO_DISTRACTION,
        AGRO_PLAYER,
        ATTACKING_OIL,
        ATTACKING_PLAYER,
        DEAD,
    }

    [System.Serializable]
    public struct ItemDrop{
        public GameObject item; // QUAN TRỌNG: Prefab này phải nằm trong thư mục Resources
        public float chance;
    }

    [Header("Enemy Stats")]
    [SerializeField] protected float moveSpeed = 1f;
    [SerializeField] protected float maxMoveSpeed = 1f;
    [SerializeField] protected float attackSpeed = 1f;
    public float attackPower = 1f;
    public float defense = 0;
    public bool isEnemy = true;
    public bool isFlying = true;

    [HideInInspector]public GameObject target;
    [HideInInspector]public GridController flowField = null;
    [HideInInspector]public PlayerMovement player = null;
    [HideInInspector]public Player playerStats = null;
    [HideInInspector]public LevelManager levelManager = null;

    public STATE state = STATE.AGRO_OIL;

    public HealthBar healthBar;
    public float currentHealth;
    public float maxHealth = 100f;

    [Header("Drops (Organize in Order Please)")]
    [SerializeField] ItemDrop[] itemDrops;

    bool firstSetHealth = false; 
    public bool isDistracted = false;

    protected void Start() {
        SetHealth(maxHealth);
        healthBar.transform.gameObject.SetActive(false);

        // <-- PHOTON: Client (người chơi khác) không cần chạy vật lý
        if (!photonView.IsMine) // photonView.IsMine == true cho Master Client
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // Tắt vật lý, chỉ nhận dữ liệu
                rb.useGravity = false;
            }
        }
    }

    protected void Update() {
        CheckDeadState();
    }

    // <-- PHOTON: Đây là hàm mà Player (người chơi) sẽ gọi khi bắn trúng
    [PunRPC]
    public void RPC_TakeDamage(float amount, PhotonMessageInfo info)
    {
        // Chỉ Master Client (chủ sở hữu AI) mới có quyền xử lý trừ máu
        if (photonView.IsMine)
        {
            TakeDamage(amount, false);
        }
    }

    // Hàm TakeDamage gốc của bạn (đã sửa)
    public void TakeDamage(float amount, bool ignoreSound = false) {
        if (state == STATE.DEAD) return;

        float damage = Mathf.Max(1f, amount - defense);
        if (damage > currentHealth) {
            // Player.DamageDealt += currentHealth; // <-- CẢNH BÁO: Biến static sẽ KHÔNG đồng bộ
            currentHealth = 0;
        } else {
            // Player.DamageDealt += damage; // <-- CẢNH BÁO: Biến static sẽ KHÔNG đồng bộ
            currentHealth -= damage;
        }
        healthBar.transform.gameObject.SetActive(true);
        healthBar.SetHealth(currentHealth);

        if (!ignoreSound) SFXManager.instance.Play("Enemy Hurt", 1.4f, 1.7f);

        if (currentHealth <= 0) {
            healthBar.transform.gameObject.SetActive(false);

            Collider collider = GetComponent<Collider>();
            if (collider != null ) collider.enabled = false;
            state = STATE.DEAD;
            // Player.EnemiesKilled += 1; // <-- CẢNH BÁO: Biến static sẽ KHÔNG đồng bộ

            // <-- PHOTON: Chỉ Master Client mới được rớt đồ
            if (photonView.IsMine)
            {
                float ran = Random.Range(0f, 100f);
                foreach (ItemDrop item in itemDrops){
                    if (ran < item.chance){
                        // Dùng PhotonNetwork.Instantiate để mọi người cùng thấy
                        // Prefab item.item.name PHẢI nằm trong thư mục Resources
                        PhotonNetwork.Instantiate(item.item.name, transform.position, Quaternion.identity);
                        break;
                    }
                }
            }
        }
    }

    public void CheckDeadState(){
        if (currentHealth <= 0) state = STATE.DEAD;
    }

    public void GainHealth(float amount) {
        // <-- PHOTON: Chỉ Master Client mới có quyền hồi máu cho AI
        if (!photonView.IsMine) return;

        currentHealth += amount;
        if (currentHealth > maxHealth) {
            currentHealth = maxHealth;
        }
        healthBar.SetHealth(currentHealth);
    }

    public void SetHealth(float value) {
        currentHealth = value;
        healthBar.SetMaxHealth(value);
    }

    // <-- PHOTON: HÀM ĐỒNG BỘ MÁU VÀ STATE
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Tôi là Master Client: Tôi gửi dữ liệu đi
            stream.SendNext(currentHealth);
            stream.SendNext((int)state); // Gửi state dưới dạng số int
        }
        else
        {
            // Tôi là Client: Tôi nhận dữ liệu về
            this.currentHealth = (float)stream.ReceiveNext();
            this.state = (STATE)(int)stream.ReceiveNext();

            // Cập nhật UI máu khi nhận được
            healthBar.SetHealth(this.currentHealth);
            if(this.currentHealth < this.maxHealth && this.currentHealth > 0)
            {
                healthBar.transform.gameObject.SetActive(true);
            }
            else
            {
                healthBar.transform.gameObject.SetActive(false);
            }
        }
    }
}
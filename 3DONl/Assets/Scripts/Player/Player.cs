using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Photon.Pun; 

// Kế thừa MonoBehaviourPun và IPunObservable để đồng bộ
public class Player : MonoBehaviourPun, IPunObservable
{
    // Biến static (chỉ tính cục bộ)
    public static int WavesCompleted = 0;
    public static int EnemiesKilled = 0;
    public static float DamageDealt = 0f;
    public static float DamageTaken = 0f;

    [SerializeField] float itemGrav = 1f;
    [SerializeField] float itemRange = 2f;
    [SerializeField] float vignetteTime = 0.25f;
    [SerializeField] Volume damageVignette;
    [SerializeField] float vignetteSpeed = 10f;

    public float initialHealth = 100f;
    public float currentHealth;
    public float maxHealth;
    public float defense = 0f;
    public float damageMultiplier = 1f;

    // Các biến này sẽ tự tìm
    public HealthBar healthBar;
    public HotBar hotBar;
    public InventoryObject inventory;
    public CraftingObject crafting;
    private CameraSystem cameraSystem;

    const float ITEM_DROP_DISTANCE = 8f;

    bool hurtEffect = false;
    float hurtEffectLerp = 0;

    LayerMask itemMask;

    private void Start()
    {
        // Chỉ chạy setup cho Player CỦA MÌNH
        if (photonView.IsMine)
        {
            currentHealth = initialHealth;
            maxHealth = initialHealth;

            // Tự tìm các thành phần UI và Camera trong Scene
            healthBar = FindObjectOfType<HealthBar>();
            hotBar = FindObjectOfType<HotBar>();
            cameraSystem = FindObjectOfType<CameraSystem>();
            
            GameObject vignetteObj = GameObject.FindWithTag("DamageVignette");
            if(vignetteObj != null) damageVignette = vignetteObj.GetComponent<Volume>();

            if (healthBar != null)
            {
                healthBar.SetMaxHealth(initialHealth);
                healthBar.valueText.gameObject.SetActive(true);
            }
        }
        
        itemMask = LayerMask.GetMask("Ground Item");
    }

    void Update() {
        // Chỉ chạy logic trên máy chủ sở hữu
        if (!photonView.IsMine) return;

        // Hút item lại gần
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, itemRange, itemMask);
        foreach(Collider col in hitColliders){
            col.transform.parent.position += (transform.position - col.transform.parent.position).normalized * itemGrav * Time.deltaTime;
        }

        // Hiệu ứng màn hình đỏ khi đau
        if (hurtEffect){
            hurtEffectLerp += Time.deltaTime * vignetteSpeed;
            hurtEffectLerp = Mathf.Clamp(hurtEffectLerp, 0, 1);
            
            if(damageVignette != null)
                damageVignette.weight = Mathf.Cos(hurtEffectLerp * Mathf.PI * 2 + Mathf.PI) * 0.5f + 0.5f;

            if (hurtEffectLerp == 1){
                hurtEffect = false;
                if(damageVignette != null) damageVignette.gameObject.SetActive(false);
            }
        }
    }

    // --- RPC: XỬ LÝ SÁT THƯƠNG ---
    [PunRPC]
    public void RPC_PlayerTakeDamage(float damageAmount)
    {
        if (photonView.IsMine) TakeDamage(damageAmount);
    }

    public void TakeDamage(float amount)
    {
        float damage = Mathf.Max(1f, amount - defense);
        if (damage > currentHealth) {
            DamageTaken += currentHealth;
            currentHealth = 0;
        } else {
            DamageTaken += damage;
            currentHealth -= damage;
        }
        
        if (healthBar != null) {
            healthBar.SetHealth(currentHealth);
            healthBar.valueText.text = currentHealth.ToString("n0") + "/" + maxHealth.ToString("n0");
        }

        hurtEffect = true;
        hurtEffectLerp = 0;
        if(damageVignette != null) damageVignette.gameObject.SetActive(true);
        SFXManager.instance?.Play("Hurt");
    }

    // --- RPC: XÓA ITEM (Nhờ Master Client xóa hộ) ---
    [PunRPC]
    public void RPC_DestroyItem(int viewID)
    {
        if (!PhotonNetwork.IsMasterClient) return; // Chỉ Master mới được xóa

        PhotonView targetView = PhotonView.Find(viewID);
        if (targetView != null)
        {
            PhotonNetwork.Destroy(targetView.gameObject);
        }
    }

    public void PickUpItem(GroundItem groundItem) {
        inventory.AddItem(groundItem.item, groundItem.amount);
    }

    public void DropItem(InventorySlot slot) {
        // Tạo item qua mạng (Prefab phải ở trong Resources)
        GameObject inst = PhotonNetwork.Instantiate(slot.item.groundPrefab.name, Vector3.zero, Quaternion.identity);
        
        inst.GetComponent<GroundItem>().amount = slot.amount;
        
        Vector3 dropPosition = transform.position + (ITEM_DROP_DISTANCE * transform.forward); // Dùng transform.forward thay vì cameraSystem để an toàn
        if (cameraSystem != null && cameraSystem.getMainCamera() != null)
             dropPosition = transform.position + (ITEM_DROP_DISTANCE * cameraSystem.getMainCamera().transform.forward);

        dropPosition.y = 0.5f; 
        inst.transform.position = dropPosition;
    }

    public void OnTriggerEnter(Collider other) {
        if (!photonView.IsMine) return;

        if (other.gameObject.tag == "GroundItem") {
            GroundItem gi = other.gameObject.GetComponent<GroundItem>();
            SFXManager.instance?.Play(gi.sfx, 0.9f, 1.1f);
            PickUpItem(gi);
            
            if (gi.currentInfo != null) { Destroy(gi.currentInfo.gameObject); }
            
            // Xử lý xóa Item qua mạng
            PhotonView itemPV = other.GetComponent<PhotonView>();
            if (itemPV != null)
            {
                if (PhotonNetwork.IsMasterClient)
                    PhotonNetwork.Destroy(other.gameObject);
                else
                {
                    other.gameObject.SetActive(false); // Ẩn ngay lập tức
                    photonView.RPC("RPC_DestroyItem", RpcTarget.MasterClient, itemPV.ViewID); // Nhờ xóa
                }
            }
            else
            {
                Destroy(other.gameObject); // Item offline
            }
        } 
    }

    // Đồng bộ Máu qua mạng
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(currentHealth);
            stream.SendNext(maxHealth);
        }
        else
        {
            this.currentHealth = (float)stream.ReceiveNext();
            this.maxHealth = (float)stream.ReceiveNext();
        }
    }
    
    // Các hàm giữ nguyên
    public List<Enemy> GetLivingRobots() { return new List<Enemy>(); }
    public void IncreaseMaxHealth(float value) {
        maxHealth += value;
        healthBar.UpdateMaxHealth(maxHealth);
        healthBar.valueText.text = currentHealth.ToString("n0") + "/" + maxHealth.ToString("n0");
    }
    public void GainHealth(float amount) {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        healthBar.SetHealth(currentHealth);
        healthBar.valueText.text = currentHealth.ToString("n0") + "/" + maxHealth.ToString("n0");
    }
    public void CleanUp() {
        inventory.container.items = new InventorySlot[28];
        crafting.container.items = new InventorySlot[28];
    }
    private void OnApplicationQuit() { CleanUp(); }
}
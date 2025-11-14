using UnityEngine;
using Photon.Pun; // <-- THÊM THƯ VIỆN PHOTON

// Giả sử script này nằm trên cùng GameObject với PhotonView,
// hoặc là con của một GameObject có PhotonView
public class ResourceBeam : UsableItem
{
    [Header("Mechanics")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float range = 50f;
    [SerializeField] private float frequency = 1f;

    [Header("Effects")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform firePoint;
    [SerializeField] private ParticleSystem impactEffect;
    
    private InventoryObject inventory;
    private Camera mainCamera; // Sẽ được gán là Camera.main
    private int layers;

    private float coolDownTime = 0;
    private bool playingSound = false;

    private PhotonView photonView; // <-- THÊM VÀO

    protected override void Init() {
        inventory = Resources.Load<InventoryObject>("Inventory/PlayerInventory");
        
        // Lấy PhotonView từ cha (vì item này là con của Player)
        photonView = GetComponentInParent<PhotonView>();
        
        // XÓA DÒNG GÂY LỖI
        // mainCamera = GameObject.FindObjectOfType<CameraSystem>().getMainCamera();
        
        layers = LayerMask.GetMask("Player");
        ShowCrosshair();
    }

    private void Update() {
        
        // ===== BƯỚC SỬA LỖI MULTIPLAYER =====
        // Chỉ Player "của tôi" (IsMine) mới chạy code Update (bắn, âm thanh)
        if (photonView == null || !photonView.IsMine)
        {
            // Tắt hiệu ứng (line renderer, sound) trên máy của người khác
            if (lineRenderer.enabled)
            {
                lineRenderer.enabled = false;
                impactEffect.Stop();
                SFXManager.instance.Stop("Beam");
            }
            playingSound = false;
            return;
        }
        // ======================================


        // ===== BƯỚC SỬA LỖI KEYNOTFOUND =====
        // Tìm camera một cách an toàn (chỉ chạy cho Player "của tôi")
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("ResourceBeam: Không tìm thấy Camera.main! Đảm bảo Camera của Player đã được gắn Tag 'MainCamera'.");
                return; // Đợi frame sau
            }
        }
        // ======================================

        
        coolDownTime += Time.deltaTime;
        if (InventoryCanvas.InventoryIsOpen || PauseMenu.GameIsPaused) 
        {  
            SFXManager.instance.Stop("Beam");
            playingSound = false;
            lineRenderer.enabled = false;
            impactEffect.Stop();
            return; 
        }
        
        if (Input.GetButtonDown("Fire2")) { Focus(); }
        if (Input.GetButton("Fire1")) {
            Use();
            if (!playingSound){
                SFXManager.instance.Play("Beam", 0.95f, 1.05f, true);
                playingSound = true;
            }
        }
        else {
            if (lineRenderer.enabled)
            {
                SFXManager.instance.Stop("Beam");
                playingSound = false;
                lineRenderer.enabled = false;
                impactEffect.Stop();
            }
        }
    }

    protected override void Use() {
        Shoot();
    }

    private void Shoot() {
        RaycastHit hit;
        if (Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out hit, range, ~layers))
        {
            Laser(hit.point);
            if (coolDownTime >= frequency) 
            {
                coolDownTime = 0;
                ResourceNode rNode = hit.transform.GetComponent<ResourceNode>();
                if (rNode == null) 
                {
                    impactEffect.Stop();
                    return;
                }
                
                // CẢNH BÁO: Logic này cần được sửa thành RPC
                // Tạm thời, nó sẽ chỉ hoạt động trên máy của bạn
                int amount = rNode.harvest(damage);
                
                if (amount > 0)
                {
                    impactEffect.Play();
                    inventory.AddItem(rNode.item, amount); // Thêm item (OK)
                } 
                else 
                    impactEffect.Stop();
            }
        } 
        else
        {
            Vector3 target = firePoint.position + firePoint.forward * range;
            Laser(target);
            impactEffect.Stop();
        }
    }

    private void Laser(Vector3 target) {
        if (!lineRenderer.enabled)
        {
            lineRenderer.enabled = true;
        }
        lineRenderer.SetPosition(0, firePoint.position);
        lineRenderer.SetPosition(1, target);
        
        Vector3 direction = firePoint.position - target;

        impactEffect.transform.position = target + direction.normalized;
        impactEffect.transform.rotation = Quaternion.LookRotation(direction);
    }
}
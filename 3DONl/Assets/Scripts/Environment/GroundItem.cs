using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun; // <-- THÊM THƯ VIỆN PHOTON

// <-- BẮT BUỘC PHẢI CÓ PHOTONVIEW
[RequireComponent(typeof(PhotonView))] 
public class GroundItem : MonoBehaviour
{
    public ItemObject item;
    public int amount = 1;
    public string sfx = "Normal Pickup";
    public GameObject infoPrefab;
    public GameObject currentInfo = null;

    [Range(0,1)][SerializeField] private float spawnRate = 1f;
    [SerializeField] private float pickupRange = 5f;

    [Header("Seperations")]
    public float stayAwayDist = 1f;
    public float moveAwaySpeed = 1f;

    // private Player player; // <-- SỬA LẠI
    private Player localPlayer; // Đổi tên để rõ nghĩa hơn
    private PhotonView photonView; // <-- THÊM VÀO

    private LayerMask mask;
    private LayerMask ground;

    // Bounce Effect
    private float hoverRate = 0.5f;
    private float highestOffset = 0.6f;
    private float startingZ;
    private float time = 0;

    private bool grounded = false;
    private float pickupRangeSqr;

    void Start() {
        // player = GameObject.FindObjectOfType<Player>(); // <-- XÓA DÒNG NÀY
        photonView = GetComponent<PhotonView>(); // <-- THÊM DÒNG NÀY

        mask = LayerMask.GetMask("Ground Item");
        ground = LayerMask.GetMask("Ground");

        startingZ = transform.position.y;
        time = Random.Range(0f, 1f);
        grounded = false;
        pickupRangeSqr = pickupRange * pickupRange;

        // Chỉ Master Client mới có quyền quyết định spawn hay không
        if (PhotonNetwork.IsMasterClient)
        {
            if (UnityEngine.Random.Range(0f, 1f) > spawnRate) 
            { 
                PhotonNetwork.Destroy(transform.gameObject); 
            }
        }
    }

    void Update() {
        
        // ===== BƯỚC SỬA LỖI NULLREFERENCE =====
        // 1. Nếu chưa tìm thấy Player của mình (localPlayer)
        if (localPlayer == null)
        {
            // 2. Tìm tất cả các Player trong scene
            Player[] allPlayers = FindObjectsOfType<Player>();
            foreach (Player p in allPlayers)
            {
                // 3. Nếu Player này là "của tôi" (IsMine)
                if (p.GetComponent<PhotonView>().IsMine)
                {
                    localPlayer = p; // Lưu lại để dùng
                    break; 
                }
            }
            
            // 4. Nếu vẫn chưa tìm thấy (Player chưa spawn xong), thì DỪNG
            if (localPlayer == null)
                return;
        }
        // ======================================

        // Dòng 50 (cũ): Giờ đã an toàn.
        float currentPlayerDist = (localPlayer.transform.position - transform.position).sqrMagnitude;
        
        if (currentPlayerDist <= pickupRangeSqr) {
            // Info Popup
            if (currentInfo == null) {
                currentInfo = Instantiate(infoPrefab, new Vector3(transform.position.x, startingZ + 2f, transform.position.z), Quaternion.identity);
                currentInfo.GetComponent<DisplayGroundItemInfo>().SetUp(item, amount);
            }

            // E to Pickup
            if (Input.GetKeyDown(KeyCode.E)) {
                if (PauseMenu.GameIsPaused) return;
                
                // Gọi hàm PickUpItem trên Player CỦA MÌNH
                localPlayer.PickUpItem(this);
                
                if (currentInfo != null) { Destroy(currentInfo.gameObject); }
                
                // ===== BƯỚC SỬA LỖI MẠNG =====
                // Phải dùng PhotonNetwork.Destroy để xóa item này trên máy mọi người
                PhotonNetwork.Destroy(transform.gameObject);
                // ==============================

                SFXManager.instance?.Play(sfx, 0.9f, 1.1f);
            }
        } else {
            if (currentInfo != null) { Destroy(currentInfo.gameObject); }
        }

        // ... (Code 'grounded' của bạn giữ nguyên)
    }

    void LateUpdate() {
        // ... (Code 'Bounce Effect' của bạn giữ nguyên)
    }
}
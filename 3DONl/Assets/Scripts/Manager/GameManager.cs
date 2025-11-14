using UnityEngine;
using Photon.Pun;

public class GameManager : MonoBehaviour
{
    // Kéo "Main Camera" của Scene vào đây
    public Camera sceneCamera; 

    // TÊN CHÍNH XÁC của file Prefab Player trong thư mục Resources
    // Ví dụ: file là "FirstPersonPlayer.prefab" thì điền "FirstPersonPlayer"
    public string playerPrefabName = "FirstPersonPlayer"; 

    public Transform spawnPoint;

    void Start()
    {
        // 1. Tắt camera của scene để dùng camera của Player
        if (sceneCamera != null)
        {
            sceneCamera.gameObject.SetActive(false);
        }

        // 2. Tính toán vị trí spawn ngẫu nhiên chút xíu để không bị trùng
        Vector3 pos = new Vector3(0, 2, 0); // Mặc định cao hơn đất
        if (spawnPoint != null) 
        {
            pos = spawnPoint.position;
            pos.x += Random.Range(-2f, 2f);
        }

        // 3. SPAWN QUA MẠNG (Quan trọng nhất)
        // PhotonNetwork.Instantiate sẽ tự động báo cho người cũ biết "có người mới vào"
        Debug.Log("GameManager: Đang tạo Player từ Resources/" + playerPrefabName);
        PhotonNetwork.Instantiate(playerPrefabName, pos, Quaternion.identity);
    }
}
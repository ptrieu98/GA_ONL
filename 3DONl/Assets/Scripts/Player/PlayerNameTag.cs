using UnityEngine;
using TMPro;
using Photon.Pun;
using System.Collections; // <-- THÊM THƯ VIỆN NÀY

public class PlayerNameTag : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    private PhotonView photonView;

    void Start()
    {
        photonView = GetComponent<PhotonView>();

        // 1. Vẫn kiểm tra xem ô Text đã được gán chưa
        if (nameText == null)
        {
            Debug.LogError("LỖI PlayerNameTag: Ô 'Name Text' (TextMeshPro) CHƯA ĐƯỢC KÉO THẢ vào Inspector!");
            return;
        }

        // 2. Bắt đầu một Coroutine để đợi Owner
        StartCoroutine(SetNameWhenReady());
    }

    // 3. Coroutine này sẽ chạy song song
    IEnumerator SetNameWhenReady()
    {
        // 4. Vòng lặp: "Nếu photonView.Owner còn bị null..."
        // (Thêm 1 bộ đếm an toàn 100 frame để tránh treo máy)
        int safetyCounter = 0;
        while (photonView.Owner == null && safetyCounter < 100)
        {
            // "...thì đợi 1 frame rồi kiểm tra lại"
            safetyCounter++;
            yield return null; 
        }

        // 5. Kiểm tra lại sau khi đã đợi
        if (photonView.Owner == null)
        {
            Debug.LogError("LỖI PlayerNameTag: Đã đợi 100 frame nhưng photonView.Owner VẪN BỊ NULL!");
        }
        else
        {
            // 6. Gán tên (Bây giờ đã an toàn)
            Debug.Log("PlayerNameTag: Owner đã sẵn sàng! Đang gán tên: " + photonView.Owner.NickName);
            nameText.text = photonView.Owner.NickName;
        }
    }
}
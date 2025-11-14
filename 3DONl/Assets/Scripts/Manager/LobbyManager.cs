using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro; 

public class LobbyManager : MonoBehaviourPunCallbacks
{
    public TMP_InputField playerNameInput; 
    public Button joinButton;
    public string gameSceneName = "MainScene"; // Đảm bảo tên này đúng

    void Start()
    {
        joinButton.interactable = false; 
        PhotonNetwork.AutomaticallySyncScene = true; // Rất quan trọng
        
        Debug.Log("Đang kết nối đến Photon...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Đã kết nối đến Master Server!");
        PhotonNetwork.JoinLobby(); 
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Đã tham gia Sảnh chờ (Lobby)!");
        joinButton.interactable = true;
    }

    public void OnJoinButtonClicked()
    {
        if (string.IsNullOrEmpty(playerNameInput.text))
        {
            Debug.LogError("Tên người chơi không được để trống!");
            return;
        }

        PhotonNetwork.NickName = playerNameInput.text;
        Debug.Log("Tên người chơi đã được lưu: " + PhotonNetwork.NickName);

        joinButton.interactable = false;
        Debug.Log("Đang tìm phòng...");
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("Không tìm thấy phòng, đang tạo phòng mới...");
        
        // ===== THÊM DÒNG NÀY VÀO =====
        Debug.LogWarning("### TEST CODE MỚI: Đã có Plugins = null! ###");
        // ================================

        // HÃY ĐẢM BẢO DÒNG NÀY CÓ CHỮ ", PLUGINS = NULL"
        PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = 4, Plugins = null });
    }

    // Hàm này dùng để bắt lỗi (như bạn thấy)
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"LỖI: Tạo phòng thất bại! Mã lỗi: {returnCode} - Tin nhắn: {message}");
        joinButton.interactable = true; // Cho phép thử lại
    }

    // Hàm này sẽ được gọi khi CreateRoom() thành công
    public override void OnJoinedRoom()
    {
        Debug.Log($"Đã vào phòng! Tên của tôi là: {PhotonNetwork.NickName}");
        
        // Tải Scene Game
        PhotonNetwork.LoadLevel(gameSceneName);
    }
}
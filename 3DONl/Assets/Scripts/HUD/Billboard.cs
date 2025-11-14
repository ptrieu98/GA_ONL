using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    // Biến này không cần nữa, nhưng chúng ta có thể giữ nó
    public Transform camera; 

    private void LateUpdate() 
    {
        // 1. Luôn tìm Camera.main
        //    (Camera.main là camera nào đang bật và có Tag "MainCamera")
        if (Camera.main != null)
        {
            camera = Camera.main.transform;
        }

        // 2. Nếu đã tìm thấy camera, thì xoay
        if (camera != null)
        {
            transform.LookAt(transform.position + camera.forward);
        }
        
        // Nếu Camera.main cũng null (vd: Player chưa spawn),
        // thì script sẽ không làm gì cả và không báo lỗi.
    }
}
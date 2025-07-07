using UnityEngine;

public class DragObject : MonoBehaviour
{
    // 存储鼠标点击点与物体中心点之间的偏移
    private Vector3 offset;
    // 用于将屏幕坐标转换到世界坐标的相机
    private Camera cam;

    void Start()
    {
        // 获取主相机
        cam = Camera.main;
    }

    // 当鼠标点击到对象时被调用
    void OnMouseDown()
    {
        // 获取当前物体在屏幕坐标中的深度值（Z 值）
        float zDistance = cam.WorldToScreenPoint(transform.position).z;
        // 将鼠标点击点（屏幕坐标）转换为世界坐标，同时保持同样深度
        Vector3 mouseWorldPoint = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, zDistance));
        // 计算偏移量，确保拖拽时物体不会瞬间跳到鼠标位置
        offset = transform.position - mouseWorldPoint;
    }

    // 当鼠标拖拽时持续被调用
    void OnMouseDrag()
    {
        // 同样获取鼠标当前在世界中的位置
        float zDistance = cam.WorldToScreenPoint(transform.position).z;
        Vector3 mouseWorldPoint = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, zDistance));
        // 更新物体位置时加上最初计算的偏移量
        transform.position = mouseWorldPoint + offset;
    }
}

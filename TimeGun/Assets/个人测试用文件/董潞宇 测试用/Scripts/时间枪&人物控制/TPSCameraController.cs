using System;
using Unity.Cinemachine;
using UnityEngine;

namespace TimeGun   
{
    /// <summary>
    /// 请把此脚本挂载在一个空物体 (管理器) 上，用于控制第三人称摄像机的切换
    /// ✅ 优化：使用 Priority 切换，避免与死亡摄像机等其它系统冲突
    /// </summary>
    public class TPSCameraController : MonoBehaviour
    {
        [Header("Cinemachine")]
        [SerializeField, Tooltip("主相机 Main Camera")]
        private CinemachineCamera playerCamera;
        [SerializeField, Tooltip("瞄准时相机 Aim Camera")]
        private CinemachineCamera aimCamera;

        [Header("Camera Root")]
        [SerializeField, Tooltip("目标位置 Camera Root")]
        private Transform cameraRoot;

        [Header("Player")]
        [SerializeField, Tooltip("玩家 Player")]
        private PlayerController player;

        [Header("Priority Settings")]
        [Tooltip("普通相机的基础优先级")]
        [SerializeField] private int normalCameraPriority = 10;

        [Tooltip("瞄准相机的基础优先级")]
        [SerializeField] private int aimCameraPriority = 15;

        private void Start()
        {
            if (!player)
            {
                player = FindFirstObjectByType<PlayerController>();
            }
            if (!cameraRoot) cameraRoot = player?.cameraRoot ?? transform;
            
            // 检查配置
            if (!playerCamera || !aimCamera || !cameraRoot || !player)
            {
                Debug.LogError("[TPSCameraController] 请在Inspector中设置所有引用");
                enabled = false;
                return;
            }

            playerCamera.LookAt = cameraRoot;
            aimCamera.LookAt = cameraRoot;

            // ✅ 使用 Priority 初始化（而不是 SetActive）
            playerCamera.Priority = normalCameraPriority;
            aimCamera.Priority = normalCameraPriority - 5; // 初始优先级更低

            // 确保两个摄像机都是激活的
            playerCamera.gameObject.SetActive(true);
            aimCamera.gameObject.SetActive(true);

            Debug.Log("[TPSCameraController] 初始化完成，使用 Priority 模式切换摄像机");
        }

        private void Update()
        {
            // 根据玩家是否在瞄准状态切换相机模式
            if (player.IsAiming)
            {
                SwitchToAimCamera();
            }
            else
            {
                SwitchToNormalCamera();
            }
        }

        // ✅ 切换到瞄准相机（使用 Priority）
        private void SwitchToAimCamera()
        {
            aimCamera.Priority = aimCameraPriority;
            playerCamera.Priority = normalCameraPriority - 5;
        }

        // ✅ 切换到正常相机（使用 Priority）
        private void SwitchToNormalCamera()
        {
            playerCamera.Priority = normalCameraPriority;
            aimCamera.Priority = normalCameraPriority - 5;
        }
    }
}


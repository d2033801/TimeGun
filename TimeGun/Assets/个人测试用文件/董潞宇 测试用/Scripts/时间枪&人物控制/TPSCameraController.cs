using System;
using Unity.Cinemachine;
using UnityEngine;

namespace TimeGun   
{
    /// <summary>
    /// 请把此脚本挂载在一个空物体 (管理器) 上，用于控制第三人称摄像机的切换
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
                Debug.LogError("TPSCameraController: 请在Inspector中设置所有引用");
                enabled = false;
                return;
            }

            playerCamera.LookAt = cameraRoot;
            aimCamera.LookAt = cameraRoot;

            // 初始时启用主相机，禁用瞄准相机
            playerCamera.gameObject.SetActive(true);
            aimCamera.gameObject.SetActive(false);

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

        // 切换到瞄准相机
        private void SwitchToAimCamera()
        {

            playerCamera.gameObject.SetActive(false);
            aimCamera.gameObject.SetActive(true);
        }

        // 切换到正常相机
        private void SwitchToNormalCamera()
        {
            playerCamera.gameObject.SetActive(true);
            aimCamera.gameObject.SetActive(false);
        }
    }
}


using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private OverlapWFC wfcGenerator;


    private const int cameraTiles = 16;


    private Vector2Int currentVisibleOrigin;

    private Vector2Int lastCameraCell;


    [SerializeField] private int regenCooldownFrames = 0;
    private int regenCooldownCounter = 0;

    private Vector2 lastMoveDir;

    void Start()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!wfcGenerator)
        {
            Debug.LogError("Player: wfcGenerator not assigned.");
            enabled = false;
            return;
        }

        wfcGenerator.baseVisibleSize = cameraTiles;
        wfcGenerator.ConfigureBaseDimensions();

        Vector3 camPos = mainCamera.transform.position;
        currentVisibleOrigin = new Vector2Int(
            Mathf.FloorToInt(camPos.x - cameraTiles * 0.5f),
            Mathf.FloorToInt(camPos.y - cameraTiles * 0.5f)
        );

        wfcGenerator.GenerateInitialFullWindow(currentVisibleOrigin);
    }

    void Update()
    {
        HandleMovement();
    }

    void LateUpdate()
    {
        SlideWindowIfNeeded();
    }

    private void HandleMovement()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        Vector2 dir = new Vector2(moveX, moveY).normalized;

        if (dir.sqrMagnitude > 0.0001f)
        {
            lastMoveDir = dir;
            transform.position += (Vector3)(dir * moveSpeed * Time.deltaTime);


            if (mainCamera)
            {
                Vector3 cam = mainCamera.transform.position;
                mainCamera.transform.position = new Vector3(transform.position.x, transform.position.y, cam.z);
            }


            wfcGenerator.EnsureDirectionalExtension(dir);
        }
    }

    private void SlideWindowIfNeeded()
    {
        if (regenCooldownCounter > 0)
        {
            regenCooldownCounter--;
            return;
        }

        Vector3 camPos = mainCamera.transform.position;
        Vector2Int desiredVisibleOrigin = new Vector2Int(
            Mathf.FloorToInt(camPos.x - cameraTiles * 0.5f),
            Mathf.FloorToInt(camPos.y - cameraTiles * 0.5f)
        );
        if (desiredVisibleOrigin == currentVisibleOrigin) return;

        wfcGenerator.SlideCommit(currentVisibleOrigin, desiredVisibleOrigin);
        currentVisibleOrigin = desiredVisibleOrigin;

        if (regenCooldownFrames > 0)
            regenCooldownCounter = regenCooldownFrames;
    }
}
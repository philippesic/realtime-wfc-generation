
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private OverlapWFC wfcGenerator;


    private const int cameraTiles = 16;

    private const int padding = 1;

    private const int totalTiles = cameraTiles + padding * 2;


    private Vector2Int currentVisibleOrigin;

    private Vector2Int lastCameraCell;


    [SerializeField] private int regenCooldownFrames = 0;
    private int regenCooldownCounter = 0;

    void Start()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!wfcGenerator)
        {
            Debug.LogError("Player: wfcGenerator not assigned.");
            enabled = false;
            return;
        }


        wfcGenerator.width = totalTiles;
        wfcGenerator.depth = totalTiles;

        Vector2 camPos = mainCamera.transform.position;
        lastCameraCell = new Vector2Int(Mathf.FloorToInt(camPos.x), Mathf.FloorToInt(camPos.y));

        currentVisibleOrigin = lastCameraCell - new Vector2Int(cameraTiles / 2, cameraTiles / 2);


        Vector2Int paddedWindowOrigin = currentVisibleOrigin - new Vector2Int(padding, padding);
        wfcGenerator.GenerateInitialFullWindow(paddedWindowOrigin);
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
        if (dir.sqrMagnitude > 0f)
            transform.position += (Vector3)(dir * moveSpeed * Time.deltaTime);


        if (mainCamera)
        {
            Vector3 cam = mainCamera.transform.position;
            mainCamera.transform.position = new Vector3(transform.position.x, transform.position.y, cam.z);
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
        Vector2Int camCell = new Vector2Int(Mathf.FloorToInt(camPos.x), Mathf.FloorToInt(camPos.y));
        if (camCell == lastCameraCell) return;

        lastCameraCell = camCell;


        Vector2Int desiredVisibleOrigin = camCell - new Vector2Int(cameraTiles / 2, cameraTiles / 2);


        if (desiredVisibleOrigin == currentVisibleOrigin) return;

        currentVisibleOrigin = desiredVisibleOrigin;
        Vector2Int paddedWindowOrigin = currentVisibleOrigin - new Vector2Int(padding, padding);


        wfcGenerator.RegenerateAt(paddedWindowOrigin);

        if (regenCooldownFrames > 0)
            regenCooldownCounter = regenCooldownFrames;
    }
}
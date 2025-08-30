using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private OverlapWFC wfcGenerator;
    private int cameraTiles;
    private const float MinMoveSqrMag = 0.0001f;

    private Vector2Int currentVisibleOrigin;
    private Vector2Int lastExtendCheckCell;

    [SerializeField] private int regenCooldownFrames = 0;
    private int regenCooldownCounter = 0;

    private Vector2 lastMoveDir;

    void Start()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!ValidateRefs()) return;
        cameraTiles = wfcGenerator.baseVisibleSize;

        InitializeWfcWindow();
    }

    void Update()
    {
        Vector2 inputDir = ReadInputDirection();
        if (inputDir.sqrMagnitude > MinMoveSqrMag)
        {
            lastMoveDir = inputDir;
            ApplyMovement(inputDir);
            FollowCamera();
            TryDirectionalExtension();
        }
    }

    void LateUpdate()
    {
        TrySlideWindow();
    }

    private bool ValidateRefs()
    {
        if (wfcGenerator) return true;
        Debug.LogError("Player: wfcGenerator not assigned");
        enabled = false;
        return false;
    }

    private void InitializeWfcWindow()
    {
        wfcGenerator.baseVisibleSize = cameraTiles;
        wfcGenerator.ConfigureBaseDimensions();

        Vector3 camPos = mainCamera.transform.position;
        currentVisibleOrigin = CalculateVisibleOrigin(camPos);
        wfcGenerator.GenerateInitialFullWindow(currentVisibleOrigin);
    }

    private Vector2 ReadInputDirection()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        return new Vector2(moveX, moveY).normalized;
    }

    private void ApplyMovement(Vector2 dir)
    {
        transform.position += (Vector3)(dir * moveSpeed * Time.deltaTime);
    }

    private void FollowCamera()
    {
        if (!mainCamera) return;
        Vector3 cam = mainCamera.transform.position;
        mainCamera.transform.position = new Vector3(transform.position.x, transform.position.y, cam.z);
    }

    private void TryDirectionalExtension()
    {
        Vector2Int camCell = GetCameraCell();
        if (camCell == lastExtendCheckCell) return;

        lastExtendCheckCell = camCell;
        wfcGenerator.EnsureDirectionalExtension(lastMoveDir);
    }

    private void TrySlideWindow()
    {
        if (regenCooldownCounter > 0)
        {
            regenCooldownCounter--;
            return;
        }

        Vector2Int desiredOrigin = CalculateVisibleOrigin(mainCamera.transform.position);
        if (desiredOrigin == currentVisibleOrigin) return;

        wfcGenerator.SlideCommit(currentVisibleOrigin, desiredOrigin);
        currentVisibleOrigin = desiredOrigin;

        if (regenCooldownFrames > 0)
            regenCooldownCounter = regenCooldownFrames;
    }

    private Vector2Int GetCameraCell()
    {
        Vector3 camPos = mainCamera.transform.position;
        return new Vector2Int(Mathf.FloorToInt(camPos.x), Mathf.FloorToInt(camPos.y));
    }

    private Vector2Int CalculateVisibleOrigin(Vector3 camPos)
    {
        float half = cameraTiles * 0.5f;
        return new Vector2Int(
            Mathf.FloorToInt(camPos.x - half),
            Mathf.FloorToInt(camPos.y - half)
        );
    }
}
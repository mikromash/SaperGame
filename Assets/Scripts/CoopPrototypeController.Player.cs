using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class CoopPrototypeController
{
    private void SetupWorld()
    {
        CacheSceneAvatars();

        _camera = Camera.main;
        if (_camera == null)
        {
            GameObject cameraRoot = new GameObject("Main Camera");
            cameraRoot.tag = "MainCamera";
            _camera = cameraRoot.AddComponent<Camera>();
        }

        _camera.transform.position = new Vector3(0f, 12f, -10f);
        _camera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0.08f, 0.1f, 0.12f);

        if (FindAnyObjectByType<Light>() == null)
        {
            GameObject lightRoot = new GameObject("Directional Light");
            Light lightComponent = lightRoot.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.15f;
            lightRoot.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        ClearAvatars();
    }

    private void CacheSceneAvatars()
    {
        _sceneAvatars.Clear();

        CoopScenePlayerAvatar[] sceneAvatars = FindObjectsByType<CoopScenePlayerAvatar>();
        foreach (CoopScenePlayerAvatar sceneAvatar in sceneAvatars)
        {
            if (sceneAvatar == null)
            {
                continue;
            }

            _sceneAvatars[sceneAvatar.PlayerId] = sceneAvatar;
        }
    }

    private void HandleMovement()
    {
        if (!_avatars.TryGetValue(_localPlayerId, out CoopAvatarView avatar) || avatar.SceneAvatar == null)
        {
            return;
        }

        Vector2 moveInput = ReadMoveInput();
        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (direction.sqrMagnitude <= 0f)
        {
            return;
        }

        Vector3 nextPosition = avatar.TargetPosition + direction * (MoveSpeed * Time.deltaTime);
        nextPosition.x = Mathf.Clamp(nextPosition.x, -9f, 9f);
        nextPosition.z = Mathf.Clamp(nextPosition.z, -9f, 9f);
        nextPosition.y = 0.6f;
        avatar.TargetPosition = nextPosition;
        avatar.SceneAvatar.Position = nextPosition;

        if (_relayClient != null && Time.unscaledTime - _lastMoveSentTime >= MoveSendIntervalSeconds)
        {
            _relayClient.SendMove(nextPosition);
            _lastMoveSentTime = Time.unscaledTime;
        }
    }

    private static Vector2 ReadMoveInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            horizontal += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            vertical -= 1f;
        }

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            vertical += 1f;
        }

        return new Vector2(horizontal, vertical);
    }

    private void UpdateCamera()
    {
        if (!_avatars.TryGetValue(_localPlayerId, out CoopAvatarView avatar) || avatar.SceneAvatar == null)
        {
            return;
        }

        Vector3 focus = avatar.SceneAvatar.Position;
        Vector3 desiredPosition = focus + new Vector3(0f, 12f, -10f);
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, desiredPosition, Time.deltaTime * 3.5f);
        _camera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
    }

    private void ApplySnapshot(CoopPlayerSnapshot[] snapshots)
    {
        if (snapshots == null)
        {
            return;
        }

        _latestSnapshots = snapshots;

        HashSet<int> activeIds = new HashSet<int>();

        foreach (CoopPlayerSnapshot snapshot in snapshots)
        {
            activeIds.Add(snapshot.PlayerId);

            if (!_avatars.TryGetValue(snapshot.PlayerId, out CoopAvatarView avatar) || avatar.SceneAvatar == null)
            {
                avatar = GetOrCreateSceneAvatar(snapshot.PlayerId);
                if (avatar == null)
                {
                    continue;
                }
            }

            Vector3 snapshotPosition = new Vector3(snapshot.X, 0.6f, snapshot.Z);
            if (snapshot.PlayerId == _localPlayerId)
            {
                float localDrift = Vector3.Distance(avatar.TargetPosition, snapshotPosition);
                if (localDrift > LocalReconciliationThreshold)
                {
                    Debug.Log($"[CoopMovement] Reconciled local player. drift={localDrift:F2}, playerId={snapshot.PlayerId}");
                    avatar.TargetPosition = snapshotPosition;
                    avatar.SceneAvatar.Position = snapshotPosition;
                }
            }
            else
            {
                float snapshotReceivedTime = Time.unscaledTime;
                float snapshotInterval = avatar.LastSnapshotReceivedTime > 0f
                    ? Mathf.Clamp(
                        snapshotReceivedTime - avatar.LastSnapshotReceivedTime,
                        RemoteInterpolationMinDuration,
                        RemoteInterpolationMaxDuration)
                    : RemoteInterpolationMinDuration;

                Vector3 currentRenderedPosition = avatar.SceneAvatar.Position;
                avatar.InterpolationFromPosition = currentRenderedPosition;
                avatar.InterpolationToPosition = snapshotPosition;
                avatar.InterpolationStartTime = snapshotReceivedTime;
                avatar.InterpolationDuration = snapshotInterval;
                avatar.ExtrapolatedVelocity = snapshotInterval > 0f
                    ? (snapshotPosition - avatar.TargetPosition) / snapshotInterval
                    : Vector3.zero;
                avatar.LastSnapshotReceivedTime = snapshotReceivedTime;
                avatar.HasRemoteInterpolation = true;
                avatar.TargetPosition = snapshotPosition;
            }

            avatar.SceneAvatar.SetVisible(true);
            avatar.SceneAvatar.SetDisplayName(snapshot.PlayerName);
        }

        List<int> toRemove = new List<int>();
        foreach (int playerId in _avatars.Keys)
        {
            if (!activeIds.Contains(playerId))
            {
                toRemove.Add(playerId);
            }
        }

        foreach (int playerId in toRemove)
        {
            if (_avatars.TryGetValue(playerId, out CoopAvatarView avatar) && avatar.SceneAvatar != null)
            {
                avatar.SceneAvatar.SetVisible(false);
            }
        }
    }

    private CoopAvatarView GetOrCreateSceneAvatar(int playerId)
    {
        if (_avatars.TryGetValue(playerId, out CoopAvatarView existingAvatar) && existingAvatar.SceneAvatar != null)
        {
            return existingAvatar;
        }

        if (!_sceneAvatars.TryGetValue(playerId, out CoopScenePlayerAvatar sceneAvatar) || sceneAvatar == null)
        {
            Debug.LogWarning($"Scene avatar for player id {playerId} was not found. Add CoopScenePlayerAvatar to a placed player object.");
            return null;
        }

        CoopAvatarView avatar = new CoopAvatarView
        {
            SceneAvatar = sceneAvatar,
            TargetPosition = sceneAvatar.Position,
            InterpolationFromPosition = sceneAvatar.Position,
            InterpolationToPosition = sceneAvatar.Position
        };

        _avatars[playerId] = avatar;
        return avatar;
    }

    private void ClearAvatars()
    {
        foreach (CoopAvatarView avatar in _avatars.Values)
        {
            if (avatar.SceneAvatar != null)
            {
                avatar.SceneAvatar.SetVisible(false);
            }
        }

        _avatars.Clear();

        foreach (CoopScenePlayerAvatar sceneAvatar in _sceneAvatars.Values)
        {
            if (sceneAvatar != null)
            {
                sceneAvatar.SetVisible(false);
            }
        }
    }

    private Vector3 GetSceneSpawnPosition(int playerId, Vector3 fallbackPosition)
    {
        if (_sceneAvatars.TryGetValue(playerId, out CoopScenePlayerAvatar sceneAvatar) && sceneAvatar != null)
        {
            return sceneAvatar.Position;
        }

        return fallbackPosition;
    }
}

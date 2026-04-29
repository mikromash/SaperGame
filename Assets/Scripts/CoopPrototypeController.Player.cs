using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public sealed partial class CoopPrototypeController
{
    private static readonly Vector3 PlayerOneFallbackSpawnPosition = new Vector3(0.5f, 3.3f, 5.3f);
    private static readonly Vector3 PlayerTwoFallbackSpawnPosition = new Vector3(2.48f, 3.35f, 5.3f);

    private void SetupWorld()
    {
        // Инициализация мира после загрузки геймплейной сцены.
        Debug.Log("[TRACE SetupWorld] Begin.");
        CacheSceneAvatars();
        Debug.Log($"[TRACE SetupWorld] Scene avatars cached: count={_sceneAvatars.Count}, ids=[{string.Join(", ", _sceneAvatars.Keys)}]");

        // Если в сцене уже есть Cinemachine-риг, используем его вместо старой ручной камеры.
        _camera = Camera.main;
        _sceneCinemachineCamera = FindAnyObjectByType<CinemachineCamera>();
        _useSceneCameraRig = _camera != null && _camera.GetComponent<CinemachineBrain>() != null && _sceneCinemachineCamera != null;
        Debug.Log($"[TRACE SetupWorld] Camera.main before setup = {(_camera != null ? _camera.name : "null")}");
        if (_useSceneCameraRig)
        {
            Debug.Log(
                $"[TRACE SetupWorld] Using scene camera rig. renderCamera={_camera.name}, " +
                $"cinemachineCamera={_sceneCinemachineCamera.name}");
        }
        else if (_camera == null)
        {
            GameObject cameraRoot = new GameObject("Main Camera");
            cameraRoot.tag = "MainCamera";
            _camera = cameraRoot.AddComponent<Camera>();
            Debug.Log("[TRACE SetupWorld] Main camera was missing. Created new Main Camera.");
        }

        // Fallback-камера нужна только если в сцене нет готового рига.
        if (!_useSceneCameraRig)
        {
            _camera.transform.position = new Vector3(0f, 12f, -10f);
            _camera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.08f, 0.1f, 0.12f);
            Debug.Log($"[TRACE SetupWorld] Camera configured. pos={_camera.transform.position}, rot={_camera.transform.eulerAngles}");
        }

        // На случай пустой тестовой сцены автоматически добавляем свет.
        if (FindAnyObjectByType<Light>() == null)
        {
            GameObject lightRoot = new GameObject("Directional Light");
            Light lightComponent = lightRoot.AddComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = 1.15f;
            lightRoot.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Debug.Log("[TRACE SetupWorld] Directional light was missing. Created new light.");
        }

        Debug.Log($"[TRACE SetupWorld] Clearing avatars. Existing runtime avatars count={_avatars.Count}");
        ClearAvatars();
        Debug.Log("[TRACE SetupWorld] End.");
    }

    private void CacheSceneAvatars()
    {
        // Сохраняем сценовые аватары по PlayerId, чтобы потом связать их с сетевыми снапшотами.
        _sceneAvatars.Clear();
        _sceneSpawnPositions.Clear();

        CoopScenePlayerAvatar[] sceneAvatars = FindObjectsByType<CoopScenePlayerAvatar>();
        foreach (CoopScenePlayerAvatar sceneAvatar in sceneAvatars)
        {
            if (sceneAvatar == null)
            {
                continue;
            }

            Debug.Log(
                $"[TRACE CacheSceneAvatars] Found avatar object={sceneAvatar.name}, playerId={sceneAvatar.PlayerId}, " +
                $"position={sceneAvatar.Position}");
            _sceneAvatars[sceneAvatar.PlayerId] = sceneAvatar;
            _sceneSpawnPositions[sceneAvatar.PlayerId] = sceneAvatar.Position;
        }
    }

    private void HandleMovement()
    {
        // Локальный игрок двигается через PlayerMovement, а здесь мы только поддерживаем синхронизацию.
        if (!_avatars.TryGetValue(_localPlayerId, out CoopAvatarView avatar) || avatar.SceneAvatar == null)
        {
            return;
        }

        EnsureLocalAvatarMovement(avatar);

        if (_localPlayerMovement == null)
        {
            return;
        }

        bool canMove = !_isPauseMenuOpen;
        if (_localPlayerMovement.enabled != canMove)
        {
            _localPlayerMovement.enabled = canMove;
        }

        if (!canMove)
        {
            avatar.TargetPosition = avatar.SceneAvatar.Position;
            return;
        }

        if (_camera != null && _localPlayerMovement.cameraTransform != _camera.transform)
        {
            _localPlayerMovement.cameraTransform = _camera.transform;
        }

        Vector3 currentPosition = avatar.SceneAvatar.Position;
        avatar.TargetPosition = currentPosition;

        // В сеть отправляем уже фактическую позицию локального аватара, включая прыжок по Y.
        if (_relayClient != null && Time.unscaledTime - _lastMoveSentTime >= 0.04f)
        {
            _relayClient.SendMove(currentPosition);
            _lastMoveSentTime = Time.unscaledTime;
        }
    }

    private void EnsureLocalAvatarMovement(CoopAvatarView avatar)
    {
        // Только локальному игроку добавляем компоненты реального управления.
        if (avatar.SceneAvatar == null)
        {
            Debug.LogWarning("[TRACE LocalMovement] SceneAvatar is null.");
            return;
        }

        GameObject avatarObject = avatar.SceneAvatar.gameObject;
        Debug.Log(
            $"[TRACE LocalMovement] Begin for object={avatarObject.name}, playerId={avatar.SceneAvatar.PlayerId}, " +
            $"position={avatar.SceneAvatar.Position}");

        // CharacterController подготавливается на лету, чтобы не вешать его вручную на обе капсулы.
        if (_localCharacterController == null || _localCharacterController.gameObject != avatarObject)
        {
            _localCharacterController = avatarObject.GetComponent<CharacterController>();
            if (_localCharacterController == null)
            {
                _localCharacterController = avatarObject.AddComponent<CharacterController>();
            }

            _localCharacterController.height = 1.8f;
            _localCharacterController.radius = 0.3f;
            _localCharacterController.center = Vector3.zero;
            _localCharacterController.stepOffset = 0.3f;
            _localCharacterController.minMoveDistance = 0.001f;
        }
        Debug.Log(
            $"[TRACE LocalMovement] CharacterController ready. exists={_localCharacterController != null}, " +
            $"center={_localCharacterController.center}, height={_localCharacterController.height}, " +
            $"radius={_localCharacterController.radius}");

        // PlayerMovement также должен существовать только у локального игрока.
        if (_localPlayerMovement == null || _localPlayerMovement.gameObject != avatarObject)
        {
            _localPlayerMovement = avatarObject.GetComponent<PlayerMovement>();
            if (_localPlayerMovement == null)
            {
                _localPlayerMovement = avatarObject.AddComponent<PlayerMovement>();
            }
        }

        if (_camera != null)
        {
            _localPlayerMovement.cameraTransform = _camera.transform;
        }
        // Если используется риг из сцены, переназначаем его на локального игрока этого клиента.
        if (_useSceneCameraRig && _sceneCinemachineCamera != null)
        {
            CameraTarget target = _sceneCinemachineCamera.Target;
            target.TrackingTarget = avatar.SceneAvatar.transform;
            target.LookAtTarget = null;
            target.CustomLookAtTarget = false;
            _sceneCinemachineCamera.Target = target;
            Debug.Log(
                $"[TRACE LocalMovement] Scene camera rig target assigned. rig={_sceneCinemachineCamera.name}, " +
                $"trackingTarget={avatar.SceneAvatar.transform.name}");
        }
        Debug.Log(
            $"[TRACE LocalMovement] PlayerMovement ready. exists={_localPlayerMovement != null}, " +
            $"cameraTransform={(_localPlayerMovement != null && _localPlayerMovement.cameraTransform != null ? _localPlayerMovement.cameraTransform.name : "null")}");

        _isLocalAvatarInitialized = true;
        Debug.Log("[TRACE LocalMovement] Local avatar initialization complete.");
    }

    private void UpdateCamera()
    {
        // При Cinemachine-риге ручное управление камерой полностью отключается.
        if (_useSceneCameraRig)
        {
            return;
        }

        if (!_avatars.TryGetValue(_localPlayerId, out CoopAvatarView avatar) || avatar.SceneAvatar == null)
        {
            Debug.LogWarning($"[TRACE UpdateCamera] No local avatar. localPlayerId={_localPlayerId}, avatarsCount={_avatars.Count}");
            return;
        }

        if (_camera == null)
        {
            Debug.LogWarning("[TRACE UpdateCamera] Camera is null.");
            return;
        }

        // Старый fallback: плавно держим камеру над локальным игроком.
        Vector3 focus = avatar.SceneAvatar.Position;
        Vector3 desiredPosition = focus + new Vector3(0f, 12f, -10f);
        Debug.Log(
            $"[TRACE UpdateCamera] localPlayerId={_localPlayerId}, focus={focus}, currentCameraPos={_camera.transform.position}, " +
            $"desiredCameraPos={desiredPosition}");
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, desiredPosition, Time.deltaTime * 3.5f);
        _camera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
    }

    private void ApplySnapshot(CoopPlayerSnapshot[] snapshots)
    {
        // Снапшот - это основной источник сетевых позиций и имен игроков.
        Debug.Log(
            $"[TRACE ApplySnapshot] Called. snapshotsNull={(snapshots == null)}, " +
            $"count={(snapshots == null ? -1 : snapshots.Length)}, screen={_screen}, localPlayerId={_localPlayerId}");
        if (snapshots == null)
        {
            return;
        }

        _latestSnapshots = snapshots;

        HashSet<int> activeIds = new HashSet<int>();

        // Применяем свежие позиции всем игрокам, кроме локального уже инициализированного аватара.
        foreach (CoopPlayerSnapshot snapshot in snapshots)
        {
            Debug.Log(
                $"[TRACE ApplySnapshot] Snapshot playerId={snapshot.PlayerId}, name={snapshot.PlayerName}, " +
                $"pos=({snapshot.X}, {snapshot.Y}, {snapshot.Z})");
            activeIds.Add(snapshot.PlayerId);

            bool isNewAvatar = !_avatars.TryGetValue(snapshot.PlayerId, out CoopAvatarView avatar) || avatar.SceneAvatar == null;
            if (isNewAvatar)
            {
                avatar = GetOrCreateSceneAvatar(snapshot.PlayerId);
                if (avatar == null)
                {
                    continue;
                }
            }
            Debug.Log(
                $"[TRACE ApplySnapshot] Avatar resolved for playerId={snapshot.PlayerId}. " +
                $"sceneObject={(avatar.SceneAvatar != null ? avatar.SceneAvatar.name : "null")}, " +
                $"scenePos={(avatar.SceneAvatar != null ? avatar.SceneAvatar.Position.ToString() : "null")}");

            Vector3 snapshotPosition = new Vector3(snapshot.X, snapshot.Y, snapshot.Z);
            Vector3 targetPosition = _screen == MenuScreen.InGame && isNewAvatar
                ? GetSceneSpawnPosition(snapshot.PlayerId, snapshotPosition)
                : snapshotPosition;
            avatar.SceneAvatar.SetVisible(true);
            avatar.SceneAvatar.SetDisplayName(snapshot.PlayerName);

            if (snapshot.PlayerId == _localPlayerId && _screen == MenuScreen.InGame)
            {
                Debug.Log(
                    $"[TRACE ApplySnapshot] Local player snapshot. initialized={_isLocalAvatarInitialized}, " +
                    $"sceneAvatarPos={avatar.SceneAvatar.Position}, snapshotPos={snapshotPosition}");
                if (!_isLocalAvatarInitialized)
                {
                    avatar.SceneAvatar.Position = targetPosition;
                    avatar.TargetPosition = targetPosition;
                    EnsureLocalAvatarMovement(avatar);
                    Debug.Log(
                        $"[TRACE ApplySnapshot] Local avatar initialized. newScenePos={avatar.SceneAvatar.Position}, " +
                        $"targetPos={avatar.TargetPosition}");
                }
                else
                {
                    avatar.TargetPosition = avatar.SceneAvatar.Position;
                    Debug.Log(
                        $"[TRACE ApplySnapshot] Local avatar already initialized. Keeping scene position={avatar.SceneAvatar.Position}");
                }

                continue;
            }

            avatar.TargetPosition = targetPosition;
            Debug.Log(
                $"[TRACE ApplySnapshot] Remote avatar target set. playerId={snapshot.PlayerId}, targetPos={avatar.TargetPosition}");
        }

        // Аватары, которых больше нет в снапшоте, просто скрываем до следующего матча.
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
        // Для каждого playerId ищем заранее размещенный объект сцены и оборачиваем его во view.
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
            TargetPosition = sceneAvatar.Position
        };

        _avatars[playerId] = avatar;
        return avatar;
    }

    private void ClearAvatars()
    {
        // Очистка скрывает сценовых аватаров и сбрасывает локальные runtime-компоненты.
        _isLocalAvatarInitialized = false;
        _localPlayerMovement = null;
        _localCharacterController = null;

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
        // Если в сцене есть готовая точка игрока, используем ее как приоритетную.
        if (_sceneSpawnPositions.TryGetValue(playerId, out Vector3 sceneSpawnPosition))
        {
            return sceneSpawnPosition;
        }

        if (_sceneAvatars.TryGetValue(playerId, out CoopScenePlayerAvatar sceneAvatar) && sceneAvatar != null)
        {
            return sceneAvatar.Position;
        }

        if (playerId == 1)
        {
            return PlayerOneFallbackSpawnPosition;
        }

        if (playerId == 2)
        {
            return PlayerTwoFallbackSpawnPosition;
        }

        return fallbackPosition;
    }

    public void ResetPlayersToMinesweeperStart()
    {
        if (_screen != MenuScreen.InGame)
        {
            return;
        }

        foreach (KeyValuePair<int, CoopAvatarView> pair in _avatars)
        {
            CoopAvatarView avatar = pair.Value;
            if (avatar?.SceneAvatar == null)
            {
                continue;
            }

            Vector3 spawnPosition = GetSceneSpawnPosition(pair.Key, avatar.SceneAvatar.Position);
            TeleportAvatar(avatar, spawnPosition);
        }

        foreach (KeyValuePair<int, CoopScenePlayerAvatar> pair in _sceneAvatars)
        {
            if (_avatars.ContainsKey(pair.Key) || pair.Value == null)
            {
                continue;
            }

            pair.Value.Position = GetSceneSpawnPosition(pair.Key, pair.Value.Position);
        }

        _lastMoveSentTime = -10f;
    }

    private void TeleportAvatar(CoopAvatarView avatar, Vector3 position)
    {
        CharacterController controller = avatar.SceneAvatar.GetComponent<CharacterController>();
        bool wasControllerEnabled = controller != null && controller.enabled;

        if (controller != null)
        {
            controller.enabled = false;
        }

        avatar.SceneAvatar.Position = position;
        avatar.TargetPosition = position;

        if (controller != null)
        {
            controller.enabled = wasControllerEnabled;
        }
    }
}

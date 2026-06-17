using UnityEngine;

public sealed class CoopScenePlayerAvatar : MonoBehaviour
{
    // Этот компонент висит на сценовой капсуле и представляет игрока в мире.
    [SerializeField] private int playerId = 1;
    [SerializeField] private TextMesh nameLabel;
    [SerializeField] private GameObject visualRoot;

    public int PlayerId => playerId;

    public Vector3 Position
    {
        // Контроллер работает с аватаром через единое свойство позиции.
        get => transform.position;
        set => transform.position = value;
    }

    private void Reset()
    {
        // Автоподбор ссылок для удобства настройки в редакторе.
        if (visualRoot == null)
        {
            visualRoot = gameObject;
        }

        if (nameLabel == null)
        {
            nameLabel = GetComponentInChildren<TextMesh>();
        }
    }

    private void Awake()
    {
        // На случай пустой сцены сами создаем простую подпись над игроком.
        if (visualRoot == null)
        {
            visualRoot = gameObject;
        }

        if (nameLabel == null)
        {
            nameLabel = GetComponentInChildren<TextMesh>();
        }

        if (nameLabel == null)
        {
            GameObject labelRoot = new GameObject("NameLabel");
            labelRoot.transform.SetParent(transform, false);
            labelRoot.transform.localPosition = new Vector3(0f, 3.6f, 0f);
            nameLabel = labelRoot.AddComponent<TextMesh>();
            nameLabel.anchor = TextAnchor.MiddleCenter;
            nameLabel.alignment = TextAlignment.Center;
            nameLabel.characterSize = 0.15f;
            nameLabel.fontSize = 48;
            nameLabel.color = Color.white;
        }
    }

    public void SetDisplayName(string playerName)
    {
        // Имя игрока приходит из сети и обновляется на подписи.
        if (nameLabel != null)
        {
            nameLabel.text = playerName;
        }
    }

    public void FaceCamera(Camera targetCamera)
    {
        // Подпись всегда поворачивается к активной камере.
        if (nameLabel == null || targetCamera == null)
        {
            return;
        }

        Transform labelTransform = nameLabel.transform;
        labelTransform.rotation = Quaternion.LookRotation(labelTransform.position - targetCamera.transform.position);
    }

    public void SetVisible(bool isVisible)
    {
        // Используется при подключении/отключении игроков и сбросе сцены.
        GameObject target = visualRoot != null ? visualRoot : gameObject;
        target.SetActive(isVisible);
    }
}

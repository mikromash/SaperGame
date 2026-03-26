using UnityEngine;

public sealed class CoopScenePlayerAvatar : MonoBehaviour
{
    [SerializeField] private int playerId = 1;
    [SerializeField] private TextMesh nameLabel;
    [SerializeField] private GameObject visualRoot;

    public int PlayerId => playerId;

    public Vector3 Position
    {
        get => transform.position;
        set => transform.position = value;
    }

    private void Reset()
    {
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
            labelRoot.transform.localPosition = new Vector3(0f, 1.4f, 0f);
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
        if (nameLabel != null)
        {
            nameLabel.text = playerName;
        }
    }

    public void FaceCamera(Camera targetCamera)
    {
        if (nameLabel == null || targetCamera == null)
        {
            return;
        }

        Transform labelTransform = nameLabel.transform;
        labelTransform.rotation = Quaternion.LookRotation(labelTransform.position - targetCamera.transform.position);
    }

    public void SetVisible(bool isVisible)
    {
        GameObject target = visualRoot != null ? visualRoot : gameObject;
        target.SetActive(isVisible);
    }
}

using UnityEngine;

[System.Serializable]
public sealed class AudioClipSettings
{
    public AudioEvent eventId;
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(-3f, 3f)] public float pitchJitter = 0.04f;
    [Min(0f)] public float cooldown = 0.03f;
    public bool spatial;
}

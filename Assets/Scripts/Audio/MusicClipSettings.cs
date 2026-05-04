using UnityEngine;

[System.Serializable]
public sealed class MusicClipSettings
{
    public MusicTrack track;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    public bool loop = true;
}

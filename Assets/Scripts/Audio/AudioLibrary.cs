using UnityEngine;

[CreateAssetMenu(fileName = "CoopAudioLibrary", menuName = "Saper Game/Audio Library")]
public sealed class AudioLibrary : ScriptableObject
{
    [SerializeField] private AudioClipSettings[] soundEffects = System.Array.Empty<AudioClipSettings>();
    [SerializeField] private MusicClipSettings[] music = System.Array.Empty<MusicClipSettings>();

    public bool TryGetSound(AudioEvent eventId, out AudioClipSettings settings)
    {
        for (int index = 0; index < soundEffects.Length; index++)
        {
            AudioClipSettings candidate = soundEffects[index];
            if (candidate != null && candidate.eventId == eventId)
            {
                settings = candidate;
                return true;
            }
        }

        settings = null;
        return false;
    }

    public bool TryGetMusic(MusicTrack track, out MusicClipSettings settings)
    {
        for (int index = 0; index < music.Length; index++)
        {
            MusicClipSettings candidate = music[index];
            if (candidate != null && candidate.track == track)
            {
                settings = candidate;
                return true;
            }
        }

        settings = null;
        return false;
    }
}

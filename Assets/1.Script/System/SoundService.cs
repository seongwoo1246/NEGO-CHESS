using Cysharp.Threading.Tasks;

/// <summary>SoundM이 없을 때(씬 단독 실행 등) 아무것도 하지 않는 대체품</summary>
public sealed class NullSoundService : ISoundService
{
    public void playBGM(string soundName, bool loop = true) { }
    public void playSFX(string soundName) { }
    public void StopBGM() { }
    public UniTask FadeSound(string newSound, float fadeTime) => UniTask.CompletedTask;
    public void SetMasterVolume(float linearVolume) { }
    public void SetBGMVolume(float linearVolume) { }
    public void SetSFXVolume(float linearVolume) { }
    public void SoundMute(VolumeType type) { }
    public void ResetAllSoundSettings() { }
}

public static class SoundService
{
    private static readonly ISoundService Fallback = new NullSoundService();
    private static ISoundService _cached;

    public static ISoundService Current
    {
        get
        {
            // 캐시가 파괴된 Unity 오브젝트면 비움 (인터페이스 변수는 Unity의 null 처리가 안 되므로 따로 확인)
            if (_cached is UnityEngine.Object obj && obj == null) _cached = null;

            if (_cached == null) ScriptM.TryGet<ISoundService>(out _cached);
            return _cached ?? Fallback;   // 없으면 소리 없는 대체품
        }
    }
}
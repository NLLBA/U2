public sealed class AudioSettingsService
{
    private const int CurrentVersion = 1;

    public bool Upgrade(MusicData settings, bool hadSavedSettings)
    {
        if (settings == null || settings.settingsVersion >= CurrentVersion)
            return false;
        if (hadSavedSettings)
            settings.musicOn = !settings.musicOn;
        settings.settingsVersion = CurrentVersion;
        return hadSavedSettings;
    }

    public void Save(MusicData settings)
    {
        XmlDataMgr.Instance.Save(settings, "MusicData");
    }

    public void SetMusic(MusicData settings, bool enabled)
    {
        settings.musicOn = enabled;
        if (BKMusic.Instance != null)
            BKMusic.Instance.SetOpen(enabled);
    }

    public void SetMusicVolume(MusicData settings, float value)
    {
        settings.musicVolume = value;
        if (BKMusic.Instance != null)
            BKMusic.Instance.SetMusicVolume(value);
    }
}

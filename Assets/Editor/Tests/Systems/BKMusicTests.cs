using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public class BKMusicTests
{
    [Test]
    public void LegacySavedMusicSwitchIsMigratedOnlyOnce()
    {
        MusicData settings = new MusicData
        {
            settingsVersion = 0,
            musicOn = false
        };
        MethodInfo upgrade = typeof(GameDataMgr).GetMethod(
            "UpgradeMusicSettings",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.IsNotNull(upgrade);
        Assert.IsTrue((bool)upgrade.Invoke(null, new object[] { settings, true }));
        Assert.IsTrue(settings.musicOn);
        Assert.AreEqual(1, settings.settingsVersion);

        Assert.IsFalse((bool)upgrade.Invoke(null, new object[] { settings, true }));
        Assert.IsTrue(settings.musicOn, "An upgraded user setting must not be inverted again.");
    }

    [TestCase("Assets/Scenes/BeginScene.unity")]
    [TestCase("Assets/Scenes/GameScene.unity")]
    public void BackgroundMusicLoopsAndUsesTheEnabledState(string scenePath)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        BKMusic music = Object.FindObjectOfType<BKMusic>();
        Assert.IsNotNull(music, scenePath + " must contain BKMusic.");

        AudioSource source = music.GetComponent<AudioSource>();
        Assert.IsNotNull(source);
        Assert.IsTrue(source.loop, scenePath + " background music must loop in the scene.");

        AudioClip clip = source.clip;
        source.clip = null;
        typeof(BKMusic).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(music, null);

        Assert.IsTrue(source.loop, "BKMusic must also enforce looping at runtime.");
        Assert.IsFalse(source.playOnAwake, "BKMusic starts playback after applying saved settings.");
        Assert.AreEqual(0f, source.spatialBlend, 0.001f, "Background music must be 2D audio.");
        music.SetOpen(false);
        Assert.IsTrue(source.mute);
        music.SetOpen(true);
        Assert.IsFalse(source.mute);
        source.clip = clip;
    }

    [Test]
    public void SettingsRefreshDoesNotDisableSavedMusic()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BeginScene.unity", OpenSceneMode.Single);

        SettingPanel panel = Object.FindObjectOfType<SettingPanel>();
        Assert.IsNotNull(panel);
        typeof(SettingPanel).GetMethod("RefreshControls", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(panel, null);
        Assert.IsFalse(panel.toggleMusic.invertSpriteState,
            "The illuminated music control must represent enabled playback.");
        Assert.IsFalse(panel.toggleSound.invertSpriteState,
            "The illuminated sound control must represent enabled effects.");

        MusicData settings = GameDataMgr.Instance.musicData;
        bool originalMusicOn = settings.musicOn;
        bool originalSoundOn = settings.soundOn;
        float originalMusicVolume = settings.musicVolume;
        float originalSoundVolume = settings.soundVolume;

        int notifications = 0;
        EventDelegate callback = new EventDelegate(() => notifications++);

        try
        {
            panel.toggleMusic.startsActive = false;
            panel.toggleSound.startsActive = false;
            panel.toggleMusic.Start();
            panel.toggleSound.Start();
            panel.toggleMusic.onChange.Add(callback);
            panel.toggleSound.onChange.Add(callback);

            settings.musicOn = true;
            settings.soundOn = true;
            settings.musicVolume = 0.65f;
            settings.soundVolume = 0.7f;

            typeof(SettingPanel).GetMethod("RefreshControls", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(panel, null);

            Assert.IsTrue(panel.toggleMusic.value);
            Assert.IsTrue(panel.toggleSound.value);
            Assert.AreEqual(0.65f, panel.sliderMusic.value, 0.001f);
            Assert.AreEqual(0.7f, panel.sliderSound.value, 0.001f);
            Assert.AreEqual(0, notifications, "Refreshing saved settings must not invoke change callbacks.");
            Assert.IsTrue(settings.musicOn);
        }
        finally
        {
            panel.toggleMusic.onChange.Remove(callback);
            panel.toggleSound.onChange.Remove(callback);
            settings.musicOn = originalMusicOn;
            settings.soundOn = originalSoundOn;
            settings.musicVolume = originalMusicVolume;
            settings.soundVolume = originalSoundVolume;
        }
    }

    [Test]
    public void BossRageChangesMusicTempoWithoutInvertingTheEnabledSwitch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Single);
        BKMusic music = Object.FindObjectOfType<BKMusic>();
        AudioSource source = music.GetComponent<AudioSource>();

        music.SetCombatIntensity(true);
        Assert.IsTrue(music.CombatIntensity);
        Assert.AreEqual(1.12f, source.pitch, 0.001f);
        music.SetOpen(true);
        Assert.IsFalse(source.mute);
        Assert.AreEqual(1.12f, source.pitch, 0.001f);

        music.SetCombatIntensity(false);
        Assert.IsFalse(music.CombatIntensity);
        Assert.AreEqual(1f, source.pitch, 0.001f);
    }
}

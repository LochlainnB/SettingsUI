using MelonLoader;
using UnityEngine;
using System;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Serialization;

namespace SettingsUI
{
    public class Mod : MelonMod
    {
        private enum SettingType
        {
            MasterVolume,
            SFXVolume,
            MusicVolume,
            VoiceVolume
        }

        private static RumbleModUI.Mod audioSettings = new RumbleModUI.Mod();
        private static RumbleModUI.ModSetting<float> masterVolume;
        private static RumbleModUI.ModSetting<float> sfxVolume;
        private static RumbleModUI.ModSetting<float> musicVolume;
        private static RumbleModUI.ModSetting<float> voiceVolume;

        // Load settings
        public override void OnLateInitializeMelon()
        {
            audioSettings.ModName = "SettingsUI";
            audioSettings.ModVersion = "1.0.0";
            audioSettings.SetFolder("SettingsUI");
            audioSettings.SetSubFolder("Audio");

            masterVolume = audioSettings.AddToList("Master Volume", 1.0f, "Master volume. 0-1 covers the normal range. >1 allows boosting beyond the normal volume limit.", new RumbleModUI.Tags());
            sfxVolume = audioSettings.AddToList("SFX Volume", 1.0f, "Sound effects volume. 0-1 covers the normal range. >1 allows boosting beyond the normal volume limit.", new RumbleModUI.Tags());
            musicVolume = audioSettings.AddToList("Music Volume", 1.0f, "Music volume. 0-1 covers the normal range. >1 allows boosting beyond the normal volume limit.", new RumbleModUI.Tags());
            voiceVolume = audioSettings.AddToList("Voice Volume", 1.0f, "Voice volume. 0-1 covers the normal range. >1 allows boosting beyond the normal volume limit.", new RumbleModUI.Tags());

            audioSettings.GetFromFile();

            masterVolume.SavedValueChanged += masterVolumeChanged;
            sfxVolume.SavedValueChanged += sfxVolumeChanged;
            musicVolume.SavedValueChanged += musicVolumeChanged;
            voiceVolume.SavedValueChanged += voiceVolumeChanged;

            RumbleModUI.UI.instance.UI_Initialized += OnUIInit;
        }

        // Display settings
        public static void OnUIInit()
        {
            RumbleModUI.UI.instance.AddMod(audioSettings);
        }

        // Update Rumble settings when our settings change
        private static void ChangeSetting(SettingType type, float value)
        {
            AudioConfiguration audioConfig = AudioManager.instance.audioConfig;
            switch (type)
            {
                case SettingType.MasterVolume:
                    audioConfig.MasterVolume = value;
                    break;
                case SettingType.SFXVolume:
                    audioConfig.SFXVolume = value;
                    break;
                case SettingType.MusicVolume:
                    audioConfig.MusicVolume = value;
                    break;
                case SettingType.VoiceVolume:
                    audioConfig.VoiceVolume = value;
                    break;
                default:
                    MelonLogger.Warning("Tried to save invalid setting.");
                    break;
            }
            AudioManager.instance.ApplyConfiguration(audioConfig);
            AudioManager.instance.SaveConfiguration();
        }
        public static void masterVolumeChanged(object sender, EventArgs args)
        {
            ChangeSetting(SettingType.MasterVolume, ((RumbleModUI.ValueChange<float>)args).Value);
        }
        public static void sfxVolumeChanged(object sender, EventArgs args)
        {
            ChangeSetting(SettingType.SFXVolume, ((RumbleModUI.ValueChange<float>)args).Value);
        }
        public static void musicVolumeChanged(object sender, EventArgs args)
        {
            ChangeSetting(SettingType.MusicVolume, ((RumbleModUI.ValueChange<float>)args).Value);
        }
        public static void voiceVolumeChanged(object sender, EventArgs args)
        {
            ChangeSetting(SettingType.VoiceVolume, ((RumbleModUI.ValueChange<float>)args).Value);
        }
    }
}

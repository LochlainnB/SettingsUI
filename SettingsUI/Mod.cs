using MelonLoader;
using UnityEngine;
using HarmonyLib;
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

        private static bool doneInit = false;
        private static bool undoNextAudioConfig = false;

        private static Il2CppRUMBLE.UI.SettingsForm settingsForm;

        private static RumbleModUI.Mod audioSettings = new RumbleModUI.Mod();
        private static RumbleModUI.ModSetting<float> masterVolume;
        private static RumbleModUI.ModSetting<float> sfxVolume;
        private static RumbleModUI.ModSetting<float> musicVolume;
        private static RumbleModUI.ModSetting<float> voiceVolume;

        [HarmonyPatch(typeof(AudioManager), "ApplyConfiguration")]
        private static class AudioManager_ApplyConfiguration_Patch
        {
            private static void Postfix(AudioManager __instance, AudioConfiguration config)
            {
                if (undoNextAudioConfig)
                {
                    // Call came from SettingsForm's initialisation and clamped any out of range values. Undo this
                    AudioConfiguration audioConfig;
                    audioConfig.MasterVolume = (float)masterVolume.Value;
                    audioConfig.SFXVolume = (float)sfxVolume.Value;
                    audioConfig.MusicVolume = (float)musicVolume.Value;
                    audioConfig.VoiceVolume = (float)voiceVolume.Value;
                    undoNextAudioConfig = false;
                    __instance.ApplyConfiguration(audioConfig);
                    __instance.SaveConfiguration();
                }
            }
        }

        public override void OnLateInitializeMelon()
        {
            RumbleModUI.UI.instance.UI_Initialized += OnUIInit;
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            settingsForm = GameObject.FindObjectOfType<Il2CppRUMBLE.UI.SettingsForm>();
            if (settingsForm != null)
            {
                undoNextAudioConfig = true;
            }

            if (doneInit || !AudioManager.instance.ConfigLoaded)
                return;

            RumbleModUI.Tags tags = new RumbleModUI.Tags { DoNotSave = true };

            audioSettings.ModName = "SettingsUI";
            audioSettings.ModVersion = "1.0.0";
            audioSettings.SetFolder("SettingsUI");

            AudioConfiguration audioConfig = AudioManager.instance.audioConfig;

            // Initialize our settings with the saved Rumble settings
            masterVolume = audioSettings.AddToList("Master Volume", audioConfig.MasterVolume, "Master volume. 0-1 covers the normal range. >1 allows boosting beyond the normal volume limit.", tags);
            sfxVolume = audioSettings.AddToList("SFX Volume", audioConfig.SFXVolume, "Sound effects volume. 0-1 covers the normal range. >1 allows boosting beyond the normal volume limit.", tags);
            musicVolume = audioSettings.AddToList("Music Volume", audioConfig.MusicVolume, "Music volume. 0-1 covers the normal range. >1 allows boosting beyond the normal volume limit.", tags);
            voiceVolume = audioSettings.AddToList("Voice Volume", audioConfig.VoiceVolume, "Voice volume. 0-1 covers the normal range. >1 allows boosting beyond the normal volume limit.", tags);

            masterVolume.SavedValueChanged += masterVolumeChanged;
            sfxVolume.SavedValueChanged += sfxVolumeChanged;
            musicVolume.SavedValueChanged += musicVolumeChanged;
            voiceVolume.SavedValueChanged += voiceVolumeChanged;

            doneInit = true;
        }

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

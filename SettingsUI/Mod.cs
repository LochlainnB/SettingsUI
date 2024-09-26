using MelonLoader;
using RumbleModdingAPI;
using UnityEngine;
using HarmonyLib;
using System;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Serialization;
using Il2CppRUMBLE.Interactions.InteractionBase;
using System.Collections;

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
        private static bool configChangeFromSelf = false;

        private static Il2CppRUMBLE.UI.SettingsForm settingsForm;

        // How much the forearm slider should allow boosting beyond the normal volume limit
        private static float boostFactor = 2.0f;

        private static RumbleModUI.Mod audioSettings = new RumbleModUI.Mod();
        private static RumbleModUI.ModSetting<float> masterVolume;
        private static RumbleModUI.ModSetting<float> sfxVolume;
        private static RumbleModUI.ModSetting<float> musicVolume;
        private static RumbleModUI.ModSetting<float> voiceVolume;

        private static Slider.Setting voiceSlider;

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
                    configChangeFromSelf = true;
                    __instance.ApplyConfiguration(audioConfig);
                    __instance.SaveConfiguration();
                }
                else if (!configChangeFromSelf)
                {
                    // Call came from SettingsForm after user input. Update our settings
                    // Temporarily remove our event handlers to prevent infinite loops
                    masterVolume.SavedValueChanged -= masterVolumeChanged;
                    sfxVolume.SavedValueChanged -= sfxVolumeChanged;
                    musicVolume.SavedValueChanged -= musicVolumeChanged;
                    voiceVolume.SavedValueChanged -= voiceVolumeChanged;
                    voiceSlider.ValueChanged -= voiceSliderChanged;
                    masterVolume.SavedValue = config.MasterVolume;
                    sfxVolume.SavedValue = config.SFXVolume;
                    musicVolume.SavedValue = config.MusicVolume;
                    voiceVolume.SavedValue = config.VoiceVolume;
                    voiceSlider.Value = config.VoiceVolume;
                    masterVolume.SavedValueChanged += masterVolumeChanged;
                    sfxVolume.SavedValueChanged += sfxVolumeChanged;
                    musicVolume.SavedValueChanged += musicVolumeChanged;
                    voiceVolume.SavedValueChanged += voiceVolumeChanged;
                    voiceSlider.ValueChanged += voiceSliderChanged;
                    masterVolume.Value = config.MasterVolume;
                    sfxVolume.Value = config.SFXVolume;
                    musicVolume.Value = config.MusicVolume;
                    voiceVolume.Value = config.VoiceVolume;
                    RumbleModUI.UI.instance.ForceRefresh();
                }
                else
                {
                    configChangeFromSelf = false;
                }
            }
        }

        // Stop left hand from interacting with the voice slider
        [HarmonyPatch(typeof(InteractionHand), "StartPreInteraction")]
        private static class InteractionHand_StartPreInteraction_Patch
        {
            private static bool Prefix(InteractionHand __instance, InteractionBase interaction)
            {
                if (__instance.gameObject == PlayerManager.instance.localPlayer.Controller.gameObject.transform.GetChild(1).GetChild(1).gameObject && interaction.transform.parent.name == "SettingSlider")
                    return false;
                return true;
            }
        }

        public override void OnLateInitializeMelon()
        {
            Slider.OnLateInitializeMelon();

            RumbleModUI.UI.instance.UI_Initialized += OnUIInit;
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Slider.OnSceneWasLoaded(buildIndex, sceneName);

            settingsForm = GameObject.FindObjectOfType<Il2CppRUMBLE.UI.SettingsForm>();
            if (settingsForm != null)
            {
                undoNextAudioConfig = true;
            }

            if (doneInit || !AudioManager.instance.ConfigLoaded)
                return;

            RumbleModUI.Tags tags = new RumbleModUI.Tags { DoNotSave = true };

            audioSettings.ModName = "SettingsUI";
            audioSettings.ModVersion = "1.1.2";
            audioSettings.SetFolder("SettingsUI");

            AudioConfiguration audioConfig = AudioManager.instance.audioConfig;

            // Initialize our settings with the saved Rumble settings
            masterVolume = audioSettings.AddToList("Master Volume", audioConfig.MasterVolume, "Master volume. 0-1 covers the normal range. >1 allows boosting beyond the normal volume limit.", tags);
            sfxVolume = audioSettings.AddToList("SFX Volume", audioConfig.SFXVolume, "Sound effects volume. 0-1 covers the normal range. >1 allows boosting beyond the normal volume limit.", tags);
            musicVolume = audioSettings.AddToList("Music Volume", audioConfig.MusicVolume, "Music volume. 0-1 covers the normal range. >1 allows boosting beyond the normal volume limit.", tags);
            voiceVolume = audioSettings.AddToList("Voice Volume", audioConfig.VoiceVolume, "Voice volume. 0-1 covers the normal range. >1 allows boosting beyond the normal volume limit.", tags);
            audioSettings.GetFromFile();
            masterVolume.SavedValueChanged += masterVolumeChanged;
            sfxVolume.SavedValueChanged += sfxVolumeChanged;
            musicVolume.SavedValueChanged += musicVolumeChanged;
            voiceVolume.SavedValueChanged += voiceVolumeChanged;

            voiceSlider = Slider.AddSetting("Global Voice Volume", "Lets you adjust voice chat volume with the arm slider. The slider's range is 0%-200%.", 0.0f, 1.0f * boostFactor, audioConfig.VoiceVolume, 2);
            voiceSlider.ValueChanged += voiceSliderChanged;

            doneInit = true;
        }

        public override void OnFixedUpdate()
        {
            Slider.OnFixedUpdate();
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
            configChangeFromSelf = true;
            AudioManager.instance.ApplyConfiguration(audioConfig);
            AudioManager.instance.SaveConfiguration();

            if (settingsForm != null)
            {
                switch (type)
                {
                    case SettingType.MasterVolume:
                        settingsForm.masterVolumeSlider.MoveToValue(settingsForm.masterVolumeSlider.ConvertToValue(Math.Min(Math.Max(value, 0.0f), 1.0f)));
                        break;
                    case SettingType.SFXVolume:
                        settingsForm.effectsVolumeSlider.MoveToValue(settingsForm.effectsVolumeSlider.ConvertToValue(Math.Min(Math.Max(value, 0.0f), 1.0f)));
                        break;
                    case SettingType.MusicVolume:
                        settingsForm.musicVolumeSlider.MoveToValue(settingsForm.musicVolumeSlider.ConvertToValue(Math.Min(Math.Max(value, 0.0f), 1.0f)));
                        break;
                    case SettingType.VoiceVolume:
                        settingsForm.dialogueVolumeSlider.MoveToValue(settingsForm.dialogueVolumeSlider.ConvertToValue(Math.Min(Math.Max(value, 0.0f), 1.0f)));
                        break;
                }
            }
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
            voiceSlider.ValueChanged -= voiceSliderChanged;
            voiceSlider.Value = ((RumbleModUI.ValueChange<float>)args).Value;
            voiceSlider.ValueChanged += voiceSliderChanged;
        }
        public static void voiceSliderChanged(object sender, Slider.ValueChangedEventArgs args)
        {
            ChangeSetting(SettingType.VoiceVolume, args.Value);
            voiceVolume.SavedValueChanged -= voiceVolumeChanged;
            voiceVolume.SavedValue = args.Value;
            voiceVolume.SavedValueChanged += voiceVolumeChanged;
            voiceVolume.Value = args.Value;
            RumbleModUI.UI.instance.ForceRefresh();
        }
    }
}

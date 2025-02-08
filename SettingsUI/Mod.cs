using MelonLoader;
using ArmSlider;
using UnityEngine;
using HarmonyLib;
using System;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.IO;

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

        // How much the forearm slider should allow boosting beyond the normal volume limit
        private static float boostFactor = 4.0f;
        // Upper limit for the individual voice volume slider
        private static float individualVolumeLimit = 2.0f;
        // Path to the individual voice volumes settings file
        private static string individualVolumesPath = "UserData/SettingsUI/IndividualVolumes.txt";

        private static bool doneInit = false;
        private static bool undoNextAudioConfig = false;
        private static bool configChangeFromSelf = false;

        private static Il2CppRUMBLE.UI.SettingsForm settingsForm;

        private static RumbleModUI.Mod audioSettings = new RumbleModUI.Mod();
        private static RumbleModUI.ModSetting<float> masterVolume;
        private static RumbleModUI.ModSetting<float> sfxVolume;
        private static RumbleModUI.ModSetting<float> musicVolume;
        private static RumbleModUI.ModSetting<float> voiceVolume;

        private static Slider.Setting masterSlider;
        private static Slider.Setting sfxSlider;
        private static Slider.Setting musicSlider;
        private static Slider.Setting voiceSlider;
        private static Slider.Setting individualVolumeSlider;

        private static Dictionary<string, float> individualVolumes = new Dictionary<string, float>();
        private static string opponentName = "";
        private static bool doSave = false;

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
                    masterSlider.ValueChanged -= masterSliderChanged;
                    sfxSlider.ValueChanged -= sfxSliderChanged;
                    musicSlider.ValueChanged -= musicSliderChanged;
                    voiceSlider.ValueChanged -= voiceSliderChanged;
                    masterVolume.SavedValue = config.MasterVolume;
                    sfxVolume.SavedValue = config.SFXVolume;
                    musicVolume.SavedValue = config.MusicVolume;
                    voiceVolume.SavedValue = config.VoiceVolume;
                    masterSlider.Value = config.MasterVolume;
                    sfxSlider.Value = config.SFXVolume;
                    musicSlider.Value = config.MusicVolume;
                    voiceSlider.Value = config.VoiceVolume;
                    masterVolume.SavedValueChanged += masterVolumeChanged;
                    sfxVolume.SavedValueChanged += sfxVolumeChanged;
                    musicVolume.SavedValueChanged += musicVolumeChanged;
                    voiceVolume.SavedValueChanged += voiceVolumeChanged;
                    masterSlider.ValueChanged += masterSliderChanged;
                    sfxSlider.ValueChanged += sfxSliderChanged;
                    musicSlider.ValueChanged += musicSliderChanged;
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

        public override void OnLateInitializeMelon()
        {
            RumbleModUI.UI.instance.UI_Initialized += OnUIInit;
            LoadIndividualVolumes();

            masterSlider = Slider.AddSetting("Master Volume", "Lets you adjust master volume with the arm slider. The slider's range is 0%-400%.", 0.0f, 1.0f * boostFactor, 0.0f, 2);
            sfxSlider = Slider.AddSetting("SFX Volume", "Lets you adjust sound effects volume with the arm slider. The slider's range is 0%-400%.", 0.0f, 1.0f * boostFactor, 0.0f, 2);
            musicSlider = Slider.AddSetting("Music Volume", "Lets you adjust music volume with the arm slider. The slider's range is 0%-400%.", 0.0f, 1.0f * boostFactor, 0.0f, 2);
            voiceSlider = Slider.AddSetting("Global Voice Volume", "Lets you adjust voice chat volume with the arm slider. The slider's range is 0%-400%.", 0.0f, 1.0f * boostFactor, 0.0f, 2);
            individualVolumeSlider = Slider.AddSetting("Individual Voice Volume", "Lets you adjust the volume of individual players' voices. The slider's range is 0%-200%.", 0.0f, individualVolumeLimit, 1.0f, 2);
            masterSlider.ValueChanged += masterSliderChanged;
            sfxSlider.ValueChanged += sfxSliderChanged;
            musicSlider.ValueChanged += musicSliderChanged;
            voiceSlider.ValueChanged += voiceSliderChanged;
            individualVolumeSlider.ValueChanged += individualVolumeSliderChanged;
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (!doneInit && AudioManager.instance.ConfigLoaded)
            {
                RumbleModUI.Tags tags = new RumbleModUI.Tags { DoNotSave = true };

                audioSettings.ModName = "SettingsUI";
                audioSettings.ModVersion = "1.3.1";
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
                
                masterSlider.Value = audioConfig.MasterVolume;
                sfxSlider.Value = audioConfig.SFXVolume;
                musicSlider.Value = audioConfig.MusicVolume;
                voiceSlider.Value = audioConfig.VoiceVolume;

                doneInit = true;
            }

            if (sceneName == "Gym")
            {
                settingsForm = GameObject.FindObjectOfType<Il2CppRUMBLE.UI.SettingsForm>();
                if (settingsForm != null)
                {
                    undoNextAudioConfig = true;
                }

                ResetIndividualVolume();
            }

            else if (sceneName == "Map0" || sceneName == "Map1")
            {
                if (PlayerManager.instance.AllPlayers.Count >= 2)
                {
                    opponentName = PlayerManager.instance.AllPlayers[1].Data.GeneralData.PlayFabMasterId;
                    if (individualVolumes.ContainsKey(opponentName))
                    {
                        individualVolumeSlider.ValueChanged -= individualVolumeSliderChanged;
                        individualVolumeSlider.Value = individualVolumes[opponentName];
                        individualVolumeSlider.ValueChanged += individualVolumeSliderChanged;
                    }
                    else
                    {
                        individualVolumes[opponentName] = 1.0f;
                        individualVolumeSlider.Value = 1.0f;
                    }
                }
                else
                {
                   ResetIndividualVolume();
                }
            }

            else
            {
                ResetIndividualVolume();
            }
        }

        private static void OnUIInit()
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
                        settingsForm.dialogueVolumeSlider.MoveToValue(settingsForm.dialogueVolumeSlider.ConvertToValue(Math.Min(Math.Max(voiceSlider.Value, 0.0f), 1.0f)));
                        break;
                }
            }
        }

        private static void masterVolumeChanged(object sender, EventArgs args)
        {
            masterSlider.ValueChanged -= masterSliderChanged;
            masterSlider.Value = ((RumbleModUI.ValueChange<float>)args).Value;
            masterSlider.ValueChanged += masterSliderChanged;
            ChangeSetting(SettingType.MasterVolume, ((RumbleModUI.ValueChange<float>)args).Value);
        }
        private static void sfxVolumeChanged(object sender, EventArgs args)
        {
            sfxSlider.ValueChanged -= sfxSliderChanged;
            sfxSlider.Value = ((RumbleModUI.ValueChange<float>)args).Value;
            sfxSlider.ValueChanged += sfxSliderChanged;
            ChangeSetting(SettingType.SFXVolume, ((RumbleModUI.ValueChange<float>)args).Value);
        }
        private static void musicVolumeChanged(object sender, EventArgs args)
        {
            musicSlider.ValueChanged -= musicSliderChanged;
            musicSlider.Value = ((RumbleModUI.ValueChange<float>)args).Value;
            musicSlider.ValueChanged += musicSliderChanged;
            ChangeSetting(SettingType.MusicVolume, ((RumbleModUI.ValueChange<float>)args).Value);
        }
        private static void voiceVolumeChanged(object sender, EventArgs args)
        {
            voiceSlider.ValueChanged -= voiceSliderChanged;
            voiceSlider.Value = ((RumbleModUI.ValueChange<float>)args).Value;
            voiceSlider.ValueChanged += voiceSliderChanged;
            ChangeSetting(SettingType.VoiceVolume, ((RumbleModUI.ValueChange<float>)args).Value * (individualVolumes.ContainsKey(opponentName) ? individualVolumes[opponentName] : 1.0f));
        }
        private static void masterSliderChanged(object sender, Slider.ValueChangedEventArgs args)
        {
            masterVolume.SavedValueChanged -= masterVolumeChanged;
            masterVolume.SavedValue = args.Value;
            masterVolume.SavedValueChanged += masterVolumeChanged;
            ChangeSetting(SettingType.MasterVolume, args.Value);
            masterVolume.Value = args.Value;
            RumbleModUI.UI.instance.ForceRefresh();
        }
        private static void sfxSliderChanged(object sender, Slider.ValueChangedEventArgs args)
        {
            sfxVolume.SavedValueChanged -= sfxVolumeChanged;
            sfxVolume.SavedValue = args.Value;
            sfxVolume.SavedValueChanged += sfxVolumeChanged;
            ChangeSetting(SettingType.SFXVolume, args.Value);
            sfxVolume.Value = args.Value;
            RumbleModUI.UI.instance.ForceRefresh();
        }
        private static void musicSliderChanged(object sender, Slider.ValueChangedEventArgs args)
        {
            musicVolume.SavedValueChanged -= musicVolumeChanged;
            musicVolume.SavedValue = args.Value;
            musicVolume.SavedValueChanged += musicVolumeChanged;
            ChangeSetting(SettingType.MusicVolume, args.Value);
            musicVolume.Value = args.Value;
            RumbleModUI.UI.instance.ForceRefresh();
        }
        private static void voiceSliderChanged(object sender, Slider.ValueChangedEventArgs args)
        {
            voiceVolume.SavedValueChanged -= voiceVolumeChanged;
            voiceVolume.SavedValue = args.Value;
            voiceVolume.SavedValueChanged += voiceVolumeChanged;
            ChangeSetting(SettingType.VoiceVolume, args.Value * (individualVolumes.ContainsKey(opponentName) ? individualVolumes[opponentName] : 1.0f));
            voiceVolume.Value = args.Value;
            RumbleModUI.UI.instance.ForceRefresh();
        }
        private static void individualVolumeSliderChanged(object sender, Slider.ValueChangedEventArgs args)
        {
            if (opponentName != "")
            {
                ChangeSetting(SettingType.VoiceVolume, (float)voiceVolume.SavedValue * args.Value);
                individualVolumes[opponentName] = args.Value;
                doSave = true;
                MelonCoroutines.Start(ScheduleSaveIndividualVolumes());
            }
        }

        private static void ResetIndividualVolume()
        {
            opponentName = "";
            if (individualVolumeSlider != null)
            {
                individualVolumeSlider.ValueChanged -= individualVolumeSliderChanged;
                individualVolumeSlider.Value = 1.0f;
                individualVolumeSlider.ValueChanged += individualVolumeSliderChanged;
                if (voiceVolume != null)
                {
                    ChangeSetting(SettingType.VoiceVolume, (float)voiceVolume.SavedValue);
                }
            }
        }

        private static void LoadIndividualVolumes()
        {
            if (File.Exists(individualVolumesPath))
            {
                // Don't catch errors. Otherwise, the file might be overwritten with an empty file
                string[] lines = File.ReadAllLines(individualVolumesPath);
                individualVolumes.Clear();
                foreach (string line in lines)
                {
                    string[] parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        if (float.TryParse(parts[1], out float volume))
                        {
                            individualVolumes[parts[0]] = volume;
                        }
                    }
                }
            }
            else
            {
                MelonLogger.Msg("Individual volumes file not found. A new one will be created if needed.");
            }
        }

        private static IEnumerator ScheduleSaveIndividualVolumes()
        {
            yield return new WaitForSeconds(1.0f);
            if (doSave)
            {
                SaveIndividualVolumes();
                doSave = false;
            }
        }

        private static void SaveIndividualVolumes()
        {
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, float> kvp in individualVolumes)
            {
                lines.Add(kvp.Key + "=" + kvp.Value);
            }
            try { File.WriteAllLines(individualVolumesPath, lines); }
            catch (Exception e) { MelonLogger.Error("Failed to save individual volumes:\n" + e); }
        }
    }
}

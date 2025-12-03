using UnityEngine;
using UnityEngine.UI;
using System;

public class SettingsBehaviour : GenericMenuItem
{
    [SerializeField]
    private GameObject settingsButtonPrefab, settingsSwitchPrefab, settingsLanguagePrefab;

    private void Start()
    {
        // Apply safe area to the bottom padding of the scrollview's content
        GetComponent<VerticalLayoutGroup>().padding.bottom += (int) SafeAreaHelper.PaddingBottom(transform.root.GetComponent<RectTransform>());

        CreateSwitch(Translation_Inspector.MUSIC,
            (bool on) => 
            {
                Database.instance.music = on;
                if(on)
                {
                    AudioManager.instance.PlayMusic();
                }
                else
                {
                    AudioManager.instance.StopMusic();
                }
            },
            () => 
            {
                return Database.instance.music;
            });

        CreateSwitch(Translation_Inspector.SOUNDS,
            (bool on) =>
            {
                Database.instance.sfx = on;
            },
            () =>
            {
                return Database.instance.sfx;
            });

        CreateSwitch(Translation_Inspector.SCIENTIFIC_NOTATION,
            (bool on) =>
            {
                NumberFormatter.scientificNotation = on;
                menu.game.UpdateEverything();
            },
            () =>
            {
                return NumberFormatter.scientificNotation;
            });

        CreateSwitch(Translation_Inspector.BATTERY_SAVER, () => "(" +
#if UNITY_ANDROID
            (int)(Screen.currentResolution.refreshRateRatio.value / Math.Max(1, QualitySettings.vSyncCount))
#elif UNITY_IOS
            Application.targetFrameRate
#else
            "0"
#endif
        + " FPS)", 
        (bool on) => 
        { 
            Database.instance.batterySaver = on;
            SetBatterySaverMode(on);
        }, 
        () =>
        { 
            return Database.instance.batterySaver; 
        });

        // Language switcher
        Instantiate(settingsLanguagePrefab, transform);

        // Show privacy settings button if he should have acces to it
        if (Advertisement.instance.IsPrivacySettingsAdjustable())
        {
            CreateButton(Translation_Inspector.PRIVACY_SETTINGS, Advertisement.instance.ShowPrivacySettings);
        }
    }

    private void CreateSwitch(Translation_Inspector title, Func<string> subtitle, Action<bool> changeValue, Func<bool> currentValue)
    {
        CreateSwitch(title, changeValue, currentValue, nonTranslatableSubtitle: subtitle);
    }

    private void CreateSwitch(Translation_Inspector title, Action<bool> changeValue, Func<bool> currentValue, Func<string> nonTranslatableSubtitle = null)
    {
        GameObject go = Instantiate(settingsSwitchPrefab, transform);
        SettingsSwitchBehaviour script = go.GetComponent<SettingsSwitchBehaviour>();

        script.title = title;
        script.changeValue = changeValue;
        script.currentValue = currentValue;

        if (nonTranslatableSubtitle != null)
        {
            script.nonTranslatableSubtitle = nonTranslatableSubtitle;
        }
    }

    private void CreateButton(Translation_Inspector title, Action callback)
    {
        GameObject button = Instantiate(settingsButtonPrefab, transform);
        button.GetComponent<SettingsButtonBehaviour>().title = title;
        button.GetComponent<SettingsButtonBehaviour>().callback = callback;
    }

    public static void SetBatterySaverMode(bool on)
    {
#if UNITY_ANDROID
        // If BS is turned on, set FPS to half the screen refresh rate (if Hz is larger than 30)
        // If BS is turned off, set FPS to full screen refresh rate
        QualitySettings.vSyncCount = on ? Screen.currentResolution.refreshRateRatio.value > 30 ? 2 : 1 : 1;
#endif

#if UNITY_IOS
        // If BS is turned on, set FPS to the smallest number of 30 or Hz
        // If BS is turned off, set FPS to the smallest number of 60 or Hz
        Application.targetFrameRate = on ? Math.Min(30, (int)Screen.currentResolution.refreshRateRatio.value) : Math.Min(60, (int)Screen.currentResolution.refreshRateRatio.value);
#endif
    }
}
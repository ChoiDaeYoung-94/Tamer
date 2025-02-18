using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace AD
{
    public class SoundManager : MonoBehaviour
    {
        [Header("Audio Mixers")]
        [SerializeField] private AudioMixer _audioMixer = null;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _bgmAudioSource = null;
        [SerializeField] private AudioSource _sfxAudioSource = null;

        [Header("Audio Clips")]
        public AudioClip BgmLoginClip = null;
        public AudioClip BgmMainClip = null;
        public AudioClip BgmSetCharacterClip = null;
        public AudioClip BgmGameClip = null;
        public AudioClip SFXUIClickClip = null;
        public AudioClip SFXUIOkClip = null;
        public AudioClip SFXPunchClip = null;
        public AudioClip SFXSwordClip = null;
        public AudioClip SFXBuffClip = null;
        public AudioClip SFXHealClip = null;
        public AudioClip SFXWalkClip = null;

        public void Init()
        {
            float bgm = PlayerPrefs.GetFloat("BGM", 1f);
            float sfx = PlayerPrefs.GetFloat("SFX", 1f);

            SetBGMVolume(bgm);
            SetSFXVolume(sfx);
        }

        #region Functions
        public void PlayBGM(AudioClip clip)
        {
            _bgmAudioSource.clip = clip;
            _bgmAudioSource.Play();
        }

        public void PauseBGM() => _bgmAudioSource.Pause();

        public void UnpauseBGM()
        {
            string temp_scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (temp_scene.Equals(AD.GameConstants.Scene.Login.ToString()))
                PlayBGM(BgmLoginClip);
            else if (temp_scene.Equals(AD.GameConstants.Scene.Main.ToString()))
                PlayBGM(BgmMainClip);
            else if (temp_scene.Equals(AD.GameConstants.Scene.SetCharacter.ToString()))
                PlayBGM(BgmSetCharacterClip);
            else if (temp_scene.Equals(AD.GameConstants.Scene.Game.ToString()))
                PlayBGM(BgmGameClip);
        }

        public void PlaySFX(AudioClip clip)
        {
            _sfxAudioSource.clip = clip;
            _sfxAudioSource.Play();
        }

        public void UI_Ok() => PlaySFX(SFXUIOkClip);

        public void UI_Click() => PlaySFX(SFXUIClickClip);

        public void SetBGMVolume(float volume)
        {
            _audioMixer.SetFloat("BGM", Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20);

            PlayerPrefs.SetFloat("BGM", volume);
        }

        public void SetSFXVolume(float volume)
        {
            _audioMixer.SetFloat("SFX", Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20);

            PlayerPrefs.SetFloat("SFX", volume);
        }
        #endregion
    }
}
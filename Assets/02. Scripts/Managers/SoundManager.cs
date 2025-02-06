using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace AD
{
    public class SoundManager : MonoBehaviour
    {
        [Header("Audio Mixers")]
        [SerializeField] public AudioMixer _audioMixer = null;

        [Header("Audio Sources")]
        [SerializeField] public AudioSource _AS_bgm = null;
        [SerializeField] public AudioSource _AS_sfx = null;

        [Header("Audio Clips")]
        [SerializeField] public AudioClip _AC_bgm_login = null;
        [SerializeField] public AudioClip _AC_bgm_main = null;
        [SerializeField] public AudioClip _AC_bgm_setCharacter = null;
        [SerializeField] public AudioClip _AC_bgm_game = null;
        [SerializeField] public AudioClip _AC_sfxUI_click = null;
        [SerializeField] public AudioClip _AC_sfxUI_ok = null;
        [SerializeField] public AudioClip _AC_sfx_punch = null;
        [SerializeField] public AudioClip _AC_sfx_sword = null;
        [SerializeField] public AudioClip _AC_sfx_buff = null;
        [SerializeField] public AudioClip _AC_sfx_heal = null;
        [SerializeField] public AudioClip _AC_sfx_walk = null;

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
            _AS_bgm.clip = clip;
            _AS_bgm.Play();
        }

        public void PauseBGM() => _AS_bgm.Pause();

        public void UnpauseBGM()
        {
            string temp_scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (temp_scene.Equals(AD.GameConstants.Scene.Login.ToString()))
                PlayBGM(_AC_bgm_login);
            else if (temp_scene.Equals(AD.GameConstants.Scene.Main.ToString()))
                PlayBGM(_AC_bgm_main);
            else if (temp_scene.Equals(AD.GameConstants.Scene.SetCharacter.ToString()))
                PlayBGM(_AC_bgm_setCharacter);
            else if (temp_scene.Equals(AD.GameConstants.Scene.Game.ToString()))
                PlayBGM(_AC_bgm_game);
        }

        public void PlaySFX(AudioClip clip)
        {
            _AS_sfx.clip = clip;
            _AS_sfx.Play();
        }

        public void UI_Ok() => PlaySFX(_AC_sfxUI_ok);

        public void UI_Click() => PlaySFX(_AC_sfxUI_click);

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
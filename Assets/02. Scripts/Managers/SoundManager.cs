using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace AD
{
    public class SoundManager : MonoBehaviour
    {
        [Header("Audio Mixers")]
        [SerializeField] internal AudioMixer _audioMixer = null;

        [Header("Audio Sources")]
        [SerializeField] internal AudioSource _AS_bgm = null;
        [SerializeField] internal AudioSource _AS_sfx = null;

        [Header("Audio Clips")]
        [SerializeField] internal AudioClip _AC_bgm_login = null;
        [SerializeField] internal AudioClip _AC_bgm_main = null;
        [SerializeField] internal AudioClip _AC_bgm_setCharacter = null;
        [SerializeField] internal AudioClip _AC_bgm_game = null;
        [SerializeField] internal AudioClip _AC_sfxUI_click = null;
        [SerializeField] internal AudioClip _AC_sfxUI_ok = null;
        [SerializeField] internal AudioClip _AC_sfx_punch = null;
        [SerializeField] internal AudioClip _AC_sfx_sword = null;
        [SerializeField] internal AudioClip _AC_sfx_buff = null;
        [SerializeField] internal AudioClip _AC_sfx_heal = null;
        [SerializeField] internal AudioClip _AC_sfx_walk = null;

        internal void Init()
        {
            float bgm = PlayerPrefs.GetFloat("BGM", 1f);
            float sfx = PlayerPrefs.GetFloat("SFX", 1f);

            SetBGMVolume(bgm);
            SetSFXVolume(sfx);
        }

        #region Functions
        internal void PlayBGM(AudioClip clip)
        {
            _AS_bgm.clip = clip;
            _AS_bgm.Play();
        }

        internal void PauseBGM() => _AS_bgm.Pause();

        internal void UnpauseBGM()
        {
            string temp_scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (temp_scene.Equals(AD.Define.Scenes.Login.ToString()))
                PlayBGM(_AC_bgm_login);
            else if (temp_scene.Equals(AD.Define.Scenes.Main.ToString()))
                PlayBGM(_AC_bgm_main);
            else if (temp_scene.Equals(AD.Define.Scenes.SetCharacter.ToString()))
                PlayBGM(_AC_bgm_setCharacter);
            else if (temp_scene.Equals(AD.Define.Scenes.Game.ToString()))
                PlayBGM(_AC_bgm_game);
        }

        internal void PlaySFX(AudioClip clip)
        {
            _AS_sfx.clip = clip;
            _AS_sfx.Play();
        }

        public void UI_Ok() => PlaySFX(_AC_sfxUI_ok);

        public void UI_Click() => PlaySFX(_AC_sfxUI_click);

        internal void SetBGMVolume(float volume)
        {
            _audioMixer.SetFloat("BGM", Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20);

            PlayerPrefs.SetFloat("BGM", volume);
        }

        internal void SetSFXVolume(float volume)
        {
            _audioMixer.SetFloat("SFX", Mathf.Log10(Mathf.Clamp(volume, 0.0001f, 1f)) * 20);

            PlayerPrefs.SetFloat("SFX", volume);
        }
        #endregion
    }
}
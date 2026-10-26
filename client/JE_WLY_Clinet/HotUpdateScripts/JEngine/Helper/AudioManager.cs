using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JEngine.Helper;
using JEngine.Core;
using System;
using HotUpdateScripts;

public class AudioManager : MonoBehaviour {

	public bool isOpenSoundEffect  
	{ 
		get 
		{
			return Convert.ToBoolean(GameUtil.Instance.GetPlayerPrefsInt(GameConst.SystemSettingOption.SoundEffect.ToString(), 1));
		} 
		set
		{
            GameUtil.Instance.SetPlayerPrefs(GameConst.SystemSettingOption.SoundEffect.ToString(), value ? 1 : 0); 
		} 
	}

	public bool isOpenBgMusic
	{ 
		get
		{
			return Convert.ToBoolean(GameUtil.Instance.GetPlayerPrefsInt(GameConst.SystemSettingOption.BgMusic.ToString(), 1)); 
		} 
		set 
		{
            GameUtil.Instance.SetPlayerPrefs(GameConst.SystemSettingOption.BgMusic.ToString(), value ? 1 : 0);
		}
	}

	private string BasePath = "Other/Audios/";


	string prevBgMusic;
	string prevLoopSound;

	private AudioSource m_bgMusicSource;
	private AudioSource m_shotMusicSource;
	private AudioSource m_LoopSoundSource;

    private Dictionary<string, AudioClip> m_cacheAudio = new Dictionary<string, AudioClip>();

    private static AudioManager instance;
	public static AudioManager Instance
	{
		get
		{
			if(instance == null)
			{
				GameObject audioGo = new GameObject("AudioManager");
				GameObject shotSource = new GameObject ("OneShot");
				GameObject loopSource = new GameObject("LoopAudio");
				shotSource.transform.SetParent(audioGo.transform);

				instance = audioGo.AddComponent<AudioManager>();
				instance.m_bgMusicSource = audioGo.AddComponent<AudioSource>();
				instance.m_shotMusicSource = shotSource.AddComponent<AudioSource>();
				instance.m_LoopSoundSource = loopSource.AddComponent<AudioSource>();
				DontDestroyOnLoad(audioGo);
			}
			return instance;
		}
	}

	void Awake (){

	} 
	

	public void SetBgAudioVolume(float value){
		m_bgMusicSource.volume = value;
	}

	public float GetBgAudioVolume(){
		return m_bgMusicSource.volume;
	}

	public void SetShotAudioVolume(float value){
		m_shotMusicSource.volume = value;
		m_LoopSoundSource.volume = value;
	}

	public float GetShotAudioVolume(){
		return m_shotMusicSource.volume;
	}

	/// <summary>
	/// 通过配置播放音效
	/// </summary>
	/// <param name="_sound"></param>
	public void PlaySound(AudioKey.Sound _sound)
	{
        PlaySound(AudioKey.SoundDic[_sound]);
	}

	/// <summary>
	/// 通过相对路径播放音效
	/// </summary>
	/// <param name="clipPath"></param>
	/// <param name="volumeScale"></param>
	public async void PlaySound(string clipPath,float volumeScale = 1f){
		if(isOpenSoundEffect)
        {
            if (m_cacheAudio.ContainsKey(clipPath))
            {
				PlaySound(m_cacheAudio[clipPath], volumeScale);
            }
            else
            {
    //            AudioClip clip = await ResouseHelper.Instance.LoadAssetAsync<AudioClip>(BasePath + clipPath+".ogg");
    //            m_cacheAudio[clipPath] = clip;
				//PlaySound(clip, volumeScale);
                await ResouseHelper.Instance.LoadAudioClipAssetAsync(BasePath + clipPath, (clip) => {
                    m_cacheAudio[clipPath] = clip;
                    PlaySound(clip, volumeScale);
                });
            }
		}
	}
	
	private int currentPlayingCount = 0;
    private void PlaySound(AudioClip clip,float volumeScale = 1f){
		if (isOpenSoundEffect)
        {
			if(clip == null || currentPlayingCount > 10) return;
			currentPlayingCount++;
            m_shotMusicSource.PlayOneShot(clip, volumeScale);

			GameHelper.Instance.SetDeayTimeDo(() => {
				currentPlayingCount--;
			}, clip.length );
		}
	}

	/// <summary>
	/// 通过配置播放背景音乐
	/// </summary>
	/// <param name="_sound"></param>
	public void PlayBgMusic(AudioKey.Sound _sound)
	{
		PlayBgMusic(AudioKey.SoundDic[_sound]);
	}

	/// <summary>
	/// 通过路径播放背景音乐
	/// </summary>
	/// <param name="clipPath"></param>
	public async void PlayBgMusic(string clipPath){
		if (prevBgMusic == clipPath)
		{
            return;
        }
		if (m_cacheAudio.ContainsKey(clipPath))
		{
            PlayBgMusic(m_cacheAudio[clipPath]);
		}
		else
		{
            await ResouseHelper.Instance.LoadAudioClipAssetAsync(BasePath + clipPath, (clip) => {
				m_cacheAudio[clipPath] = clip;
                PlayBgMusic(clip);
            });
        }
          
    }

	private void PlayBgMusic(AudioClip clip){
		if(clip == null)
        {
			return;
		}
		string clipName = clip.name;

		if (prevBgMusic == clipName) { return; } else { StopBgMusic(); }


		m_bgMusicSource.loop = true;
		m_bgMusicSource.clip = clip;

		if(m_bgMusicSource.clip != null && m_bgMusicSource.isPlaying == false && isOpenBgMusic)
		{
			m_bgMusicSource.Play();
		}

		prevBgMusic = clipName;
	}

	public void StopBgMusic()
	{
		if (m_bgMusicSource != null && m_bgMusicSource.isPlaying)
		{
			m_bgMusicSource.Stop();
			m_bgMusicSource.clip = null;

		}
		prevBgMusic = null;
	}

	public void ReSumeBgMusic()
	{
		if (m_bgMusicSource != null && m_bgMusicSource.clip != null)
		{
			m_bgMusicSource.Play();
		}
	}


	public void PauseBgMusic()
	{
		if (m_bgMusicSource != null && m_bgMusicSource.clip != null)
		{
			m_bgMusicSource.Pause();
		}
	}

	/// <summary>
	/// 通过配置播放循环音效
	/// </summary>
	/// <param name="_sound"></param>
	public void PlayLoopSound(AudioKey.Sound _sound)
	{
		PlayLoopSound(AudioKey.SoundDic[_sound]);
	}

	/// <summary>
	/// 通过路径播放循环音效
	/// </summary>
	/// <param name="clipPath"></param>
	/// <param name="volumeScale"></param>
	public async void PlayLoopSound(string clipPath, float volumeScale = 1f)
	{
		if (prevLoopSound == clipPath)
			return;
		if (m_cacheAudio.ContainsKey(clipPath))
		{
			PlayLoopSound(m_cacheAudio[clipPath], volumeScale);
		}
		else
		{
            await ResouseHelper.Instance.LoadAudioClipAssetAsync(BasePath + clipPath , (clip) => {
                m_cacheAudio[clipPath] = clip;
                PlayLoopSound(clip, volumeScale);
            });
		}

	}

	private void PlayLoopSound(AudioClip clip, float volumeScale = 1f)
	{
		if (clip == null)
		{
			return;
		}
		string clipName = clip.name;
		if (prevLoopSound == clipName) { return; } else { StopLoopSound(); }
		m_LoopSoundSource.loop = true;
		m_LoopSoundSource.clip = clip;
		if (m_LoopSoundSource.clip != null && m_LoopSoundSource.isPlaying == false)
		{
			m_LoopSoundSource.Play();
		}
		prevBgMusic = clipName;
	}

	public void StopLoopSound(){
		if(m_LoopSoundSource != null && m_LoopSoundSource.isPlaying){
			m_LoopSoundSource.Stop();
			m_LoopSoundSource.clip = null;
		}
		prevLoopSound = null;
	}
	
	
	public void OnMusic(bool isOn){
		isOpenBgMusic = isOn;
		if(isOpenBgMusic == false){
			PauseBgMusic();
		}
		else{
			if(m_bgMusicSource != null && m_bgMusicSource.isPlaying == false){
				m_bgMusicSource.loop = true;
				if(m_bgMusicSource.clip != null){
					m_bgMusicSource.Play();
				}
			}
		}
		
	}
    public void OnSound(bool isOn)
    {
        isOpenSoundEffect  = isOn; 
    }



}

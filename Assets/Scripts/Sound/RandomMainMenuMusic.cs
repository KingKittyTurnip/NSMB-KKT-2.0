using UnityEngine;
using NSMB.Sound;

public class RandomMainMenuMusic : MonoBehaviour {
    [SerializeField] private LoopingMusicPlayer loopingmusicplayer;

    [SerializeField] private LoopingMusicData[] Musics;

    public void OnEnable() {
        loopingmusicplayer.Play(Musics[Random.Range(0, Musics.Length)], true);
    }
}

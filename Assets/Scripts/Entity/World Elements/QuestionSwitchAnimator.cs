using Quantum;
using UnityEngine;
using System.Collections;

public unsafe class QuestionSwitchAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource globalSfx;
    [SerializeField] private AudioSource sfx;
    [Space]
    [SerializeField] private AudioClip ActivateSound;
    [SerializeField] private AudioClip Music, MusicEnd, Appear;

    public void Start() {
        QuantumEvent.Subscribe<EventQuestionSwitchAnimation>(this, OnAnimation);
    }

    private void OnAnimation(EventQuestionSwitchAnimation e) {
        if (e.Entity != EntityRef) {
            return;
        }

        if (e.ForHitSwitch) {
            //set stuff up with the hit switch
            sfx.PlayOneShot(ActivateSound);

            //SetupMusicPlayer
            StopCoroutine(HandleMusic());
            StartCoroutine(HandleMusic());
        } else {
            //Play animation for all of the same switches
            if (e.Activate) {
                animator.Play("Hit");
            } else {
                animator.Play("Idle");
            }
        }
    }

    private IEnumerator HandleMusic() {
        //Normal
        int interval = 5;
        float timer = interval+1;
        globalSfx.clip = Music;

        tryInterval:
        while (timer > 0) {
            if (timer < interval) {
                interval--;
                globalSfx.Play();
            }
            timer -= Time.deltaTime;
            yield return null;
        }
        //Ending
        if (globalSfx.clip != MusicEnd) {
            interval = 3;
            timer = interval;
            globalSfx.clip = MusicEnd;
            goto tryInterval;
        }
        yield break;
    }
}
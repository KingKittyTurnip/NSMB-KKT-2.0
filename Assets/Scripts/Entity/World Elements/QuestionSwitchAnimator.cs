using Quantum;
using UnityEngine;
using System.Collections;

public unsafe class QuestionSwitchAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource captainSfx;

    public void Start() {
        QuantumEvent.Subscribe<EventQuestionSwitchAnimation>(this, OnAnimation);
        QuantumEvent.Subscribe<EventQuestionSwitchEndMusic>(this, OnEndMusic);
    }

    private void OnAnimation(EventQuestionSwitchAnimation e) {
        if (e.Entity != EntityRef) {
            return;
        }

        if (e.Activate) {
            animator.Play("Hit");
            captainSfx.volume = 1;
            captainSfx.Play();
        } else {
            animator.Play("Idle");
        }
    }
    private void OnEndMusic(EventQuestionSwitchEndMusic e) {
        if (this.gameObject == captainSfx.gameObject) {
            Debug.Log("END");
            StartCoroutine(FadeOutMusic());
        }
    }

    private IEnumerator FadeOutMusic() {
        float timer = 1;

        while (timer > 0) {
            timer -= Time.deltaTime;
            captainSfx.volume = timer;
            yield return null;
        }
    }
}
using NSMB.Utilities.Components;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;

public class SwapStuffWithMonthEvent : MonoBehaviour {

    [SerializeField] private Animator ourAnimator;
    [SerializeField] private SpriteRenderer ourSpriteRenderer;
    //[SerializeField] private LegacyAnimateSpriteRenderer ourLegacyAnimator;
    [Space]
    [SerializeField] private GameObject[] EnableObjects;
    [SerializeField] private AnimatorController[] Controllers;
    //[SerializeField] private List<Sprite[]> LegacyFrames;
    [SerializeField] private Sprite[] Sprites;
    [SerializeField] private Color32[] Colors;


    void Awake() {
        //enable the correct stuff for event
        int EventNumb = (int) MonthEventScript.CurrentEventWeek;
        if (EnableObjects.Length != 0) {
            //used for objects with more complicated parts
            CheckIfInvalid(EnableObjects[EventNumb] == null);
            for (var i = 0; i < EnableObjects.Length; i++) {
                if (EnableObjects[i] != null) {
                    EnableObjects[i].SetActive(i == EventNumb);
                }
            }
        }

        if (Controllers.Length != 0) {
            //used for objects animated with the controller
            CheckIfInvalid(Controllers[EventNumb] == null);
            ourAnimator.runtimeAnimatorController = Controllers[EventNumb];
        }

        /*if (LegacyFrames.Count != 0) {
            //used for objects animated using the legacy animator
            CheckIfInvalid(LegacyFrames[EventNumb].Length == 0);
            ourLegacyAnimator.frames = LegacyFrames[EventNumb];
        }*/

        if (Sprites.Length != 0) {
            //used for single sprite objects
            CheckIfInvalid(Sprites[EventNumb] == null);
            ourSpriteRenderer.sprite = Sprites[EventNumb];
        }

        if (Colors.Length != 0) {
            //used for ui typically
            CheckIfInvalid(Colors[EventNumb] == Color.clear);
            ourSpriteRenderer.color = Colors[EventNumb];
        }

        void CheckIfInvalid(bool check) {
            if (check)
                EventNumb = 0;
        }
    }
}

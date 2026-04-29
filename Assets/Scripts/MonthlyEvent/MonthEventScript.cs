using System;
using System.Configuration;
using UnityEngine;

public class MonthEventScript : MonoBehaviour {

    public static EventWeek CurrentEventWeek = EventWeek.None;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void SetDay() {
        //Debug.Log("Month: " + DateTime.Now.Month + "Day: " + DateTime.Now.Day);

        if (DateTime.Now.Month == 2) { //february
            if (DateTime.Now.Day > 0 && DateTime.Now.Day < 10) {
                SetEvent(EventWeek.Anniversary, "Anniversary Week !!");
            } else if (DateTime.Now.Day == 29) {
                SetEvent(EventWeek.Leap, "Frog Day !!");
            }
        } else if (DateTime.Now.Month == 4) { //april
            if (DateTime.Now.Day == 1) {
                SetEvent(EventWeek.Fools, "April Fools !!");
            }

        } else if (DateTime.Now.Month == 10) { //october
            if (DateTime.Now.Day >= 24) {
                SetEvent(EventWeek.Spooky, "Spooky Week !!");
            }
        }

        //Force
        //SetEvent((EventWeek) 4, "April Fools !!");

        void SetEvent(EventWeek EventEnum, string Log) {
            CurrentEventWeek = EventEnum;
            Debug.Log(Log);
        }
    }

    public enum EventWeek : int {
        None = 0,
        Anniversary = 1,
        Leap = 2,
        Fools = 3,
        Spooky = 4,
        //put new events on the bottom
    }
}
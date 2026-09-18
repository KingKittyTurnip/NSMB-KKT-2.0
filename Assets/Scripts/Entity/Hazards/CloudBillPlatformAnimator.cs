using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;
using System.Collections.Generic;

public unsafe class CloudBillPlatformAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private GameObject TemplateGraphic;
    [SerializeField] private GameObject CloudParent;
    [SerializeField] private List<Animator> CloudList;

    public void Start() {
        QuantumEvent.Subscribe<EventCloudBillCloudAnimation>(this, OnAnimation, FilterOutReplayFastForward);
        QuantumEvent.Subscribe<EventCloudBillCloudBreak>(this, OnBreak, FilterOutReplayFastForward);
    }

    private void OnAnimation(EventCloudBillCloudAnimation e) {
        if (e.Entity != EntityRef) {
            return;
        }

        var cloudplatform = e.f.Unsafe.GetPointer<CloudBillPlatform>(e.Entity);
        var platformtransform = e.f.Unsafe.GetPointer<Transform2D>(e.Entity);
        var list = e.f.ResolveList(cloudplatform->ActiveClouds);
        int Offset = cloudplatform->FacingRight ? -1 : 1;

        if (CloudList.Count < cloudplatform->CurrentClouds && !e.ApprearNew) {
            //create new
            var newCloud = Instantiate(TemplateGraphic, transform.position, Quaternion.identity);
            newCloud.SetActive(true);
            newCloud.transform.SetParent(CloudParent.transform, false);
            CloudList.Insert(0, newCloud.GetComponent<Animator>());
            if (CloudList.Count >= list.Count) {
                CloudList[CloudList.Count-1].SetTrigger("Away");
            }
        } else {
            //Move last cloud to front, what it represents has been removed
            CloudList.Insert(0, CloudList[CloudList.Count-1]);
            CloudList.RemoveAt(CloudList.Count-1);
            CloudList[CloudList.Count-1].SetTrigger("Away");
        }

        //Update visibility
        for (int i = 0; i < list.Count; i++) {
            if (CloudList.Count <= i) {
                break;
            }
            CloudList[i].SetBool("Destroyed", !list[i]);
            CloudList[i].transform.position = new Vector3(platformtransform->Position.X.AsFloat + (i * Offset * 0.5f), platformtransform->Position.Y.AsFloat, 0);
        }
        if (!e.ApprearNew) {
            CloudList[0].SetTrigger("Appear");
        }
    }

    private void OnBreak(EventCloudBillCloudBreak e) {
        if (e.Entity != EntityRef) {
            return;
        }

        //MoveItToFront
        CloudList[e.id].SetBool("Destroyed", true);
    }
}
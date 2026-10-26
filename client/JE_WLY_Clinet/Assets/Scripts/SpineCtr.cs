using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpineCtr : MonoBehaviour
{
    public float Time;
    public string AnimationName;

    void OnEnable()
    {
        Invoke("PlaySpine", Time);
    }

    private void PlaySpine()
    {
        if (GetComponent<SkeletonGraphic>() != null)
        {
            GetComponent<SkeletonGraphic>().AnimationState.ClearTracks();
            GetComponent<SkeletonGraphic>().AnimationState.SetAnimation(0, AnimationName, false);
        }

        if (GetComponent<SkeletonAnimation>() != null)
        {
            GetComponent<SkeletonAnimation>().AnimationState.ClearTracks();
            GetComponent<SkeletonAnimation>().AnimationState.SetAnimation(0, AnimationName, false);
        }
    }
}

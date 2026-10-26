using JEngine.Singleton;
using Spine;
using Spine.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Spine.AnimationState;
using static UIAwards;

namespace JEngine.Helper
{
    public class SpineUtil : SingletonTemplate<SpineUtil>
    {
        private Dictionary<SkeletonAnimation , TrackEntryDelegate> CompleteSkeAnimDic = new Dictionary<SkeletonAnimation , TrackEntryDelegate>();
        private Dictionary<SkeletonGraphic, TrackEntryDelegate> CompleteSkeletonGraphicDic = new Dictionary<SkeletonGraphic, TrackEntryDelegate>();

        private Dictionary<int, string> KuangSpineName = new()
        {
            [2] = "lv",
            [3] = "lan",
            [4] = "zi",
            [6] = "cheng",
            [7] = "hong"

        };
        public void PlayOnce(SkeletonGraphic skeletonGraphic, string animationName, Action callBck)
        {
            CheakAndRemove(skeletonGraphic);
            skeletonGraphic.AnimationState.ClearTracks();
            skeletonGraphic.AnimationState.SetAnimation(0, animationName, false);

            void compCallBack (TrackEntry trackEntry)
            {
                callBck?.Invoke();
                CheakAndRemove(skeletonGraphic);
            }
            skeletonGraphic.AnimationState.Complete += compCallBack;
        }

        public void PlayOnce(SkeletonAnimation skeletonAnimation, string animationName, Action callBck)
        {
            CheakAndRemove(skeletonAnimation);
            skeletonAnimation.skeleton.SetToSetupPose();
            skeletonAnimation.state.ClearTracks();
            skeletonAnimation.state.SetAnimation(0, animationName, false);

            void compCallBack(TrackEntry trackEntry)
            {
                callBck?.Invoke();
                CheakAndRemove(skeletonAnimation);
            }
            skeletonAnimation.state.Complete += compCallBack;
            CompleteSkeAnimDic.Add(skeletonAnimation, compCallBack);
        }

        public void PlayOnce(Transform root, string animationName, Action callBck)
        {
            if (root.GetComponent<SkeletonGraphic>() != null)
            {
                PlayOnce(root.GetComponent<SkeletonGraphic>(), animationName, callBck);
            }
            else if (root.GetComponent<SkeletonAnimation>() != null)
            {
                PlayOnce(root.GetComponent<SkeletonAnimation>(), animationName, callBck);
            }
            else
            {
                UnityEngine.Debug.LogError("PlayOnce 没有找到SkeletonGraphic或SkeletonAnimation组件");
            }

        }

        public void PlayLoop(Transform root, string animationName)
        {
            if (root.GetComponent<SkeletonGraphic>() != null)
            {
                PlayLoop(root.GetComponent<SkeletonGraphic>(), animationName);
            }
            else if (root.GetComponent<SkeletonAnimation>() != null)
            {
                PlayLoop(root.GetComponent<SkeletonAnimation>(), animationName);
            }
            else
            {
                UnityEngine.Debug.LogError("PlayLoop 没有找到SkeletonGraphic或SkeletonAnimation组件");
            }
        }

        public void PlayItemSpineLoop(Transform root, int quality)
        {
            PlayLoop(root, KuangSpineName[quality]);
        }

        public void PlayLoop(SkeletonGraphic skeletonGraphic, string animationName)
        {
            CheakAndRemove(skeletonGraphic);
            skeletonGraphic.Skeleton.SetToSetupPose();
            skeletonGraphic.AnimationState.ClearTracks();
            skeletonGraphic.AnimationState.SetAnimation(0, animationName, true);
        }

        public void PlayLoop(SkeletonAnimation skeletonAnimation, string animationName)
        {
            CheakAndRemove(skeletonAnimation);
            skeletonAnimation.skeleton.SetToSetupPose();
            skeletonAnimation.state.ClearTracks();
            skeletonAnimation.state.SetAnimation(0, animationName, true);
        }

        public void PlayQueue(Transform root, List<string> AnimNameQueue, Action callBck, bool isLoop = false)
        {
            if (root.GetComponent<SkeletonGraphic>() != null)
            {
                PlayQueue(root.GetComponent<SkeletonGraphic>(), AnimNameQueue, callBck);
            }
            else if (root.GetComponent<SkeletonAnimation>() != null)
            {
                PlayQueue(root.GetComponent<SkeletonAnimation>(), AnimNameQueue, callBck, isLoop);
            }
            else
            {
                UnityEngine.Debug.LogError("PlayLoop 没有找到SkeletonGraphic或SkeletonAnimation组件");
            }
        }

        public void PlayQueue(SkeletonGraphic skeletonGraphic, List<string> AnimNameQueue, Action callBck,bool isLoop = false)
        {
            CheakAndRemove(skeletonGraphic);
            skeletonGraphic.AnimationState.ClearTracks();

            skeletonGraphic.AnimationState.SetAnimation(0, AnimNameQueue[0], AnimNameQueue.Count == 1 ? isLoop : false);
            AnimNameQueue.RemoveAt(0);

            void compCallBack(TrackEntry trackEntry)
            {
                CheakAndRemove(skeletonGraphic);
                int QueueCount = AnimNameQueue.Count;
                if (QueueCount > 0)
                {
                    PlayQueue(skeletonGraphic, AnimNameQueue, callBck, isLoop);
                }
                if (QueueCount == 1)
                    callBck?.Invoke();

            }
            skeletonGraphic.AnimationState.Complete += compCallBack;
            CompleteSkeletonGraphicDic.Add(skeletonGraphic, compCallBack);
        }

        public void PlayQueue(SkeletonAnimation skeletonAnimation, List<string> AnimNameQueue, Action callBck, bool isLoop = false)
        {
            CheakAndRemove(skeletonAnimation);
            skeletonAnimation.skeleton.SetToSetupPose();
            skeletonAnimation.state.ClearTracks();
            skeletonAnimation.state.SetAnimation(0, AnimNameQueue[0], AnimNameQueue.Count == 1 ? isLoop : false);
            AnimNameQueue.RemoveAt(0);

            void compCallBack(TrackEntry trackEntry)
            {
                CheakAndRemove(skeletonAnimation);
                int QueueCount = AnimNameQueue.Count;
                if (QueueCount > 0)
                {
                    PlayQueue(skeletonAnimation, AnimNameQueue, callBck, isLoop);
                }
                if (QueueCount == 1)
                    callBck?.Invoke();
            }
            skeletonAnimation.state.Complete += compCallBack;
            CompleteSkeAnimDic.Add(skeletonAnimation, compCallBack);
        }

        private void CheakAndRemove(SkeletonAnimation skeletonAnimation) 
        {
            if (CompleteSkeAnimDic.ContainsKey(skeletonAnimation))
            {
                skeletonAnimation.state.Complete -= CompleteSkeAnimDic[skeletonAnimation];
                CompleteSkeAnimDic.Remove(skeletonAnimation);
            }
        }

        private void CheakAndRemove(SkeletonGraphic skeletonAnimation)
        {
            if (CompleteSkeletonGraphicDic.ContainsKey(skeletonAnimation))
            {
                skeletonAnimation.AnimationState.Complete -= CompleteSkeletonGraphicDic[skeletonAnimation];
                CompleteSkeletonGraphicDic.Remove(skeletonAnimation);
            }
        }
    }
}

namespace FireSafety
{
    public enum HeadsetState { Desktop, Ready, Disconnected, TrackingLost, NotWorn }

    public static class VRTrackingPolicy
    {
        public static HeadsetState Evaluate(bool active, bool previouslyActive, bool valid,
            bool tracked, bool presenceAvailable, bool worn)
        {
            if(!active)return previouslyActive?HeadsetState.Disconnected:HeadsetState.Desktop;
            if(!valid || !tracked)return HeadsetState.TrackingLost;
            if(presenceAvailable && !worn)return HeadsetState.NotWorn;
            return HeadsetState.Ready;
        }
        public static bool Paused(HeadsetState state)
        {return state!=HeadsetState.Desktop && state!=HeadsetState.Ready;}
    }
}

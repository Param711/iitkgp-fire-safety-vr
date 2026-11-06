namespace FireSafety
{
    public static class TrainingInstructions
    {
        public static string FireName(FireKind kind)
        {
            switch(kind)
            {
                case FireKind.EnergizedElectrical:return "ENERGIZED ELECTRICAL EQUIPMENT";
                case FireKind.FlammableLiquid:return "FLAMMABLE LIQUID SPILL";
                default:return "ORDINARY COMBUSTIBLES / PAPER AND WOOD";
            }
        }
        public static string Controls(bool vr)
        {
            return vr?
                "QUEST / VR CONTROLLERS\n\nLeft stick: walk / head tracking: look\nRight stick: 30-degree snap turn\nAim the right ray; green means an interactive target.\nRight trigger: select / open door / alarm / tap\nRight grip: tap once to pick up; tap again to return\nGrab nearby equipment or aim the ray at it.\nLeft trigger: remove held extinguisher pin\nHold right trigger: spray / pour; release to stop\nLeft Y (or menu button): session controls\n\nExtinguisher: cylinder in left hand, nozzle in right.\nBucket: hold its mouth below the running outlet.\nRelease trigger after picking up or closing a panel.\n\nTracking loss pauses the trial. Reconnect both\ncontrollers, then release buttons and centre sticks.":
                "DESKTOP / KEYBOARD AND MOUSE\n\nW A S D: walk\nHold right mouse and move mouse: look\nAim at the object, then press E: interact\nG: pick up or release equipment\nP: remove held extinguisher pin\nHold left mouse: spray / pour\nTab: session controls / Esc: release mouse\nWith the mouse released, click panel buttons.\n\nTap: aim and press E to open / close.\nBucket: stand near a running outlet with bucket held.\nTo pour, aim away from other interactable objects.\n\nIn Unity, keep the Pause button off.";
        }
        public const string Equipment=
            "EQUIPMENT ORIENTATION / BEFORE ASSESSMENT\n\n"+
            "FIRE ALARM: red corridor panel, white press button.\nAim at the button and interact (E / right trigger).\n"+
            "EMERGENCY CALL / office phone: simulated assistance.\nAim at it and use the same interaction control.\n\n"+
            "EXTINGUISHER: grab, pull pin, aim at the fire base,\nsqueeze, then sweep from side to side (PASS).\n"+
            "Only consider a small fire with a clear escape route.\nThe installed extinguisher rating still needs verification.\n\n"+
            "WATER: open a tap, fill bucket, close tap, then pour.\nDo not use water on electrical equipment or liquid fires.\n\n"+
            "These instructions are shown before the trial.\nThe assessment provides no live correctness hints.";
    }
}

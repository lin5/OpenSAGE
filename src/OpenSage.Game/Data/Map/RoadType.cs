﻿using System;

namespace OpenSage.Data.Map
{
    // TODO: Figure these out properly.
    [Flags]
    public enum RoadType : uint
    {
        None = 0,

        Start = 2,
        End = 4,

        Angled = 8,

        Unknown1 = 16,
        Unknown2 = 32,

        TightCurve = 64,

        EndCap = 128,

        [AddedIn(SageGame.BattleForMiddleEarthII)]
        Unknown3 = 256,

        [AddedIn(SageGame.Cnc4)]
        Unknown4 = 512,

        BroadCurveStart = Start,
        BroadCurveEnd = End
    }
}

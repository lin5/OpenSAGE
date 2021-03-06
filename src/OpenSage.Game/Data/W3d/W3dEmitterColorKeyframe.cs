﻿using System.IO;

namespace OpenSage.Data.W3d
{
    public sealed class W3dEmitterColorKeyframe
    {
        public float Time { get; private set; }
        public W3dRgba Color { get; private set; }

        internal static W3dEmitterColorKeyframe Parse(BinaryReader reader)
        {
            return new W3dEmitterColorKeyframe
            {
                Time = reader.ReadSingle(),
                Color = W3dRgba.Parse(reader)
            };
        }
    }
}

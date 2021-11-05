﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixelStacker.Logic.Model
{
    public class PxPoint
    {
        public int X { get; set; }
        public int Y { get; set; }

        public PxPoint() { }
        public PxPoint(SkiaSharp.SKPoint p) { X = (int)p.X; Y = (int)p.Y; }
        public PxPoint(int x, int y) { X = x; Y = y; }
        public PxPoint(float x, float y) { X = (int)x; Y = (int)y; }

        public PxPoint Clone() => new PxPoint(X, Y);
    }
}
﻿using Newtonsoft.Json;
using PixelStacker.Extensions;
using PixelStacker.IO.JsonConverters;
using System;
using System.Drawing;

namespace PixelStacker.Logic.Model
{
    [JsonConverter(typeof(MaterialCombinationJsonConverter))]
    public class MaterialCombination : IEquatable<MaterialCombination>
    {
        #region Constructors
        public MaterialCombination(string pixelStackerID) : this(Materials.FromPixelStackerID(pixelStackerID)) { }

        public MaterialCombination(string pixelStackerIdBottom, string pixelStackerIdTop)
            : this(Materials.FromPixelStackerID(pixelStackerIdBottom), Materials.FromPixelStackerID(pixelStackerIdTop)) { }

        public MaterialCombination(Material m) : this(m, m) { }

        public MaterialCombination(Material mBottom, Material mTop)
        {
            this.Top = mTop;
            this.Bottom = mBottom;
            this.IsMultiLayer = !ReferenceEquals(Top, Bottom); //Top?.PixelStackerID != Bottom?.PixelStackerID;
            if (mBottom == null) throw new ArgumentNullException(nameof(mBottom));
            if (mTop == null) throw new ArgumentNullException(nameof(mTop));
        }
        #endregion Constructors

        public bool IsMultiLayer { get; }
        public Material Top { get; }
        public Material Bottom { get; }

        public Color GetAverageColor(bool isSide) => this.LazyValue(() => {
            return this.GetImage(isSide).GetAverageColor();
        });

        public Bitmap GetImage(bool isSide) => isSide ? this.SideImage : this.TopImage;
        public Bitmap TopImage => this.LazyValue(() => {
            Bitmap rt = Top.TopImage.To32bppBitmap();
            if (IsMultiLayer) return rt;
            Bottom.TopImage.ToMergeStreamParallel(rt, null, (x, y, cLower, cUpper) => cLower.OverlayColor(cUpper));
            return rt;
        });

        public Bitmap SideImage => this.LazyValue(() => {
            Bitmap rt = Top.SideImage.To32bppBitmap();
            if (IsMultiLayer) return rt;
            Bottom.SideImage.ToMergeStreamParallel(rt, null, (x, y, cLower, cUpper) => cLower.OverlayColor(cUpper));
            return rt;
        });


        #region Equality/ Override methods
        public bool Equals(MaterialCombination y)
        {
            var x = this;
            if (x != null ^ y != null) return false;
            if (x == null && y == null) return true;
            if (x.Top.PixelStackerID != y.Top.PixelStackerID) return false;
            if (x.Bottom.PixelStackerID != y.Bottom.PixelStackerID) return false;
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is MaterialCombination mc) return this.Equals(mc);
            return false;
        }

        public int GetHashCode(MaterialCombination x)
        {
            return (x.Top.PixelStackerID+"::"+x.Bottom.PixelStackerID).GetHashCode();
        }

        public override string ToString()
        {
            return $"{Bottom.PixelStackerID}::{Top.PixelStackerID}";
        }

        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        #endregion
    }
}
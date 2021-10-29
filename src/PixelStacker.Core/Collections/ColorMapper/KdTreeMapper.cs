﻿using KdTree;
using PixelStacker.Core;
using PixelStacker.Core.Model;
using PixelStacker.Core.Model.Drawing;
using PixelStacker.Core.Model.Mats;

namespace PixelStacker.Core.Collections.ColorMapper
{
    public class KdTreeMapper : IColorMapper
    {
        public double AccuracyRating => 99.127;
        public double SpeedRating => 232.1;

        private Dictionary<PxColor, MaterialCombination> Cache { get; set; } = new Dictionary<PxColor, MaterialCombination>();

        private bool IsSideView;
        private MaterialPalette Palette;
        private KdTree<float, MaterialCombination> KdTree;
        private object Padlock = new { };

        public void SetSeedData(List<MaterialCombination> combos, MaterialPalette palette, bool isSideView)
        {
            lock (Padlock)
            {
                Cache = null;
                Cache = new Dictionary<PxColor, MaterialCombination>();
                IsSideView = isSideView;
                Palette = palette;
                KdTree = new KdTree<float, MaterialCombination>(3, new KdTree.Math.FloatMath());

                foreach (var cb in combos)
                {
                    var c = cb.GetAverageColor(isSideView);
                    float[] metrics = new float[] { c.R, c.G, c.B };
                    KdTree.Add(metrics, cb);
                }
            }
        }

        public MaterialCombination FindBestMatch(PxColor c)
        {
            lock (Padlock)
            {
                if (Cache.TryGetValue(c, out MaterialCombination mc))
                {
                    return mc;
                }

                if (c.A < 32) return Palette[Constants.MaterialCombinationIDForAir];
                var closest = KdTree.GetNearestNeighbours(new float[] { c.R, c.G, c.B }, 10);
                var found = closest.MinBy(x => x.Value.GetAverageColorDistance(IsSideView, c));
                Cache[c] = found.Value;
                return found.Value;
            }
        }

        /// <summary>
        /// Super SLOW! Avoid if you can help it. Not at all optimized.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="maxMatches"></param>
        /// <returns></returns>
        public List<MaterialCombination> FindBestMatches(PxColor c, int maxMatches)
        {
            lock (Padlock)
            {
                if (c.A < 32) return new List<MaterialCombination>() { Palette[Constants.MaterialCombinationIDForAir] };
                var closest = KdTree.GetNearestNeighbours(new float[] { c.R, c.G, c.B }, 10);

                var found = closest.OrderBy(x => x.Value.GetAverageColorDistance(IsSideView, c))
                    .Take(maxMatches).Select(x => x.Value).ToList();

                return found;
            }
        }

        public bool IsSeeded() => KdTree != null;
    }
}
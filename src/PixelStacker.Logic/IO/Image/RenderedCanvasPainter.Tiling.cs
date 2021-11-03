﻿using PixelStacker.Extensions;
using PixelStacker.Logic.Extensions;
using PixelStacker.Logic.IO.Config;
using PixelStacker.Logic.Model;
using PixelStacker.Logic.Utilities;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PixelStacker.Logic.IO.Image
{
    public partial class RenderedCanvasPainter
    {

        public const int BlocksPerChunk = 38;

        /// Should contain: 
        /// 0 = 1/1 size, when viewing at zoom(tex)+ to zoom(tex * 0.75) 
        /// 1 = 1/2 size
        /// 2 = 1/4 size
        /// 3 = 1/8 size
        private List<SKBitmap[,]> Bitmaps { get; }

        /// <summary>
        /// Initialize the bitmaps by rendering a canvas into image tiles.
        /// </summary>
        /// <returns></returns>
        private static async Task<List<SKBitmap[,]>> RenderIntoTilesAsync(CancellationToken? worker, RenderedCanvas data, int maxLayers)
        {
            worker ??= CancellationToken.None;

            var sizes = CalculateChunkSizes(data, maxLayers);
            worker.SafeThrowIfCancellationRequested();
            var bitmaps = new List<SKBitmap[,]>();

            int chunksFinishedSoFar = 0;
            int totalChunksToRender = 0;
            foreach (var size in sizes)
            {
                totalChunksToRender += size.Length;
                bitmaps.Add(new SKBitmap[size.GetLength(0), size.GetLength(1)]);
            }

            #region LAYER 0
            {
                SKSize[,] sizeSet = sizes[0];
                int scaleDivide = 1;
                int numChunksWide = sizeSet.GetLength(0);
                int numChunksHigh = sizeSet.GetLength(1);
                int srcPixelsPerChunk = BlocksPerChunk * scaleDivide;
                int dstPixelsPerChunk = Constants.TextureSize * srcPixelsPerChunk / scaleDivide;
                int iTask = 0;
                Task[] L0Tasks = new Task[sizes[0].Length];
                for (int cW = 0; cW < numChunksWide; cW++)
                {
                    for (int cH = 0; cH < numChunksHigh; cH++)
                    {
                        int cWf = cW;
                        int cHf = cH;
                        SKSize tileSize = sizeSet[cW, cH];
                        SKRect srcRect = new SKRect()
                        {
                            Location = new SKPoint(cWf * srcPixelsPerChunk, cHf * srcPixelsPerChunk),
                            Size = new SKSize((float)Math.Floor(tileSize.Width * scaleDivide / Constants.TextureSize)
                            , (float)Math.Floor(tileSize.Height * scaleDivide / Constants.TextureSize))
                        };
                        SKRect dstRect = new SKRect()
                        {
                            Location = new SKPoint(cWf * dstPixelsPerChunk, cHf * dstPixelsPerChunk),
                            Size = new SKSize(tileSize.Width, tileSize.Height)
                        };

                        L0Tasks[iTask++] = Task.Run(() =>
                        {
                            var bmToAdd = CreateLayer0Image(data, srcRect, dstRect);
                            bitmaps[0][cWf, cHf] = bmToAdd;
                            int nVal = Interlocked.Increment(ref chunksFinishedSoFar);
                            ProgressX.Report(100 * nVal / totalChunksToRender);
                        }, worker.Value);
                    }
                }

                await Task.WhenAll(L0Tasks);
            }
            #endregion LAYER 0

            #region OTHER LAYERS
            {
                for (int l = 1; l < sizes.Count; l++)
                {
                    SKSize[,] sizeSet = sizes[l];
                    int scaleDivide = (int)Math.Pow(2, l);
                    int numChunksWide = sizeSet.GetLength(0);
                    int numChunksHigh = sizeSet.GetLength(1);
                    int srcPixelsPerChunk = BlocksPerChunk * scaleDivide;
                    int dstPixelsPerChunk = Constants.TextureSize * srcPixelsPerChunk / scaleDivide;

                    for (int x = 0; x < sizeSet.GetLength(0); x++)
                    {
                        for (int y = 0; y < sizeSet.GetLength(1); y++)
                        {
                            SKSize dstSize = sizeSet[x, y];

                            var bm = new SKBitmap((int)dstSize.Width, (int)dstSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
                            using SKCanvas g = new SKCanvas(bm);
                            //g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            //g.InterpolationMode = InterpolationMode.NearestNeighbor;
                            //g.SmoothingMode = SmoothingMode.None;
                            //g.CompositingMode = CompositingMode.SourceOver;

                            // tiles within the chunk. We iterate over the main src image to get our content for our chunk data.
                            for (int xWithinDownsizedChunk = 0; xWithinDownsizedChunk < scaleDivide; xWithinDownsizedChunk++)
                            {
                                for (int yWithinDownsizedChunk = 0; yWithinDownsizedChunk < scaleDivide; yWithinDownsizedChunk++)
                                {
                                    int xIndexOIfL0Chunk = xWithinDownsizedChunk + scaleDivide * x;
                                    int yIndexOfL0Chunk = yWithinDownsizedChunk + scaleDivide * y;
                                    if (xIndexOIfL0Chunk > bitmaps[0].GetLength(0) - 1 || yIndexOfL0Chunk > bitmaps[0].GetLength(1) - 1)
                                        continue;

                                    var bmToPaint = bitmaps[0][xIndexOIfL0Chunk, yIndexOfL0Chunk];
                                    var rect = new SKRect()
                                    {
                                        Location = new SKPoint((float)(xWithinDownsizedChunk * dstPixelsPerChunk / scaleDivide),
                                            (float)(yWithinDownsizedChunk * dstPixelsPerChunk / scaleDivide)),
                                        Size = new SKSize(dstPixelsPerChunk / scaleDivide, dstPixelsPerChunk / scaleDivide)
                                    };

                                    g.DrawBitmap(
                                        bmToPaint,
                                        rect);
                                }
                            }

                            bitmaps[l][x, y] = bm;
                            ProgressX.Report(100 * ++chunksFinishedSoFar / totalChunksToRender);
                        }
                    }
                }
            }
            #endregion OTHER LAYERS

            return bitmaps;
        }

        private static SKSize[,] CalculateChunkSizesForLayer(SKSize srcImageSize, int scaleDivide)
        {
            int srcW = (int)srcImageSize.Width;
            int srcH = (int)srcImageSize.Height;
            int srcPixelsPerChunk = BlocksPerChunk * scaleDivide;
            int dstPixelsPerChunk = Constants.TextureSize * srcPixelsPerChunk / scaleDivide; // 16 * (RenderedCanvasPainter.BlocksPerChunk * N) / N = 6RenderedCanvasPainter.BlocksPerChunk
            int numChunksWide = (int)srcW / srcPixelsPerChunk + (srcW % srcPixelsPerChunk == 0 ? 0 : 1);
            int numChunksHigh = (int)srcH / srcPixelsPerChunk + (srcH % srcPixelsPerChunk == 0 ? 0 : 1);
            var sizeSet = new SKSize[numChunksWide, numChunksHigh];

            // MAX PERFECT WIDTH - ACTUAL WIDTH = difference
            int deltaX = numChunksWide * dstPixelsPerChunk - Constants.TextureSize * srcW / scaleDivide;
            int deltaY = numChunksHigh * dstPixelsPerChunk - Constants.TextureSize * srcH / scaleDivide;
            for (int x = 0; x < numChunksWide; x++)
            {
                int dstWidthOfChunk = x < numChunksWide - 1
                    ? dstPixelsPerChunk // Very simple. We know if it isn't on the tail we can assume a standard full width.
                    : dstPixelsPerChunk - deltaX;
                for (int y = 0; y < numChunksHigh; y++)
                {
                    int dstHeightOfChunk = y < numChunksHigh - 1
                        ? dstPixelsPerChunk // Very simple. We know if it isn't on the tail we can assume a standard full width.
                        : dstPixelsPerChunk - deltaY;

                    sizeSet[x, y] = new SKSize(width: dstWidthOfChunk, height: dstHeightOfChunk);
                }
            }
            return sizeSet;
        }

        private static List<SKSize[,]> CalculateChunkSizes(RenderedCanvas data, int maxLayers)
        {
            int scaleDivide = 1;
            List<SKSize[,]> sizesList = new List<SKSize[,]>();
            SKSize[,] curSizeSet;
            do
            {
                curSizeSet = CalculateChunkSizesForLayer(new SKSize(data.Width, data.Height), scaleDivide);
                sizesList.Add(curSizeSet);
                scaleDivide *= 2;
                maxLayers--;
            } while (
            // Do not split if one dimension is unable to be split further.
            curSizeSet.GetLength(0) > 2 && curSizeSet.GetLength(1) > 2

            // Do not go on forever
            && maxLayers > 0
            );

            return sizesList;
        }
        private static SKBitmap CreateLayer0Image(RenderedCanvas data, SKRect srcTile, SKRect dstTile)
        {
            var bm = new SKBitmap((int)dstTile.Width, (int)dstTile.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            int scaleDivide = (int)(dstTile.Width / srcTile.Width);

            int srcWidth = (int)srcTile.Width;
            int srcHeight = (int)srcTile.Height;
            var canvas = new SKCanvas(bm);
            var paint = new SKPaint()
            {
                BlendMode = SKBlendMode.SrcOver,
                FilterQuality = SKFilterQuality.High,
                IsAntialias = false,
                //Style = SKPaintStyle.Fill
            };

            //for (int y = 0; y < srcHeight; y++)
            Parallel.For(0, srcHeight, (y) =>
            {
                //g.InterpolationMode = InterpolationMode.NearestNeighbor;
                //g.SmoothingMode = SmoothingMode.None;
                //g.PixelOffsetMode = PixelOffsetMode.Half;

                for (int x = 0; x < srcWidth; x++)
                {
                    var loc = srcTile.Location;
                    var mc = data.CanvasData[(int)loc.X + x, (int)loc.Y + y];
                    var toPaint = mc.GetImage(data.IsSideView);
                    canvas.DrawBitmap(toPaint, new SKPoint(x * Constants.TextureSize, y * Constants.TextureSize), paint);
                }
                //}
            });

            return bm;
        }
    }
}

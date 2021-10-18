﻿using PixelStacker.IO.Config;
using PixelStacker.Logic;
using PixelStacker.Logic.Engine;
using PixelStacker.Logic.Model;
using PixelStacker.Logic.Utilities;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace PixelStacker.UI
{
    public partial class CanvasEditor
    {
        private object Padlock = new { };
        private Point initialDragPoint;
        private bool IsDragging = false;
        private RenderedCanvasPainter Painter;

        public RenderedCanvas Canvas { get; private set; }
        private PanZoomSettings PanZoomSettings { get; set; }



        public async Task SetCanvas(CancellationToken? worker, RenderedCanvas canvas, PanZoomSettings pz)
        {
            pz ??= CalculateInitialPanZoomSettings(canvas.PreprocessedImage);
            int? textureSizeOut = RenderCanvasEngine.CalculateTextureSize(canvas.Width, canvas.Height, 2);
            if (textureSizeOut == null)
            {
                ProgressX.Report(100, Resources.Text.Error_ImageTooLarge);
                return;
            }

            int textureSize = textureSizeOut.Value;
            // possible to use faster math?


            ProgressX.Report(0, "Rendering block plan to viewing window.");
            var painter = await RenderedCanvasPainter.Create(worker, canvas);
            this.Painter = painter;
            // DO not set these until ready
            this.Canvas = canvas;
            this.PanZoomSettings = pz;

            this.RepaintRequested = true;
        }

        private PanZoomSettings CalculateInitialPanZoomSettings(Bitmap src)
        {
            var settings = new PanZoomSettings()
            {
                initialImageX = 0,
                initialImageY = 0,
                imageX = 0,
                imageY = 0,
                zoomLevel = 0,
                maxZoomLevel = Constants.MAX_ZOOM,
                minZoomLevel = Constants.MIN_ZOOM
            };

            if (src != null)
            {
                lock (src)
                {
                    double wRatio = (double)Width / src.Width;
                    double hRatio = (double)Height / src.Height;
                    if (hRatio < wRatio)
                    {
                        settings.zoomLevel = hRatio;
                        settings.imageX = (Width - (int)(src.Width * hRatio)) / 2;
                    }
                    else
                    {
                        settings.zoomLevel = wRatio;
                        settings.imageY = (Height - (int)(src.Height * wRatio)) / 2;
                    }

                    int numICareAbout = Math.Max(src.Width, src.Height);
                    settings.minZoomLevel = (100.0D / numICareAbout);
                    if (settings.minZoomLevel > 1.0D)
                    {
                        settings.minZoomLevel = 1.0D;
                    }
                }
            }

            return settings;
        }
    }
}

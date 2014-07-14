/*
 * Copyright © 2013 Nokia Corporation. All rights reserved.
 * Nokia and Nokia Connecting People are registered trademarks of Nokia Corporation.
 * Other product and company names mentioned herein may be trademarks
 * or trade names of their respective owners.
 * See LICENSE.TXT for license information.
 */

using Nokia.Graphics.Imaging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Phone.Media.Capture;
using Windows.Storage.Streams;

//using System.Windows.Media;

namespace RealtimeFilterDemo
{
    public class NokiaImagingSDKEffects : ICameraEffect
    {
        private PhotoCaptureDevice m_PhotoCaptureDevice = null;
        private CameraPreviewImageSource m_StreamImageSource = null;
        private FilterEffect m_FilterEffect = null;
        private CustomEffectBase m_CustomEffect = null;
        private int m_EffectIndex = 0;
        private Semaphore m_Semaphore = new Semaphore(1, 1);
        private Size m_FrameSize;
        private BitmapRenderer m_Renderer;

        public String EffectName { get; private set; }

        public PhotoCaptureDevice PhotoCaptureDevice
        {
            set
            {
                if (m_PhotoCaptureDevice != value)
                {
                    while (!m_Semaphore.WaitOne(100)) ;

                    m_PhotoCaptureDevice = value;

                    Initialize();

                    m_Semaphore.Release();
                }
            }
        }

        ~NokiaImagingSDKEffects()
        {
            while (!m_Semaphore.WaitOne(100)) ;

            Uninitialize();

            m_Semaphore.Release();
        }

        public async Task GetNewFrameAndApplyEffect(IBuffer frameBuffer, Size frameSize)
        {
            if (m_Semaphore.WaitOne(500))
            {
                m_FrameSize = frameSize;

                var scanlineByteSize = (uint)frameSize.Width * 4; // 4 bytes per pixel in BGRA888 mode
                var bitmap = new Bitmap(frameSize, ColorMode.Bgra8888, scanlineByteSize, frameBuffer);
                if (m_Renderer != null)
                {
                    m_Renderer.Bitmap = bitmap;
                    await m_Renderer.RenderAsync();
                }
                else if (m_FilterEffect != null)
                {
                    var renderer = new BitmapRenderer(m_FilterEffect, bitmap);
                    await renderer.RenderAsync();
                }
                else if (m_CustomEffect != null)
                {
                    var renderer = new BitmapRenderer(m_CustomEffect, bitmap);
                    await renderer.RenderAsync();
                }
                else
                {
                    var renderer = new BitmapRenderer(m_StreamImageSource, bitmap);
                    await renderer.RenderAsync();
                }

                m_Semaphore.Release();
            }
        }

        public void NextEffect()
        {
            if (m_Semaphore.WaitOne(500))
            {
                Uninitialize();

                m_EffectIndex++;

                if (m_EffectIndex >= m_EffectCount)
                {
                    m_EffectIndex = 0;
                }

                Initialize();

                m_Semaphore.Release();
            }
        }

        public void PreviousEffect()
        {
            if (m_Semaphore.WaitOne(500))
            {
                Uninitialize();

                m_EffectIndex--;

                if (m_EffectIndex < 0)
                {
                    m_EffectIndex = m_EffectCount - 1;
                }

                Initialize();

                m_Semaphore.Release();
            }
        }

        private void Uninitialize()
        {
            if (m_StreamImageSource != null)
            {
                m_StreamImageSource.Dispose();
                m_StreamImageSource = null;
            }

            if (m_FilterEffect != null)
            {
                m_FilterEffect.Dispose();
                m_FilterEffect = null;
            }

            if (m_CustomEffect != null)
            {
                m_CustomEffect.Dispose();
                m_CustomEffect = null;
            }

            if(m_Renderer != null)
            {
                m_Renderer = null;
            }
        }

        private void Initialize()
        {
            var filters = new List<IFilter>();
            var nameFormat = "{0}/" + m_EffectCount + " - {1}";

            App.AssignedColorCache = new Dictionary<uint, uint>(); // Reset
            m_StreamImageSource = new CameraPreviewImageSource(m_PhotoCaptureDevice);

            switch (m_EffectIndex)
            {

                case 0:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Built-in BrightnessFilter >>> +0.50");
                        filters.Add(new BrightnessFilter(0.50));
                    }
                    break;

                case 1:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Built-in ColorAdjustFilter >>> Red at -1.0");
                        filters.Add(new ColorAdjustFilter(-1.0, 0, 0));
                    }
                    break;

                case 2:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Built-in ColorAdjustFilter >>> Red at +1.0");
                        filters.Add(new ColorAdjustFilter(1.0, 0, 0));
                    }
                    break;

                case 3:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Built-in ColorAdjustFilter >>> Green at -1.0");
                        filters.Add(new ColorAdjustFilter(0, -1.0, 0));
                    }
                    break;

                case 4:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Built-in ColorAdjustFilter >>> Green at +1.0");
                        filters.Add(new ColorAdjustFilter(0, 1.0, 0));
                    }
                    break;

                case 5:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Built-in ColorAdjustFilter >>> Blue at -1.0");
                        filters.Add(new ColorAdjustFilter(0, 0, -1.0));
                    }
                    break;

                case 6:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Built-in ColorAdjustFilter >>> Blue at +1.0");
                        filters.Add(new ColorAdjustFilter(0, 0, 1.0));
                    }
                    break;

                case 7:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Built-in MirrorFilter");
                        filters.Add(new MirrorFilter());
                    }
                    break;
                case 8:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Built-in MirrorFilter and RotateFilter");
                        filters.Add(new RotationFilter(270));
                        filters.Add(new MirrorFilter());
                        filters.Add(new RotationFilter(90));
                    }
                    break;
                case 9:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Built-in GrayscaleFilter");
                        filters.Add(new GrayscaleFilter());
                    }
                    break;
                case 10:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Built-in GrayscaleNegativeFilter");
                        filters.Add(new GrayscaleNegativeFilter());
                    }
                    break;

                case 11:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Built-in NegativeFilter");
                        filters.Add(new NegativeFilter());
                    }
                    break;

                case 12:
                    {
                        //// Dismal performance without Cache
                        //EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "QuantizeColorEffect without Cache - 16 color");
                        //Dictionary<uint, Color> assignedColorCache = null;
                        //_customEffect = new QuantizeColorEffect(m_StreamImageSource, ref assignedColorCache,
                        //    null, QuantizeColorEffect.ColorPalette.Color16);

                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Inbuilt CartoonFilter");
                        filters.Add(new CartoonFilter());
                    }
                    break;

                case 13:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Built-in SepiaFilter");
                        filters.Add(new SepiaFilter());
                    }
                    break;

                case 14:
                    {
                        EffectName = String.Format(nameFormat, (m_EffectIndex + 1), "Orton Effect");
                        FilterEffect bgFilters = new FilterEffect(m_StreamImageSource) { Filters = new IFilter[] { new BlendFilter(m_StreamImageSource, BlendFunction.Screen, 1.0) } };
                        int blurFact = 45;
                        FilterEffect fgFilters = new FilterEffect(bgFilters) { Filters = new IFilter[] { new BlurFilter(blurFact) } };
                        FilterEffect fFilters = new FilterEffect(bgFilters) { Filters = new IFilter[] { new BlendFilter(fgFilters, BlendFunction.Multiply, 1.0) } };
                        m_Renderer = new BitmapRenderer(fFilters);
                    }
                    break;
            }

            if (filters.Count > 0)
            {
                m_FilterEffect = new FilterEffect(m_StreamImageSource)
                {
                    Filters = filters
                };
            }
        }

        private int m_EffectCount = 15;  // Remember to increment by one with each case added above.
    }
}
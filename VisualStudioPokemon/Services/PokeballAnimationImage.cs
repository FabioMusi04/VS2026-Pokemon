using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VisualStudioPokemon.Services
{
    internal sealed class PokeballAnimationImage : Image
    {
        private readonly DispatcherTimer timer;
        private BitmapSource? sourceSheet;
        private int frameIndex;
        private int frameCount;
        private int frameSize;

        public event EventHandler Completed = delegate { };

        public PokeballAnimationImage()
        {
            Stretch = Stretch.Uniform;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(75) };
            timer.Tick += Timer_Tick;
        }

        public void Play(string path)
        {
            Stop();
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Completed?.Invoke(this, EventArgs.Empty);
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            sourceSheet = bitmap;
            frameSize = Math.Min(sourceSheet.PixelWidth, sourceSheet.PixelHeight);
            frameCount = Math.Max(1, sourceSheet.PixelHeight / frameSize);
            frameIndex = 0;
            SetFrame();
            timer.Start();
        }

        public void Stop()
        {
            timer.Stop();
            Source = null;
            sourceSheet = null;
            frameIndex = 0;
            frameCount = 0;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            frameIndex++;
            if (frameIndex >= frameCount)
            {
                Stop();
                Completed?.Invoke(this, EventArgs.Empty);
                return;
            }

            SetFrame();
        }

        private void SetFrame()
        {
            if (sourceSheet == null || frameSize <= 0)
            {
                return;
            }

            var frame = new CroppedBitmap(sourceSheet, new System.Windows.Int32Rect(0, frameIndex * frameSize, frameSize, frameSize));
            frame.Freeze();
            Source = frame;
        }
    }
}

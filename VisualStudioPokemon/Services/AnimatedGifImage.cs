using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DrawingImage = System.Drawing.Image;
using WpfImage = System.Windows.Controls.Image;

namespace VisualStudioPokemon.Services
{
    internal sealed class AnimatedGifImage : WpfImage
    {
        private readonly DispatcherTimer timer;
        private readonly List<BitmapSource> frames = [];
        private readonly List<int> frameDelays = [];
        private string currentPath = string.Empty;
        private int frameIndex;

        public AnimatedGifImage()
        {
            timer = new DispatcherTimer(DispatcherPriority.Render);
            timer.Tick += Timer_Tick;
        }

        public void SetImageFile(string path)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(currentPath, path))
            {
                return;
            }

            currentPath = path;
            timer.Stop();
            frames.Clear();
            frameDelays.Clear();
            frameIndex = 0;

            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Source = null;
                return;
            }

            string extension = Path.GetExtension(path);
            if (StringComparer.OrdinalIgnoreCase.Equals(extension, ".gif"))
            {
                LoadGif(path);
            }
            else
            {
                Source = LoadBitmap(path);
                return;
            }

            if (frames.Count == 0)
            {
                Source = null;
                return;
            }

            Source = frames[0];
            if (frames.Count > 1)
            {
                timer.Interval = TimeSpan.FromMilliseconds(frameDelays[0]);
                timer.Start();
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (frames.Count <= 1)
            {
                timer.Stop();
                return;
            }

            frameIndex = (frameIndex + 1) % frames.Count;
            Source = frames[frameIndex];
            timer.Interval = TimeSpan.FromMilliseconds(frameDelays[Math.Min(frameIndex, frameDelays.Count - 1)]);
        }

        private void LoadGif(string path)
        {
            using DrawingImage gif = DrawingImage.FromFile(path);
            var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
            int frameCount = gif.GetFrameCount(dimension);
            int[] delays = ReadFrameDelays(gif, frameCount);

            for (int i = 0; i < frameCount; i++)
            {
                gif.SelectActiveFrame(dimension, i);
                using var frame = new Bitmap(gif.Width, gif.Height, PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(frame))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.DrawImage(gif, 0, 0, gif.Width, gif.Height);
                }

                frames.Add(BitmapToBitmapSource(frame));
                frameDelays.Add(delays[i]);
            }
        }

        private static int[] ReadFrameDelays(DrawingImage gif, int frameCount)
        {
            var result = new int[frameCount];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = 125;
            }

            try
            {
                PropertyItem delayProperty = gif.GetPropertyItem(0x5100);
                byte[] values = delayProperty.Value;
                for (int i = 0; i < frameCount; i++)
                {
                    int offset = i * 4;
                    if (offset + 3 >= values.Length)
                    {
                        break;
                    }

                    int hundredths = BitConverter.ToInt32(values, offset);
                    int milliseconds = Math.Max(40, hundredths * 10);
                    result[i] = milliseconds;
                }
            }
            catch
            {
                // Keep the default 8fps-ish delay when GIF metadata is missing.
            }

            return result;
        }

        private static BitmapSource LoadBitmap(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource BitmapToBitmapSource(Bitmap bitmap)
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}

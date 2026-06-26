using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VisualStudioPokemon.Models;
using VisualStudioPokemon.Services;

namespace VisualStudioPokemon.UI
{
    public partial class PokemonControl : UserControl
    {
        private readonly Random random = new Random();
        private readonly DispatcherTimer animationTimer;
        private readonly PokemonSessionStore store = new PokemonSessionStore();
        private readonly List<Companion> companions = new List<Companion>();
        private bool restored;

        public PokemonControl()
        {
            InitializeComponent();

            SpeciesCombo.ItemsSource = PokemonCatalog.All;
            SpeciesCombo.SelectedIndex = PokemonCatalog.All.Count > 0 ? 0 : -1;
            SizeCombo.ItemsSource = Enum.GetValues(typeof(PokemonSize));
            SizeCombo.SelectedItem = PokemonSize.Medium;

            animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();

            Loaded += PokemonControl_Loaded;
        }

        public void EnsureStarted()
        {
            EnsureRestored();
            if (companions.Count == 0)
            {
                PokemonSpecies first = PokemonCatalog.All.FirstOrDefault() ?? PokemonCatalog.Find("bulbasaur");
                SpawnPokemon(new PokemonSpec(first.Key, first.DisplayName, PokemonCatalog.GetRandomName(), PokemonSize.Medium, false));
            }
        }

        public void SpawnSelectedFromToolbar()
        {
            EnsureRestored();
            var species = SpeciesCombo.SelectedItem as PokemonSpecies ?? PokemonCatalog.All[0];
            string nickname = String.IsNullOrWhiteSpace(NameBox.Text) ? PokemonCatalog.GetRandomName() : NameBox.Text.Trim();
            var size = SizeCombo.SelectedItem is PokemonSize ? (PokemonSize)SizeCombo.SelectedItem : PokemonSize.Medium;
            SpawnPokemon(new PokemonSpec(species.Key, species.DisplayName, nickname, size, ShinyCheck.IsChecked == true));
            NameBox.Text = String.Empty;
        }

        public void SpawnRandom()
        {
            EnsureRestored();
            PokemonSpecies species = PokemonCatalog.GetRandomSpecies();
            var size = SizeCombo.SelectedItem is PokemonSize ? (PokemonSize)SizeCombo.SelectedItem : PokemonSize.Medium;
            bool shiny = ShinyCheck.IsChecked == true || random.Next(8192) == 0;
            SpawnPokemon(new PokemonSpec(species.Key, species.DisplayName, PokemonCatalog.GetRandomName(), size, shiny));
        }

        public void RemoveAll()
        {
            EnsureRestored();
            companions.Clear();
            Playground.Children.Clear();
            store.Save(Enumerable.Empty<PokemonSpec>());
            UpdateStatus();
        }

        public void RollCall()
        {
            EnsureRestored();
            if (companions.Count == 0)
            {
                MessageBox.Show("No Pokemon are currently in the playground.", "Pokemon Roll-call", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (Companion companion in companions)
            {
                ShowSpeechBubble(companion, friend: true);
            }

            string message = String.Join(Environment.NewLine, companions.Select(c => c.Spec.Nickname + " - " + c.Spec.DisplayName + (c.Spec.Shiny ? " (shiny)" : String.Empty)));
            MessageBox.Show(message, "Pokemon Roll-call", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PokemonControl_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureRestored();
            UpdateStatus();
        }

        private void SpawnButton_Click(object sender, RoutedEventArgs e)
        {
            SpawnSelectedFromToolbar();
        }

        private void RandomButton_Click(object sender, RoutedEventArgs e)
        {
            SpawnRandom();
        }

        private void RemoveAllButton_Click(object sender, RoutedEventArgs e)
        {
            RemoveAll();
        }

        private void RollCallButton_Click(object sender, RoutedEventArgs e)
        {
            RollCall();
        }

        private void Playground_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            foreach (Companion companion in companions)
            {
                ClampToPlayground(companion);
                Position(companion, 0);
            }
        }

        private void EnsureRestored()
        {
            if (restored)
            {
                return;
            }

            restored = true;
            foreach (PokemonSpec spec in store.Load())
            {
                PokemonSpecies existing = PokemonCatalog.Find(spec.Species);
                SpawnPokemon(new PokemonSpec(existing.Key, existing.DisplayName, spec.Nickname, spec.Size, spec.Shiny), save: false);
            }
        }

        private void SpawnPokemon(PokemonSpec spec, bool save = true)
        {
            FrameworkElement sprite = PokemonSpriteFactory.Create(spec);
            double width = Math.Max(sprite.Width, PokemonSizing.ToPixels(spec.Size));
            double height = Math.Max(sprite.Height, PokemonSizing.ToPixels(spec.Size));

            var root = new Grid
            {
                Width = width,
                Height = height + 20,
                ClipToBounds = false
            };

            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

            Grid.SetRow(sprite, 0);
            root.Children.Add(sprite);

            Image speechBubble = CreateSpeechBubble();
            Grid.SetRow(speechBubble, 0);
            root.Children.Add(speechBubble);

            var label = new TextBlock
            {
                Text = spec.Nickname,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 3,
                    ShadowDepth = 1,
                    Opacity = 0.7
                }
            };
            Grid.SetRow(label, 1);
            root.Children.Add(label);

            var companion = new Companion(spec, root, sprite as IPokemonSpriteView, speechBubble)
            {
                X = random.NextDouble() * Math.Max(1, Playground.ActualWidth - root.Width),
                BaseY = Math.Max(0, Playground.ActualHeight - root.Height - 10 - random.Next(0, 35)),
                Speed = random.NextDouble() * 1.2 + 0.45,
                Phase = random.NextDouble() * Math.PI * 2,
                HoldFrames = random.Next(35, 95)
            };

            root.MouseLeftButtonDown += delegate
            {
                Swipe(companion);
            };

            Playground.Children.Add(root);
            companions.Add(companion);
            SetState(companion, PokemonAnimationState.Idle);
            ClampToPlayground(companion);
            Position(companion, 0);

            if (save)
            {
                store.Save(companions.Select(c => c.Spec));
            }

            UpdateStatus();
        }

        private Image CreateSpeechBubble()
        {
            return new Image
            {
                Width = 26,
                Height = 26,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -4, -2, 0),
                Visibility = Visibility.Hidden,
                IsHitTestVisible = false,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 3,
                    ShadowDepth = 1,
                    Opacity = 0.55
                }
            };
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (companions.Count == 0 || Playground.ActualWidth <= 1 || Playground.ActualHeight <= 1)
            {
                return;
            }

            foreach (Companion companion in companions)
            {
                companion.FramesInState++;

                switch (companion.State)
                {
                    case PokemonAnimationState.Swipe:
                        if (companion.FramesInState > 15)
                        {
                            SetState(companion, PokemonAnimationState.Idle);
                        }
                        break;

                    case PokemonAnimationState.Idle:
                        if (companion.FramesInState > companion.HoldFrames)
                        {
                            SetState(companion, random.Next(2) == 0 ? PokemonAnimationState.WalkLeft : PokemonAnimationState.WalkRight);
                        }
                        break;

                    case PokemonAnimationState.WalkLeft:
                        companion.X -= companion.Speed;
                        if (companion.X <= 0 || companion.FramesInState > 60 && random.NextDouble() < 0.01)
                        {
                            companion.X = Math.Max(0, companion.X);
                            SetState(companion, PokemonAnimationState.Idle);
                        }
                        break;

                    case PokemonAnimationState.WalkRight:
                        companion.X += companion.Speed;
                        double maxX = Math.Max(0, Playground.ActualWidth - companion.Visual.Width);
                        if (companion.X >= maxX || companion.FramesInState > 60 && random.NextDouble() < 0.01)
                        {
                            companion.X = Math.Min(maxX, companion.X);
                            SetState(companion, PokemonAnimationState.Idle);
                        }
                        break;
                }

                double bob = companion.State == PokemonAnimationState.Idle
                    ? 0
                    : Math.Sin(companion.FramesInState * 0.28 + companion.Phase) * 2.2;

                Position(companion, bob);
            }
        }

        private void Swipe(Companion companion)
        {
            SetState(companion, PokemonAnimationState.Swipe);
            ShowSpeechBubble(companion, friend: false);
        }

        private void SetState(Companion companion, PokemonAnimationState state)
        {
            companion.State = state;
            companion.FramesInState = 0;
            companion.HoldFrames = state == PokemonAnimationState.Idle ? random.Next(35, 95) : companion.HoldFrames;
            if (companion.Sprite != null)
            {
                companion.Sprite.SetAnimation(state);
            }
        }

        private void ShowSpeechBubble(Companion companion, bool friend)
        {
            string path = PokemonResourceLocator.GetResourcePath(friend ? "heart.png" : "happy.png");
            if (System.IO.File.Exists(path))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                companion.SpeechBubble.Source = bitmap;
            }

            companion.SpeechBubble.Visibility = Visibility.Visible;
            companion.SpeechTimer.Stop();
            companion.SpeechTimer.Interval = TimeSpan.FromMilliseconds(1800);
            companion.SpeechTimer.Start();
        }

        private void Position(Companion companion, double bob)
        {
            Canvas.SetLeft(companion.Visual, companion.X);
            Canvas.SetTop(companion.Visual, companion.BaseY + bob);
        }

        private void ClampToPlayground(Companion companion)
        {
            double maxX = Math.Max(0, Playground.ActualWidth - companion.Visual.Width);
            double maxY = Math.Max(0, Playground.ActualHeight - companion.Visual.Height - 8);
            companion.X = Math.Max(0, Math.Min(maxX, companion.X));
            companion.BaseY = Math.Max(0, Math.Min(maxY, companion.BaseY));
        }

        private void UpdateStatus()
        {
            StatusText.Text = companions.Count == 1 ? "1 companion" : companions.Count + " companions";
        }

        private sealed class Companion
        {
            public Companion(PokemonSpec spec, FrameworkElement visual, IPokemonSpriteView sprite, Image speechBubble)
            {
                Spec = spec;
                Visual = visual;
                Sprite = sprite;
                SpeechBubble = speechBubble;
                SpeechTimer = new DispatcherTimer();
                SpeechTimer.Tick += delegate
                {
                    SpeechTimer.Stop();
                    SpeechBubble.Visibility = Visibility.Hidden;
                };
            }

            public PokemonSpec Spec { get; private set; }
            public FrameworkElement Visual { get; private set; }
            public IPokemonSpriteView Sprite { get; private set; }
            public Image SpeechBubble { get; private set; }
            public DispatcherTimer SpeechTimer { get; private set; }
            public PokemonAnimationState State { get; set; }
            public int FramesInState { get; set; }
            public int HoldFrames { get; set; }
            public double X { get; set; }
            public double BaseY { get; set; }
            public double Speed { get; set; }
            public double Phase { get; set; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VisualStudioPokemon.Models;
using VisualStudioPokemon.Services;
using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;

namespace VisualStudioPokemon.UI
{
    public partial class PokemonControl : UserControl
    {
        private const double PlaygroundPadding = 12;
        private const double MinimumCompanionGap = 8;

        private readonly Random random = new();
        private readonly DispatcherTimer animationTimer;
        private readonly PokemonSessionStore store = new();
        private readonly ObservableCollection<Companion> companions = [];
        private readonly ICollectionView speciesView;
        private bool restored;
        private bool suppressSpeciesFilter;
        private bool suppressCompanionSelection;
        private Companion? selectedCompanion;
        private Delegate? vsThemeChangedHandler;

        public PokemonControl()
        {
            InitializeComponent();

            ApplyTheme();
            TryHookVsThemeChanged();

            speciesView = CollectionViewSource.GetDefaultView(PokemonCatalog.All);
            SpeciesCombo.ItemsSource = speciesView;
            SpeciesCombo.SelectedIndex = PokemonCatalog.All.Count > 0 ? 0 : -1;
            SpeciesCombo.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(SpeciesCombo_TextChanged));

            SizeCombo.ItemsSource = Enum.GetValues(typeof(PokemonSize));
            SizeCombo.SelectedItem = PokemonSize.Medium;

            SpawnedCombo.ItemsSource = companions;

            HookComboBoxChrome(SpeciesCombo, fixSelectedValueContrast: false);
            HookComboBoxChrome(SizeCombo, fixSelectedValueContrast: false);
            HookComboBoxChrome(SpawnedCombo, fixSelectedValueContrast: false);

            animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();

            Loaded += PokemonControl_Loaded;
            Unloaded += PokemonControl_Unloaded;
        }

        public void EnsureStarted()
        {
            EnsureRestored();
            if (companions.Count == 0)
            {
                PokemonSpecies first = PokemonCatalog.All.FirstOrDefault() ?? PokemonCatalog.Find("bulbasaur");
                SpawnPokemon(new PokemonSpec(first.Key, first.DisplayName, first.DisplayName, PokemonSize.Medium, false));
            }
        }

        public void SpawnSelectedFromToolbar()
        {
            EnsureRestored();
            PokemonSpecies species = ResolveSpeciesFromCombo();
            string nickname = String.IsNullOrWhiteSpace(NameBox.Text) ? species.DisplayName : NameBox.Text.Trim();
            var size = SizeCombo.SelectedItem is PokemonSize defaultSize ? defaultSize : PokemonSize.Medium;
            SpawnPokemon(new PokemonSpec(species.Key, species.DisplayName, nickname, size, ShinyCheck.IsChecked == true));
            NameBox.Text = String.Empty;
            ResetSpeciesFilter(species);
        }

        public void SpawnRandom()
        {
            EnsureRestored();
            PokemonSpecies species = PokemonCatalog.GetRandomSpecies();
            var size = SizeCombo.SelectedItem is PokemonSize defaultSize ? defaultSize : PokemonSize.Medium;
            bool shiny = ShinyCheck.IsChecked == true || random.Next(8192) == 0;
            SpawnPokemon(new PokemonSpec(species.Key, species.DisplayName, species.DisplayName, size, shiny));
            ResetSpeciesFilter(species);
        }

        public void RemoveAll()
        {
            EnsureRestored();

            foreach (Companion companion in companions.ToList())
            {
                companion.SpeechTimer.Stop();
                PlayPokeballAt(companion.X + companion.Visual.Width / 2, companion.BaseY + companion.Visual.Height / 2, null, 48);
            }

            selectedCompanion = null;
            companions.Clear();
            Playground.Children.Clear();
            SyncCompanionSelectors();
            store.Save([]);
            UpdateSelectionUi();
            UpdateStatus();
        }

        public void RemoveSelected()
        {
            EnsureRestored();
            if (selectedCompanion == null)
            {
                return;
            }

            RemoveCompanion(selectedCompanion);
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
            UpdateSelectionUi();
            UpdateStatus();
            Dispatcher.BeginInvoke(new Action(PlayWindowOpenAnimation), DispatcherPriority.Loaded);
        }

        private void PokemonControl_Unloaded(object sender, RoutedEventArgs e)
        {
            TryUnhookVsThemeChanged();
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

        private void RemoveSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            RemoveSelected();
        }

        private void RemoveCompanionItemButton_Click(object sender, RoutedEventArgs e)
        {
            Companion? companion = sender is FrameworkElement element ? element.Tag as Companion : null;
            if (companion == null)
            {
                return;
            }

            SelectCompanion(companion, fromList: false);
            RemoveCompanion(companion);
            e.Handled = true;
        }

        private void RollCallButton_Click(object sender, RoutedEventArgs e)
        {
            RollCall();
        }

        private void SpawnedCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressCompanionSelection)
            {
                return;
            }

            SelectCompanion((Companion)SpawnedCombo.SelectedItem, fromList: true);
        }

        private void SpeciesCombo_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SpawnSelectedFromToolbar();
                e.Handled = true;
            }
        }

        private void SpeciesCombo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressSpeciesFilter || !SpeciesCombo.IsKeyboardFocusWithin)
            {
                return;
            }

            ApplySpeciesFilter(SpeciesCombo.Text);
            SpeciesCombo.IsDropDownOpen = true;
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
                ClipToBounds = false,
                ToolTip = spec.Nickname + " - " + spec.DisplayName
            };

            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

            Grid.SetRow(sprite, 0);
            root.Children.Add(sprite);

            Image speechBubble = CreateSpeechBubble();
            Grid.SetRow(speechBubble, 0);
            Panel.SetZIndex(speechBubble, 30);
            root.Children.Add(speechBubble);

            var label = new TextBlock
            {
                Text = spec.Nickname,
                Foreground = GetBrushResource("Pokemon.TextBrush", Brushes.White),
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
            Panel.SetZIndex(label, 20);
            root.Children.Add(label);

            Border selectionRing = CreateSelectionRing();
            Grid.SetRowSpan(selectionRing, 2);
            Panel.SetZIndex(selectionRing, 10);
            root.Children.Add(selectionRing);

            var companion = new Companion(spec, root, (IPokemonSpriteView)sprite, speechBubble, selectionRing, label)
            {
                Lane = 0,
                Speed = random.NextDouble() * 0.8 + 0.5,
                Phase = random.NextDouble() * Math.PI * 2,
                HoldFrames = random.Next(45, 110)
            };

            companion.BaseY = GetRandomBaseY(companion);
            companion.X = FindOpenTarget(companion, MovementMinX(), MovementMaxX(companion), preferFarAway: false);
            companion.TargetX = companion.X;
            companion.TargetY = companion.BaseY;

            root.Cursor = Cursors.Hand;
            root.MouseLeftButtonDown += delegate (object clickSender, MouseButtonEventArgs args)
            {
                SelectCompanion(companion, fromList: false);
                Swipe(companion);
                args.Handled = true;
            };

            root.MouseRightButtonDown += delegate (object clickSender, MouseButtonEventArgs args)
            {
                SelectCompanion(companion, fromList: false);
                args.Handled = true;
            };

            var removeThisMenuItem = new MenuItem
            {
                Header = "Remove this Pokemon",
                Foreground = GetBrushResource("Pokemon.TextBrush", Brushes.White),
                Background = GetBrushResource("Pokemon.ControlBackgroundBrush", Brushes.DimGray)
            };

            removeThisMenuItem.Click += delegate
            {
                RemoveCompanion(companion);
            };

            var contextMenu = new ContextMenu
            {
                Background = GetBrushResource("Pokemon.ControlBackgroundBrush", Brushes.DimGray),
                Foreground = GetBrushResource("Pokemon.TextBrush", Brushes.White)
            };

            contextMenu.Items.Add(removeThisMenuItem);
            root.ContextMenu = contextMenu;

            root.Opacity = 0;
            Playground.Children.Add(root);
            companions.Add(companion);
            SetState(companion, PokemonAnimationState.Idle);
            ClampToPlayground(companion);
            Position(companion, 0);
            PlayPokeballAt(companion.X + width / 2, companion.BaseY + height / 2, delegate { root.Opacity = 1; }, 54);

            if (save)
            {
                SaveSession();
            }

            UpdateStatus();
        }

        private Border CreateSelectionRing()
        {
            Brush accentBrush = GetBrushResource("Pokemon.AccentBrush", Brushes.DeepSkyBlue);
            MediaColor accentColor = Colors.DodgerBlue;
            if (accentBrush is SolidColorBrush solidAccent)
            {
                accentColor = solidAccent.Color;
            }

            var selectedFill = new SolidColorBrush(MediaColor.FromArgb(38, accentColor.R, accentColor.G, accentColor.B));

            return new Border
            {
                BorderBrush = accentBrush,
                Background = selectedFill,
                BorderThickness = new Thickness(3),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(-6),
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = "SELECTED",
                    Background = accentBrush,
                    Foreground = EnsureReadableBrush(Brushes.White, accentBrush),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(5, 1, 5, 1),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -1, -1, 0)
                },
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 14,
                    ShadowDepth = 0,
                    Opacity = 0.85,
                    Color = accentColor
                }
            };
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

            foreach (Companion companion in companions.ToList())
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
                            BeginWalk(companion);
                        }
                        break;

                    case PokemonAnimationState.WalkLeft:
                    case PokemonAnimationState.WalkRight:
                        StepTowardTarget(companion);
                        break;
                }

                companion.CurrentBob = companion.State == PokemonAnimationState.Idle
                    ? 0
                    : Math.Sin(companion.FramesInState * 0.22 + companion.Phase) * 1.4;
            }

            foreach (Companion companion in companions)
            {
                Position(companion, companion.CurrentBob);
            }
        }

        private void BeginWalk(Companion companion)
        {
            double minX = MovementMinX();
            double maxX = MovementMaxX(companion);
            double minY = MovementMinY();
            double maxY = MovementMaxY(companion);
            if (maxX <= minX && maxY <= minY)
            {
                companion.X = minX;
                companion.BaseY = minY;
                SetState(companion, PokemonAnimationState.Idle);
                return;
            }

            double targetX = Math.Max(minX, Math.Min(maxX, FindOpenTarget(companion, minX, maxX, preferFarAway: true)));
            double targetY = minY + random.NextDouble() * Math.Max(1, maxY - minY);

            if (Math.Abs(targetX - companion.X) < 3 && Math.Abs(targetY - companion.BaseY) < 3)
            {
                companion.HoldFrames = random.Next(45, 120);
                companion.FramesInState = 0;
                return;
            }

            companion.TargetX = targetX;
            companion.TargetY = targetY;
            SetState(companion, targetX < companion.X ? PokemonAnimationState.WalkLeft : PokemonAnimationState.WalkRight);
        }

        private void StepTowardTarget(Companion companion)
        {
            double minX = MovementMinX();
            double maxX = MovementMaxX(companion);
            double minY = MovementMinY();
            double maxY = MovementMaxY(companion);
            companion.TargetX = Math.Max(minX, Math.Min(maxX, companion.TargetX));
            companion.TargetY = Math.Max(minY, Math.Min(maxY, companion.TargetY));

            double deltaX = companion.TargetX - companion.X;
            double deltaY = companion.TargetY - companion.BaseY;
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (distance <= companion.Speed)
            {
                companion.X = companion.TargetX;
                companion.BaseY = companion.TargetY;
                SetState(companion, PokemonAnimationState.Idle);
                return;
            }

            companion.X += deltaX / distance * companion.Speed;
            companion.BaseY += deltaY / distance * companion.Speed;
        }

        private double FindOpenTarget(Companion companion, double minX, double maxX, bool preferFarAway)
        {
            if (maxX <= minX)
            {
                return minX;
            }

            double fallback = minX + random.NextDouble() * (maxX - minX);
            double minimumMove = Math.Min(80, Math.Max(24, (maxX - minX) * 0.25));

            for (int attempt = 0; attempt < 16; attempt++)
            {
                double candidate = minX + random.NextDouble() * (maxX - minX);
                fallback = candidate;

                if (!preferFarAway || Math.Abs(candidate - companion.X) >= minimumMove)
                {
                    return candidate;
                }
            }

            return fallback;
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
            companion.HoldFrames = state == PokemonAnimationState.Idle ? random.Next(55, 140) : companion.HoldFrames;
            companion.Sprite?.SetAnimation(state);
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
            companion.X = Math.Max(MovementMinX(), Math.Min(MovementMaxX(companion), companion.X));
            companion.TargetX = Math.Max(MovementMinX(), Math.Min(MovementMaxX(companion), companion.TargetX));

            double maxY = Playground.ActualHeight <= 1
                ? PlaygroundPadding
                : Math.Max(PlaygroundPadding, Playground.ActualHeight - PlaygroundPadding - companion.Visual.Height);
            companion.BaseY = Math.Max(PlaygroundPadding, Math.Min(maxY, companion.BaseY));
        }

        private double GetRandomBaseY(Companion companion)
        {
            if (Playground.ActualHeight <= 1)
            {
                return PlaygroundPadding;
            }

            double maxY = Math.Max(PlaygroundPadding, Playground.ActualHeight - PlaygroundPadding - companion.Visual.Height);
            double minY = Math.Max(PlaygroundPadding, maxY - 36);
            return minY + random.NextDouble() * Math.Max(1, maxY - minY);
        }

        private double GetLaneHeight()
        {
            double tallest = companions.Count == 0 ? 98 : companions.Max(c => c.Visual.Height);
            return Math.Max(64, tallest + 18);
        }

        private double MovementMinX()
        {
            return PlaygroundPadding;
        }

        private double MovementMaxX(Companion companion)
        {
            if (Playground.ActualWidth <= 1)
            {
                return PlaygroundPadding;
            }

            return Math.Max(PlaygroundPadding, Playground.ActualWidth - PlaygroundPadding - companion.Visual.Width);
        }

        private double MovementMinY()
        {
            return PlaygroundPadding;
        }

        private double MovementMaxY(Companion companion)
        {
            if (Playground.ActualHeight <= 1)
            {
                return PlaygroundPadding;
            }

            return Math.Max(PlaygroundPadding, Playground.ActualHeight - PlaygroundPadding - companion.Visual.Height);
        }

        private void SelectCompanion(Companion? companion, bool fromList)
        {
            _ = fromList;

            selectedCompanion = companion;
            ApplySelectedCompanionVisuals();
            SyncCompanionSelectors();
            UpdateSelectionUi();
            UpdateStatus();
        }

        private void ApplySelectedCompanionVisuals()
        {
            foreach (Companion item in companions)
            {
                bool isSelected = ReferenceEquals(item, selectedCompanion);
                item.SelectionRing.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
                Panel.SetZIndex(item.Visual, isSelected ? 1000 : 0);

                // Keep the reaction icon above the SELECTED badge whenever it is visible.
                Panel.SetZIndex(item.SelectionRing, 10);
                Panel.SetZIndex(item.SpeechBubble, 30);
            }
        }

        private void SyncCompanionSelectors()
        {
            suppressCompanionSelection = true;
            SpawnedCombo.SelectedItem = selectedCompanion;
            suppressCompanionSelection = false;
        }

        private void RemoveCompanion(Companion companion)
        {
            companion.SpeechTimer.Stop();
            companion.Visual.Opacity = 0;
            PlayPokeballAt(companion.X + companion.Visual.Width / 2, companion.BaseY + companion.Visual.Height / 2, delegate { Playground.Children.Remove(companion.Visual); }, 48);
            companions.Remove(companion);

            if (ReferenceEquals(selectedCompanion, companion))
            {
                selectedCompanion = companions.FirstOrDefault();
            }

            ApplySelectedCompanionVisuals();
            SyncCompanionSelectors();

            foreach (Companion item in companions)
            {
                ClampToPlayground(item);
                Position(item, 0);
            }

            SaveSession();
            UpdateSelectionUi();
            UpdateStatus();
        }

        private void UpdateSelectionUi()
        {
            SpawnedCombo.IsEnabled = companions.Count > 0;
            RemoveSelectedButton.IsEnabled = selectedCompanion != null;
            SelectedDetailsText.Text = selectedCompanion == null
                ? "Click a Pokemon in the playground or choose it from the dropdown."
                : "Selected: " + selectedCompanion.DisplayLabel + ". Use Remove selected or right-click it.";
        }

        private void UpdateStatus()
        {
            string countText = companions.Count == 1 ? "1 companion" : companions.Count + " companions";
            if (selectedCompanion != null)
            {
                countText += " - " + selectedCompanion.Spec.Nickname + " selected";
            }

            StatusText.Text = countText;
        }

        private void SaveSession()
        {
            store.Save(companions.Select(c => c.Spec));
        }

        private PokemonSpecies ResolveSpeciesFromCombo()
        {
            string text = (SpeciesCombo.Text ?? String.Empty).Trim();
            if (!String.IsNullOrWhiteSpace(text))
            {
                PokemonSpecies exact = PokemonCatalog.All.FirstOrDefault(s =>
                    StringComparer.CurrentCultureIgnoreCase.Equals(s.DisplayName, text) ||
                    StringComparer.OrdinalIgnoreCase.Equals(s.Key, text));
                if (exact != null)
                {
                    return exact;
                }

                PokemonSpecies startsWith = PokemonCatalog.All.FirstOrDefault(s =>
                    s.DisplayName.StartsWith(text, StringComparison.CurrentCultureIgnoreCase) ||
                    s.Key.StartsWith(text.Replace(' ', '_'), StringComparison.OrdinalIgnoreCase));
                if (startsWith != null)
                {
                    return startsWith;
                }

                PokemonSpecies contains = PokemonCatalog.All.FirstOrDefault(s =>
                    s.DisplayName.IndexOf(text, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    s.Key.IndexOf(text.Replace(' ', '_'), StringComparison.OrdinalIgnoreCase) >= 0);
                if (contains != null)
                {
                    return contains;
                }
            }

            if (SpeciesCombo.SelectedItem is PokemonSpecies selected)
            {
                return selected;
            }

            return PokemonCatalog.All.FirstOrDefault() ?? PokemonCatalog.Find("bulbasaur");
        }

        private void ApplySpeciesFilter(string filter)
        {
            string normalized = (filter ?? String.Empty).Trim();
            speciesView.Filter = delegate (object item)
            {
                if (item is PokemonSpecies species)
                {
                    if (species == null || string.IsNullOrWhiteSpace(normalized))
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }

                string key = normalized.Replace(' ', '_');
                return species.DisplayName.IndexOf(normalized, StringComparison.CurrentCultureIgnoreCase) >= 0
                    || species.Key.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
            };

            speciesView.Refresh();
        }

        private void ResetSpeciesFilter(PokemonSpecies species)
        {
            suppressSpeciesFilter = true;
            speciesView.Filter = null;
            speciesView.Refresh();
            SpeciesCombo.SelectedItem = species;
            SpeciesCombo.Text = species.DisplayName;
            suppressSpeciesFilter = false;
        }

        private void ApplyTheme()
        {
            ThemePalette palette = ThemePalette.Create();
            Resources["Pokemon.WindowBackgroundBrush"] = CreateFrozenBrush(palette.WindowBackground);
            Resources["Pokemon.CommandBarBackgroundBrush"] = CreateFrozenBrush(palette.CommandBarBackground);
            Resources["Pokemon.PanelBackgroundBrush"] = CreateFrozenBrush(palette.PanelBackground);
            Resources["Pokemon.EditorBackgroundBrush"] = CreateFrozenBrush(palette.EditorBackground);
            Resources["Pokemon.BorderBrush"] = CreateFrozenBrush(palette.Border);
            Resources["Pokemon.TextBrush"] = CreateFrozenBrush(palette.Text);
            Resources["Pokemon.MutedTextBrush"] = CreateFrozenBrush(palette.MutedText);
            Resources["Pokemon.ControlBackgroundBrush"] = CreateFrozenBrush(palette.ControlBackground);
            Resources["Pokemon.ControlBorderBrush"] = CreateFrozenBrush(palette.ControlBorder);
            Resources["Pokemon.ButtonBackgroundBrush"] = CreateFrozenBrush(palette.ButtonBackground);
            Resources["Pokemon.AccentBrush"] = CreateFrozenBrush(palette.Accent);

            // These two used to be static hardcoded dark values in XAML and never followed the
            // theme. Now they're refreshed here just like every other brush.
            Resources["Pokemon.ComboBoxEditableBackgroundBrush"] = CreateFrozenBrush(palette.ControlBackground);
            Resources["Pokemon.ComboBoxEditableTextBrush"] = CreateFrozenBrush(palette.Text);

            // Text drawn on top of the accent color (selected combo item, SELECTED badge) needs to
            // flip between black/white depending on how light or dark the accent itself is.
            Brush accentContrastBrush = EnsureReadableBrush(Brushes.White, CreateFrozenBrush(palette.Accent));
            Resources["Pokemon.AccentContrastBrush"] = accentContrastBrush;

            Resources[SystemColors.WindowBrushKey] = CreateFrozenBrush(palette.ControlBackground);
            Resources[SystemColors.WindowTextBrushKey] = CreateFrozenBrush(palette.Text);
            Resources[SystemColors.ControlBrushKey] = CreateFrozenBrush(palette.ControlBackground);
            Resources[SystemColors.ControlTextBrushKey] = CreateFrozenBrush(palette.Text);
            Resources[SystemColors.InfoBrushKey] = CreateFrozenBrush(palette.ControlBackground);
            Resources[SystemColors.InfoTextBrushKey] = CreateFrozenBrush(palette.Text);
            Resources[SystemColors.HighlightBrushKey] = CreateFrozenBrush(palette.Accent);
            Resources[SystemColors.HighlightTextBrushKey] = accentContrastBrush;
            Resources[SystemColors.GrayTextBrushKey] = CreateFrozenBrush(palette.MutedText);

            foreach (Companion companion in companions)
            {
                Brush accentBrush = GetBrushResource("Pokemon.AccentBrush", Brushes.DeepSkyBlue);
                companion.SelectionRing.BorderBrush = accentBrush;
                companion.NicknameLabel.Foreground = GetBrushResource("Pokemon.TextBrush", Brushes.White);

                if (companion.SelectionRing.Child is TextBlock badge)
                {
                    badge.Background = accentBrush;
                    badge.Foreground = EnsureReadableBrush(Brushes.White, accentBrush);
                }
            }

            Dispatcher.BeginInvoke(new Action(RefreshComboBoxVisuals), DispatcherPriority.Loaded);
        }

        private void HookComboBoxChrome(ComboBox comboBox, bool fixSelectedValueContrast)
        {
            comboBox.Loaded += delegate { RefreshComboBoxVisual(comboBox, fixSelectedValueContrast); };
            comboBox.DropDownOpened += delegate { RefreshComboBoxVisual(comboBox, fixSelectedValueContrast); };
            comboBox.DropDownClosed += delegate { RefreshComboBoxVisual(comboBox, fixSelectedValueContrast); };
            comboBox.SelectionChanged += delegate { RefreshComboBoxVisual(comboBox, fixSelectedValueContrast); };
        }

        private void RefreshComboBoxVisuals()
        {
            UpdateComboBoxVisuals(SpeciesCombo);
            UpdateSelectedValueComboBoxVisuals(SizeCombo);
            UpdateSelectedValueComboBoxVisuals(SpawnedCombo);
        }

        private void RefreshComboBoxVisual(ComboBox comboBox, bool fixSelectedValueContrast)
        {
            if (fixSelectedValueContrast)
            {
                UpdateSelectedValueComboBoxVisuals(comboBox);
            }
            else
            {
                UpdateComboBoxVisuals(comboBox);
            }
        }

        private void UpdateComboBoxVisuals(ComboBox comboBox)
        {
            if (comboBox == null)
            {
                return;
            }

            Brush textBrush = GetBrushResource("Pokemon.TextBrush", Brushes.White);
            Brush backgroundBrush = GetBrushResource("Pokemon.ControlBackgroundBrush", Brushes.DimGray);
            Brush borderBrush = GetBrushResource("Pokemon.ControlBorderBrush", Brushes.Gray);
            Brush accentBrush = GetBrushResource("Pokemon.AccentBrush", Brushes.DeepSkyBlue);
            Brush accentContrastBrush = GetBrushResource("Pokemon.AccentContrastBrush", Brushes.White);

            comboBox.Foreground = textBrush;
            comboBox.Background = backgroundBrush;
            comboBox.BorderBrush = borderBrush;
            comboBox.Resources[SystemColors.WindowBrushKey] = backgroundBrush;
            comboBox.Resources[SystemColors.WindowTextBrushKey] = textBrush;
            comboBox.Resources[SystemColors.ControlBrushKey] = backgroundBrush;
            comboBox.Resources[SystemColors.ControlTextBrushKey] = textBrush;
            comboBox.Resources[SystemColors.HighlightBrushKey] = accentBrush;
            comboBox.Resources[SystemColors.HighlightTextBrushKey] = accentContrastBrush;


            if (comboBox.Template.FindName("PART_EditableTextBox", comboBox) is TextBox editableTextBox)
            {
                Brush comboEditableTextBrush =
                    GetBrushResource("Pokemon.ComboBoxEditableTextBrush", Brushes.Black);

                Brush comboEditableBackgroundBrush =
                    GetBrushResource("Pokemon.ComboBoxEditableBackgroundBrush", Brushes.White);

                editableTextBox.Foreground = comboEditableTextBrush;
                editableTextBox.Background = comboEditableBackgroundBrush;
                editableTextBox.BorderBrush = comboEditableBackgroundBrush;
                editableTextBox.CaretBrush = comboEditableTextBrush;
                editableTextBox.SelectionBrush = accentBrush;
            }

            Border? popupBorder = comboBox.Template.FindName("PART_Popup", comboBox) is Popup popup ?
                popup.Child as Border : null;

            if (popupBorder != null)
            {
                popupBorder.Background = backgroundBrush;
                popupBorder.BorderBrush = borderBrush;
            }
        }

        private void UpdateSelectedValueComboBoxVisuals(ComboBox comboBox)
        {
            if (comboBox == null)
            {
                return;
            }

            comboBox.ApplyTemplate();

            Brush backgroundBrush = GetBrushResource("Pokemon.ControlBackgroundBrush", Brushes.DimGray);
            Brush borderBrush = GetBrushResource("Pokemon.ControlBorderBrush", Brushes.Gray);
            Brush accentBrush = GetBrushResource("Pokemon.AccentBrush", Brushes.DeepSkyBlue);
            Brush accentContrastBrush = GetBrushResource("Pokemon.AccentContrastBrush", Brushes.White);
            Brush textBrush = EnsureReadableBrush(GetBrushResource("Pokemon.TextBrush", Brushes.White), backgroundBrush);

            comboBox.Foreground = textBrush;
            comboBox.SetValue(TextElement.ForegroundProperty, textBrush);
            comboBox.Background = backgroundBrush;
            comboBox.BorderBrush = borderBrush;

            // These keys affect the closed ComboBox selection box. This fixes white text on a white
            // selected value without changing the Species editable ComboBox behavior.
            comboBox.Resources[SystemColors.WindowBrushKey] = backgroundBrush;
            comboBox.Resources[SystemColors.WindowTextBrushKey] = textBrush;
            comboBox.Resources[SystemColors.ControlBrushKey] = backgroundBrush;
            comboBox.Resources[SystemColors.ControlTextBrushKey] = textBrush;
            comboBox.Resources[SystemColors.HighlightBrushKey] = accentBrush;
            comboBox.Resources[SystemColors.HighlightTextBrushKey] = accentContrastBrush;
            comboBox.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = backgroundBrush;
            comboBox.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = textBrush;

            Border? popupBorder = comboBox.Template.FindName("PART_Popup", comboBox) is Popup popup ?
                popup.Child as Border : null;
            if (popupBorder != null)
            {
                popupBorder.Background = backgroundBrush;
                popupBorder.BorderBrush = borderBrush;
            }
        }

        private static Brush EnsureReadableBrush(Brush preferredTextBrush, Brush backgroundBrush)
        {
            if (preferredTextBrush is not SolidColorBrush text || backgroundBrush is not SolidColorBrush background)
            {
                return preferredTextBrush;
            }

            double contrast = Math.Abs(GetReadableLuminance(text.Color) - GetReadableLuminance(background.Color));
            if (contrast >= 0.32)
            {
                return preferredTextBrush;
            }

            return GetReadableLuminance(background.Color) > 0.55 ? Brushes.Black : Brushes.White;
        }

        private static double GetReadableLuminance(MediaColor color)
        {
            return (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        }

        private static SolidColorBrush CreateFrozenBrush(MediaColor color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private Brush GetBrushResource(string key, Brush fallback)
        {
            return TryFindResource(key) as Brush ?? fallback;
        }

        private void TryHookVsThemeChanged()
        {
            if (vsThemeChangedHandler != null)
            {
                return;
            }

            try
            {
                Type vsColorTheme = ThemePalette.FindType("Microsoft.VisualStudio.PlatformUI.VSColorTheme");
                if (vsColorTheme == null)
                {
                    return;
                }

                EventInfo themeChangedEvent = vsColorTheme.GetEvent("ThemeChanged", BindingFlags.Static | BindingFlags.Public);
                MethodInfo handlerMethod = GetType().GetMethod("OnVsThemeChanged", BindingFlags.Instance | BindingFlags.NonPublic);
                if (themeChangedEvent == null || handlerMethod == null)
                {
                    return;
                }

                vsThemeChangedHandler = Delegate.CreateDelegate(themeChangedEvent.EventHandlerType, this, handlerMethod);
                themeChangedEvent.AddEventHandler(null, vsThemeChangedHandler);
            }
            catch
            {
                vsThemeChangedHandler = null;
            }
        }

        private void TryUnhookVsThemeChanged()
        {
            if (vsThemeChangedHandler == null)
            {
                return;
            }

            try
            {
                Type vsColorTheme = ThemePalette.FindType("Microsoft.VisualStudio.PlatformUI.VSColorTheme");
                EventInfo? themeChangedEvent = vsColorTheme?.GetEvent("ThemeChanged", BindingFlags.Static | BindingFlags.Public);
                themeChangedEvent?.RemoveEventHandler(null, vsThemeChangedHandler);
            }
            catch
            {
                // Best effort only. The extension still works with the current palette.
            }

            vsThemeChangedHandler = null;
        }

        private void OnVsThemeChanged(object sender, EventArgs e)
        {
            if (Dispatcher.CheckAccess())
            {
                ApplyTheme();
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(ApplyTheme));
            }
        }

        public sealed class Companion
        {
            internal Companion(PokemonSpec spec, FrameworkElement visual, IPokemonSpriteView sprite, Image speechBubble, Border selectionRing, TextBlock nicknameLabel)
            {
                Spec = spec;
                Visual = visual;
                Sprite = sprite;
                SpeechBubble = speechBubble;
                SelectionRing = selectionRing;
                NicknameLabel = nicknameLabel;
                SpeechTimer = new DispatcherTimer();
                SpeechTimer.Tick += delegate
                {
                    SpeechTimer.Stop();
                    SpeechBubble.Visibility = Visibility.Hidden;
                };
            }

            public PokemonSpec Spec { get; private set; }
            internal FrameworkElement Visual { get; private set; }
            internal IPokemonSpriteView Sprite { get; private set; }
            internal Image SpeechBubble { get; private set; }
            internal Border SelectionRing { get; private set; }
            internal TextBlock NicknameLabel { get; private set; }
            internal DispatcherTimer SpeechTimer { get; private set; }
            internal PokemonAnimationState State { get; set; }
            internal int FramesInState { get; set; }
            internal int HoldFrames { get; set; }
            internal int Lane { get; set; }
            internal double X { get; set; }
            internal double TargetX { get; set; }
            internal double TargetY { get; set; }
            internal double BaseY { get; set; }
            internal double Speed { get; set; }
            internal double Phase { get; set; }
            internal double CurrentBob { get; set; }

            public string DisplayLabel
            {
                get
                {
                    return Spec.Nickname + (Spec.Shiny ? " *" : String.Empty);
                }
            }

            public string DetailsLabel
            {
                get
                {
                    return Spec.DisplayName + " - " + Spec.Size;
                }
            }
        }

        private sealed class ThemePalette
        {
            public MediaColor WindowBackground { get; private set; }
            public MediaColor CommandBarBackground { get; private set; }
            public MediaColor PanelBackground { get; private set; }
            public MediaColor EditorBackground { get; private set; }
            public MediaColor Border { get; private set; }
            public MediaColor Text { get; private set; }
            public MediaColor MutedText { get; private set; }
            public MediaColor ControlBackground { get; private set; }
            public MediaColor ControlBorder { get; private set; }
            public MediaColor ButtonBackground { get; private set; }
            public MediaColor Accent { get; private set; }

            public static ThemePalette Create()
            {
                MediaColor toolWindowBackground = GetVsColor("ToolWindowBackgroundColorKey", FromRgb(0x25, 0x25, 0x26));
                bool dark = IsDark(toolWindowBackground);

                return new ThemePalette
                {
                    WindowBackground = toolWindowBackground,
                    CommandBarBackground = GetVsColor("CommandBarGradientBeginColorKey", dark ? FromRgb(0x2D, 0x2D, 0x30) : FromRgb(0xF3, 0xF3, 0xF3)),
                    PanelBackground = GetVsColor("ToolWindowBackgroundColorKey", dark ? FromRgb(0x25, 0x25, 0x26) : FromRgb(0xF5, 0xF5, 0xF5)),
                    EditorBackground = GetVsColor("EditorExpansionFillColorKey", dark ? FromRgb(0x1E, 0x1E, 0x1E) : FromRgb(0xFF, 0xFF, 0xFF)),
                    Border = GetVsColor("PanelBorderColorKey", dark ? FromRgb(0x3F, 0x3F, 0x46) : FromRgb(0xCC, 0xCC, 0xCC)),
                    Text = GetVsColor("ToolWindowTextColorKey", dark ? FromRgb(0xF1, 0xF1, 0xF1) : FromRgb(0x1E, 0x1E, 0x1E)),
                    MutedText = dark ? FromRgb(0xC8, 0xC8, 0xC8) : FromRgb(0x5F, 0x5F, 0x5F),
                    ControlBackground = GetVsColor("ComboBoxBackgroundColorKey", dark ? FromRgb(0x33, 0x33, 0x37) : FromRgb(0xFF, 0xFF, 0xFF)),
                    ControlBorder = GetVsColor("ComboBoxBorderColorKey", dark ? FromRgb(0x55, 0x55, 0x5A) : FromRgb(0xA6, 0xA6, 0xA6)),
                    ButtonBackground = GetVsColor("ButtonFaceColorKey", dark ? FromRgb(0x3A, 0x3D, 0x41) : FromRgb(0xE1, 0xE1, 0xE1)),
                    Accent = GetVsColor("AccentMediumColorKey", FromRgb(0x00, 0x7A, 0xCC))
                };
            }

            public static Type FindType(string fullName)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type type = assembly.GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }

                string[] assemblyNames =
                [
                    "Microsoft.VisualStudio.Shell.15.0",
                    "Microsoft.VisualStudio.Shell.Framework",
                    "Microsoft.VisualStudio.PlatformUI"
                ];

                foreach (string assemblyName in assemblyNames)
                {
                    Type type = Type.GetType(fullName + ", " + assemblyName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }

                return Type.GetType(fullName, false);
            }

            private static MediaColor GetVsColor(string resourceKeyPropertyName, MediaColor fallback)
            {
                try
                {
                    Type environmentColors = FindType("Microsoft.VisualStudio.PlatformUI.EnvironmentColors");
                    Type vsColorTheme = FindType("Microsoft.VisualStudio.PlatformUI.VSColorTheme");
                    if (environmentColors == null || vsColorTheme == null)
                    {
                        return fallback;
                    }

                    PropertyInfo property = environmentColors.GetProperty(resourceKeyPropertyName, BindingFlags.Static | BindingFlags.Public);
                    if (property == null)
                    {
                        return fallback;
                    }

                    object key = property.GetValue(null, null);
                    if (key == null)
                    {
                        return fallback;
                    }

                    MethodInfo method = vsColorTheme.GetMethods(BindingFlags.Static | BindingFlags.Public)
                        .FirstOrDefault(m => m.Name == "GetThemedColor"
                            && m.GetParameters().Length == 1
                            && m.GetParameters()[0].ParameterType.IsInstanceOfType(key));
                    if (method == null)
                    {
                        return fallback;
                    }

                    object value = method.Invoke(null, [key]);
                    if (value is DrawingColor drawingColor)
                    {
                        return MediaColor.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
                    }

                    if (value is MediaColor color)
                    {
                        return color;
                    }
                }
                catch
                {
                    // Fall back to the built-in Visual Studio-like dark/light palette.
                }

                return fallback;
            }

            private static MediaColor FromRgb(byte red, byte green, byte blue)
            {
                return MediaColor.FromRgb(red, green, blue);
            }

            private static bool IsDark(MediaColor color)
            {
                double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
                return luminance < 0.5;
            }
        }


        private void PlayWindowOpenAnimation()
        {
            if (Playground.ActualWidth <= 1 || Playground.ActualHeight <= 1)
            {
                return;
            }

            PlayPokeballAt(Playground.ActualWidth / 2, Playground.ActualHeight / 2, null, 64);
        }

        private void PlayPokeballAt(double centerX, double centerY, Action? completed, double size)
        {
            string path = PokemonResourceLocator.GetResourcePath("pokeball_sprite_sheet.png");
            var animation = new PokeballAnimationImage
            {
                Width = size,
                Height = size,
                Opacity = 0.95
            };

            Canvas.SetLeft(animation, Math.Max(0, centerX - size / 2));
            Canvas.SetTop(animation, Math.Max(0, centerY - size / 2));
            Panel.SetZIndex(animation, 5000);
            PokeballLayer.Children.Add(animation);
            animation.Completed += delegate
            {
                PokeballLayer.Children.Remove(animation);
                completed?.Invoke();
            };
            animation.Play(path);
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectCompanion(null, fromList: false);
        }
    }
}
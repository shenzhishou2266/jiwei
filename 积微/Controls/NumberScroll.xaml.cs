using System;
using System.Collections.Generic;
using System.ComponentModel;
using SW = System.Windows;
using SWC = System.Windows.Controls;
using SWM = System.Windows.Media;
using SWMA = System.Windows.Media.Animation;

namespace 积微.Controls
{
    /// <summary>滚动方向枚举。</summary>
    public enum ScrollDirection
    {
        /// <summary>自动根据数值变化方向决定。</summary>
        Auto,
        /// <summary>强制向上滚动。</summary>
        Up,
        /// <summary>强制向下滚动。</summary>
        Down
    }

    /// <summary>数字滚动选择控件，支持上下滚动切换数值。</summary>
    public partial class NumberScroll : SWC.UserControl, INotifyPropertyChanged
    {
        private int _currentValue = 0;
        private int _maxValue = 9;
        private bool _isEditable = true;
        private double _fontSize = 48;
        private double _digitHeight = 70;
        private double _digitWidth = 60;
        private int _animationVersion = 0;
        private List<SWC.TextBlock> _normalDigits = new List<SWC.TextBlock>();
        private SWC.TextBlock _upperExtraDigit = null;
        private SWC.TextBlock _lowerExtraDigit = null;

        /// <summary>数值变化事件。</summary>
        public event EventHandler<int> ValueChanged;
        /// <summary>属性变化事件。</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>获取或设置当前数值，变化时播放滚动动画。</summary>
        public int CurrentValue
        {
            get => _currentValue;
            set
            {
                int newValue = Math.Clamp(value, 0, _maxValue);
                if (newValue != _currentValue)
                {
                    _animationVersion++;
                    int oldValue = _currentValue;
                    _currentValue = newValue;
                    AnimateToValue(oldValue, _currentValue);
                    ValueChanged?.Invoke(this, _currentValue);
                }
            }
        }

        /// <summary>获取或设置允许的最大值（0-9）。</summary>
        public int MaxValue
        {
            get => _maxValue;
            set
            {
                int newMax = Math.Clamp(value, 0, 9);
                if (newMax != _maxValue)
                {
                    _maxValue = newMax;
                    if (_currentValue > _maxValue)
                    {
                        SetValueWithoutAnimation(_maxValue);
                        ValueChanged?.Invoke(this, _currentValue);
                    }
                    GenerateDigits();
                    SetPosition(_currentValue);
                }
            }
        }

        /// <summary>获取或设置是否允许编辑（上下按钮切换）。</summary>
        public bool IsEditable
        {
            get => _isEditable;
            set
            {
                if (_isEditable != value)
                {
                    _isEditable = value;
                    OnPropertyChanged(nameof(IsEditable));
                }
            }
        }

        /// <summary>获取或设置数字字体大小。</summary>
        public double FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value)
                {
                    _fontSize = value;
                    OnPropertyChanged(nameof(FontSize));
                    GenerateDigits();
                    SetPosition(_currentValue);
                }
            }
        }

        /// <summary>获取或设置每个数字显示区域的高度。</summary>
        public double DigitHeight
        {
            get => _digitHeight;
            set
            {
                if (_digitHeight != value)
                {
                    _digitHeight = value;
                    OnPropertyChanged(nameof(DigitHeight));
                    GenerateDigits();
                    SetPosition(_currentValue);
                }
            }
        }

        /// <summary>获取或设置每个数字显示区域的宽度。</summary>
        public double DigitWidth
        {
            get => _digitWidth;
            set
            {
                if (_digitWidth != value)
                {
                    _digitWidth = value;
                    OnPropertyChanged(nameof(DigitWidth));
                    GenerateDigits();
                    SetPosition(_currentValue);
                }
            }
        }

        public NumberScroll()
        {
            InitializeComponent();
            GenerateDigits();
            SetPosition(0);
        }

        private void UpButton_Click(object sender, SW.RoutedEventArgs e)
        {
            if (!_isEditable) return;
            int newValue = _currentValue + 1;
            if (newValue > _maxValue)
            {
                newValue = 0;
            }
            CurrentValue = newValue;
        }

        private void DownButton_Click(object sender, SW.RoutedEventArgs e)
        {
            if (!_isEditable) return;
            int newValue = _currentValue - 1;
            if (newValue < 0)
            {
                newValue = _maxValue;
            }
            CurrentValue = newValue;
        }

        private void GenerateDigits()
        {
            NumbersCanvas.Children.Clear();
            _normalDigits.Clear();

            var foreground = new SWM.SolidColorBrush(SWM.Color.FromArgb(255, 17, 24, 39));

            for (int i = 0; i <= _maxValue; i++)
            {
                SWC.Border border = new SWC.Border
                {
                    Width = _digitWidth,
                    Height = _digitHeight,
                    Background = SWM.Brushes.Transparent
                };
                SWC.TextBlock tb = new SWC.TextBlock
                {
                    Text = i.ToString(),
                    FontSize = _fontSize,
                    FontWeight = SW.FontWeights.Bold,
                    Foreground = foreground,
                    TextAlignment = SW.TextAlignment.Center,
                    VerticalAlignment = SW.VerticalAlignment.Center,
                    HorizontalAlignment = SW.HorizontalAlignment.Center
                };
                border.Child = tb;
                SWC.Canvas.SetLeft(border, 0);
                SWC.Canvas.SetTop(border, i * _digitHeight);
                NumbersCanvas.Children.Add(border);
                _normalDigits.Add(tb);
            }

            _upperExtraDigit = new SWC.TextBlock
            {
                Text = _maxValue.ToString(),
                FontSize = _fontSize,
                FontWeight = SW.FontWeights.Bold,
                Foreground = foreground,
                TextAlignment = SW.TextAlignment.Center,
                VerticalAlignment = SW.VerticalAlignment.Center,
                HorizontalAlignment = SW.HorizontalAlignment.Center
            };
            SWC.Border upperBorder = new SWC.Border
            {
                Width = _digitWidth,
                Height = _digitHeight,
                Background = SWM.Brushes.Transparent,
                Child = _upperExtraDigit
            };
            SWC.Canvas.SetLeft(upperBorder, 0);
            SWC.Canvas.SetTop(upperBorder, -(_maxValue + 1) * _digitHeight);
            NumbersCanvas.Children.Add(upperBorder);

            _lowerExtraDigit = new SWC.TextBlock
            {
                Text = "0",
                FontSize = _fontSize,
                FontWeight = SW.FontWeights.Bold,
                Foreground = foreground,
                TextAlignment = SW.TextAlignment.Center,
                VerticalAlignment = SW.VerticalAlignment.Center,
                HorizontalAlignment = SW.HorizontalAlignment.Center
            };
            SWC.Border lowerBorder = new SWC.Border
            {
                Width = _digitWidth,
                Height = _digitHeight,
                Background = SWM.Brushes.Transparent,
                Child = _lowerExtraDigit
            };
            SWC.Canvas.SetLeft(lowerBorder, 0);
            SWC.Canvas.SetTop(lowerBorder, (_maxValue + 1) * _digitHeight);
            NumbersCanvas.Children.Add(lowerBorder);
        }

        private void SetPosition(int value)
        {
            int safeValue = Math.Clamp(value, 0, _maxValue);
            double offset = safeValue * -_digitHeight;
            SWM.TranslateTransform transform = new SWM.TranslateTransform(0, offset);
            NumbersCanvas.RenderTransform = transform;
        }

        private void AnimateToValue(int fromValue, int toValue)
        {
            bool isLoopUp = (fromValue == _maxValue && toValue == 0);
            bool isLoopDown = (fromValue == 0 && toValue == _maxValue);

            if (isLoopUp)
            {
                double fromOffset = fromValue * -_digitHeight;
                double toOffset = -(_maxValue + 1) * _digitHeight;
                AnimateWithSnap(fromOffset, toOffset, 0);
                return;
            }
            else if (isLoopDown)
            {
                double fromOffset = fromValue * -_digitHeight;
                double toOffset = _digitHeight;
                AnimateWithSnap(fromOffset, toOffset, _maxValue);
                return;
            }

            double startOffset = fromValue * -_digitHeight;
            double endOffset = toValue * -_digitHeight;
            AnimateWithoutSnap(startOffset, endOffset);
        }

        private void AnimateWithoutSnap(double fromOffset, double toOffset)
        {
            SWM.TranslateTransform transform = new SWM.TranslateTransform(0, fromOffset);
            NumbersCanvas.RenderTransform = transform;

            SWMA.DoubleAnimation animation = new SWMA.DoubleAnimation
            {
                To = toOffset,
                Duration = new SW.Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new SWMA.QuadraticEase { EasingMode = SWMA.EasingMode.EaseOut }
            };

            transform.BeginAnimation(SWM.TranslateTransform.YProperty, animation);
        }

        private void AnimateWithSnap(double fromOffset, double toOffset, int snapToValue)
        {
            SWM.TranslateTransform transform = new SWM.TranslateTransform(0, fromOffset);
            NumbersCanvas.RenderTransform = transform;

            SWMA.DoubleAnimation animation = new SWMA.DoubleAnimation
            {
                To = toOffset,
                Duration = new SW.Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new SWMA.QuadraticEase { EasingMode = SWMA.EasingMode.EaseOut }
            };

            int expectedVersion = _animationVersion;
            animation.Completed += (s, e) =>
            {
                if (_animationVersion != expectedVersion) return;
                SetPosition(snapToValue);
            };

            transform.BeginAnimation(SWM.TranslateTransform.YProperty, animation);
        }

        /// <summary>设置数值但不播放滚动动画。</summary>
        public void SetValueWithoutAnimation(int value)
        {
            int clampedValue = Math.Clamp(value, 0, _maxValue);
            _currentValue = clampedValue;
            SetPosition(clampedValue);
        }

        /// <summary>设置数值并播放滚动动画，可指定滚动方向。</summary>
        public void SetValueWithScroll(int value, ScrollDirection direction = ScrollDirection.Auto)
        {
            int newValue = Math.Clamp(value, 0, _maxValue);
            if (newValue == _currentValue) return;

            _animationVersion++;
            int oldValue = _currentValue;
            _currentValue = newValue;

            if (direction == ScrollDirection.Up)
            {
                AnimateWithDirection(oldValue, newValue, scrollUp: true);
            }
            else if (direction == ScrollDirection.Down)
            {
                AnimateWithDirection(oldValue, newValue, scrollUp: false);
            }
            else
            {
                AnimateToValue(oldValue, newValue);
            }

            ValueChanged?.Invoke(this, _currentValue);
        }

        /// <summary>按指定方向执行动画，必要时通过额外数字循环。</summary>
        private void AnimateWithDirection(int fromValue, int toValue, bool scrollUp)
        {
            if (scrollUp)
            {
                if (toValue >= fromValue)
                {
                    AnimateWithoutSnap(fromValue * -_digitHeight, toValue * -_digitHeight);
                }
                else
                {
                    // 需要向上循环：先滚到 MaxValue 再跳回 0 继续滚到目标
                    double fromOffset = fromValue * -_digitHeight;
                    double loopOffset = -(_maxValue + 1) * _digitHeight;

                    SWM.TranslateTransform transform = new SWM.TranslateTransform(0, fromOffset);
                    NumbersCanvas.RenderTransform = transform;

                    SWMA.DoubleAnimation anim1 = new SWMA.DoubleAnimation
                    {
                        To = loopOffset,
                        Duration = new SW.Duration(TimeSpan.FromMilliseconds(120)),
                        EasingFunction = new SWMA.QuadraticEase { EasingMode = SWMA.EasingMode.EaseOut }
                    };

                    int targetVal = toValue;
                    int expectedVersion = _animationVersion;
                    anim1.Completed += (s, e) =>
                    {
                        if (_animationVersion != expectedVersion) return;
                        SetPosition(0);
                        if (targetVal > 0)
                        {
                            SWM.TranslateTransform t2 = new SWM.TranslateTransform(0, 0);
                            NumbersCanvas.RenderTransform = t2;
                            SWMA.DoubleAnimation anim2 = new SWMA.DoubleAnimation
                            {
                                To = targetVal * -_digitHeight,
                                Duration = new SW.Duration(TimeSpan.FromMilliseconds(120)),
                                EasingFunction = new SWMA.QuadraticEase { EasingMode = SWMA.EasingMode.EaseOut }
                            };
                            t2.BeginAnimation(SWM.TranslateTransform.YProperty, anim2);
                        }
                    };

                    transform.BeginAnimation(SWM.TranslateTransform.YProperty, anim1);
                }
            }
            else // scroll down
            {
                if (toValue <= fromValue)
                {
                    AnimateWithoutSnap(fromValue * -_digitHeight, toValue * -_digitHeight);
                }
                else
                {
                    // 需要向下循环：先滚到 0 再跳回 MaxValue 继续滚到目标
                    double fromOffset = fromValue * -_digitHeight;
                    double loopOffset = _digitHeight;

                    SWM.TranslateTransform transform = new SWM.TranslateTransform(0, fromOffset);
                    NumbersCanvas.RenderTransform = transform;

                    SWMA.DoubleAnimation anim1 = new SWMA.DoubleAnimation
                    {
                        To = loopOffset,
                        Duration = new SW.Duration(TimeSpan.FromMilliseconds(120)),
                        EasingFunction = new SWMA.QuadraticEase { EasingMode = SWMA.EasingMode.EaseOut }
                    };

                    int targetVal = toValue;
                    int expectedVersion = _animationVersion;
                    anim1.Completed += (s, e) =>
                    {
                        if (_animationVersion != expectedVersion) return;
                        SetPosition(_maxValue);
                        if (targetVal < _maxValue)
                        {
                            SWM.TranslateTransform t2 = new SWM.TranslateTransform(0, _maxValue * -_digitHeight);
                            NumbersCanvas.RenderTransform = t2;
                            SWMA.DoubleAnimation anim2 = new SWMA.DoubleAnimation
                            {
                                To = targetVal * -_digitHeight,
                                Duration = new SW.Duration(TimeSpan.FromMilliseconds(120)),
                                EasingFunction = new SWMA.QuadraticEase { EasingMode = SWMA.EasingMode.EaseOut }
                            };
                            t2.BeginAnimation(SWM.TranslateTransform.YProperty, anim2);
                        }
                    };

                    transform.BeginAnimation(SWM.TranslateTransform.YProperty, anim1);
                }
            }
        }
    }
}

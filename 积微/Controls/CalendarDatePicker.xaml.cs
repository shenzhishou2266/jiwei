using System;
using System.Windows;
using System.Windows.Controls;

namespace 积微.Controls
{
    /// <summary>日历日期选择器控件，支持单选和范围选择模式。</summary>
    public partial class CalendarDatePicker : System.Windows.Controls.UserControl
    {
        private DateTime _currentMonth;
        private DateTime? _selectedDate;
        private DateTime? _rangeStart;
        private DateTime? _rangeEnd;
        private bool _isRangeMode;
        private bool _isCalendarVisible;

        /// <summary>已选日期的依赖属性。</summary>
        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register(nameof(SelectedDate), typeof(DateTime?), typeof(CalendarDatePicker),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateChanged));

        /// <summary>范围起始日期的依赖属性。</summary>
        public static readonly DependencyProperty RangeStartProperty =
            DependencyProperty.Register(nameof(RangeStart), typeof(DateTime?), typeof(CalendarDatePicker),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeStartChanged));

        /// <summary>范围结束日期的依赖属性。</summary>
        public static readonly DependencyProperty RangeEndProperty =
            DependencyProperty.Register(nameof(RangeEnd), typeof(DateTime?), typeof(CalendarDatePicker),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeEndChanged));

        /// <summary>范围选择模式的依赖属性。</summary>
        public static readonly DependencyProperty RangeModeProperty =
            DependencyProperty.Register(nameof(RangeMode), typeof(bool), typeof(CalendarDatePicker),
                new PropertyMetadata(false, OnRangeModeChanged));

        /// <summary>获取或设置当前选中的日期。</summary>
        public DateTime? SelectedDate
        {
            get => (DateTime?)GetValue(SelectedDateProperty);
            set => SetValue(SelectedDateProperty, value);
        }

        /// <summary>获取或设置范围选择的起始日期。</summary>
        public DateTime? RangeStart
        {
            get => (DateTime?)GetValue(RangeStartProperty);
            set => SetValue(RangeStartProperty, value);
        }

        /// <summary>获取或设置范围选择的结束日期。</summary>
        public DateTime? RangeEnd
        {
            get => (DateTime?)GetValue(RangeEndProperty);
            set => SetValue(RangeEndProperty, value);
        }

        /// <summary>获取或设置是否为范围选择模式。</summary>
        public bool RangeMode
        {
            get => (bool)GetValue(RangeModeProperty);
            set => SetValue(RangeModeProperty, value);
        }

        /// <summary>日期变化事件。</summary>
        public event EventHandler<DateTime?>? DateChanged;
        /// <summary>范围变化事件。</summary>
        public event EventHandler<(DateTime? Start, DateTime? End)>? RangeChanged;

        private static readonly string[] ChineseMonths = { "1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月" };

        public CalendarDatePicker()
        {
            InitializeComponent();
            _currentMonth = DateTime.Today;
            _selectedDate = null;
            _isRangeMode = false;
            _isCalendarVisible = false;
            UpdateDateDisplay();
            RenderCalendar();
            Loaded += CalendarDatePicker_Loaded;
        }

        private void CalendarDatePicker_Loaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.SizeChanged += Window_SizeChanged;
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isCalendarVisible)
            {
                UpdateCalendarPosition();
            }
        }

        private void UpdateDateDisplay()
        {
            if (_selectedDate.HasValue)
            {
                DateDisplayText.Text = _selectedDate.Value.ToString("yyyy年MM月dd日");
            }
            else
            {
                DateDisplayText.Text = "选择日期";
            }
        }

        private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CalendarDatePicker picker)
            {
                if (e.NewValue is DateTime newDate)
                {
                    picker._selectedDate = newDate;
                    picker._currentMonth = new DateTime(newDate.Year, newDate.Month, 1);
                    picker.UpdateDateDisplay();
                    picker.RenderCalendar();
                }
                else
                {
                    picker._selectedDate = null;
                    picker.UpdateDateDisplay();
                    picker.RenderCalendar();
                }
                picker.DateChanged?.Invoke(picker, e.NewValue as DateTime?);
            }
        }

        private static void OnRangeStartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CalendarDatePicker picker)
            {
                picker._rangeStart = e.NewValue as DateTime?;
                picker.RenderCalendar();
                picker.RangeChanged?.Invoke(picker, (picker._rangeStart, picker._rangeEnd));
            }
        }

        private static void OnRangeEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CalendarDatePicker picker)
            {
                picker._rangeEnd = e.NewValue as DateTime?;
                picker.RenderCalendar();
                picker.RangeChanged?.Invoke(picker, (picker._rangeStart, picker._rangeEnd));
            }
        }

        private static void OnRangeModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CalendarDatePicker picker)
            {
                picker._isRangeMode = (bool)e.NewValue;
                picker.RenderCalendar();
            }
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(-1);
            RenderCalendar();
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(1);
            RenderCalendar();
        }

        private void PickerButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleCalendar();
        }

        private void ToggleCalendar()
        {
            if (_isCalendarVisible)
            {
                HideCalendar();
            }
            else
            {
                ShowCalendar();
            }
        }

        private void ShowCalendar()
        {
            _isCalendarVisible = true;
            CalendarPopup.IsOpen = true;
            UpdateCalendarPosition();
        }

        private void UpdateCalendarPosition()
        {
            double buttonWidth = PickerButton.ActualWidth;
            if (buttonWidth <= 0)
                return;

            var window = Window.GetWindow(this);
            if (window == null)
                return;

            double calendarWidth = CalendarContainer.ActualWidth > 0 ? CalendarContainer.ActualWidth : 320;
            double windowWidth = window.ActualWidth;
            var controlPosInWindow = this.TranslatePoint(new System.Windows.Point(0, 0), window);

            double offsetX = 0;

            // 1. 优先左对齐
            if (controlPosInWindow.X + calendarWidth <= windowWidth)
            {
                offsetX = 0;
            }
            // 2. 右对齐
            else if (controlPosInWindow.X + buttonWidth - calendarWidth >= 0)
            {
                offsetX = buttonWidth - calendarWidth;
            }
            // 3. 都不行，贴窗口左边
            else
            {
                offsetX = -controlPosInWindow.X;
            }

            CalendarPopup.HorizontalOffset = offsetX;
        }

        private void HideCalendar()
        {
            _isCalendarVisible = false;
            CalendarPopup.IsOpen = false;
        }

        private void CalendarPopup_Closed(object sender, EventArgs e)
        {
            _isCalendarVisible = false;
        }

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _selectedDate = DateTime.Today;
            if (_isRangeMode)
            {
                if (_rangeStart == null)
                {
                    _rangeStart = DateTime.Today;
                    RangeStart = DateTime.Today;
                }
                else if (_rangeEnd == null)
                {
                    _rangeEnd = DateTime.Today;
                    RangeEnd = DateTime.Today;
                    if (_rangeStart > _rangeEnd)
                    {
                        var temp = _rangeStart;
                        _rangeStart = _rangeEnd;
                        _rangeEnd = temp;
                        RangeStart = _rangeStart;
                        RangeEnd = _rangeEnd;
                    }
                    HideCalendar();
                }
                else
                {
                    _rangeStart = DateTime.Today;
                    _rangeEnd = null;
                    RangeStart = DateTime.Today;
                    RangeEnd = null;
                }
            }
            else
            {
                SelectedDate = DateTime.Today;
                HideCalendar();
            }
            RenderCalendar();
        }

        private void Day_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.CommandParameter is DateTime date)
            {
                if (_isRangeMode)
                {
                    if (_rangeStart == null || (_rangeStart != null && _rangeEnd != null))
                    {
                        _rangeStart = date;
                        _rangeEnd = null;
                        RangeStart = date;
                        RangeEnd = null;
                    }
                    else
                    {
                        _rangeEnd = date;
                        if (_rangeStart > _rangeEnd)
                        {
                            var temp = _rangeStart;
                            _rangeStart = _rangeEnd;
                            _rangeEnd = temp;
                            RangeStart = _rangeStart;
                            RangeEnd = _rangeEnd;
                        }
                        else
                        {
                            RangeEnd = date;
                        }
                        HideCalendar();
                    }
                }
                else
                {
                    _selectedDate = date;
                    _currentMonth = new DateTime(date.Year, date.Month, 1);
                    SelectedDate = date;
                    HideCalendar();
                }
                RenderCalendar();
            }
        }

        private void RenderCalendar()
        {
            MonthText.Text = ChineseMonths[_currentMonth.Month - 1];
            YearText.Text = _currentMonth.Year.ToString();

            var today = DateTime.Today;
            var firstDayOfMonth = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);
            var startDayOfWeek = (int)firstDayOfMonth.DayOfWeek;

            for (int i = CalendarGrid.Children.Count - 1; i >= 7; i--)
            {
                CalendarGrid.Children.RemoveAt(i);
            }

            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(_currentMonth.Year, _currentMonth.Month, day);
                var button = new System.Windows.Controls.Button
                {
                    Content = day.ToString(),
                    Style = (Style)FindResource("DayButtonStyle"),
                    CommandParameter = date
                };

                bool isToday = date.Date == today.Date;
                bool isSelected = !_isRangeMode && _selectedDate.HasValue && date.Date == _selectedDate.Value.Date;

                if (isToday && isSelected)
                {
                    button.Tag = "TodaySelected";
                }
                else if (isToday)
                {
                    button.Tag = "Today";
                }
                else if (isSelected)
                {
                    button.Tag = "Selected";
                }

                if (_isRangeMode)
                {
                    bool isRangeStart = _rangeStart.HasValue && date.Date == _rangeStart.Value.Date;
                    bool isRangeEnd = _rangeEnd.HasValue && date.Date == _rangeEnd.Value.Date;

                    if (isRangeStart || isRangeEnd)
                    {
                        button.Tag = "Selected";
                    }
                    else if (_rangeStart.HasValue && _rangeEnd.HasValue)
                    {
                        if (date.Date > _rangeStart.Value.Date && date.Date < _rangeEnd.Value.Date)
                        {
                            button.Tag = "Range";
                        }
                    }
                }

                button.Click += Day_Click;

                int cellIndex = startDayOfWeek + day - 1;
                int row = cellIndex / 7 + 1;
                int col = cellIndex % 7;
                Grid.SetRow(button, row);
                Grid.SetColumn(button, col);
                CalendarGrid.Children.Add(button);
            }

            TodayButton.Visibility = Visibility.Visible;
        }

        /// <summary>设置当前选中日期并刷新日历显示。</summary>
        public void SetDate(DateTime date)
        {
            _selectedDate = date;
            _currentMonth = new DateTime(date.Year, date.Month, 1);
            SelectedDate = date;
            RenderCalendar();
        }

        /// <summary>设置日期范围并刷新日历显示。</summary>
        public void SetRange(DateTime start, DateTime end)
        {
            _rangeStart = start;
            _rangeEnd = end;
            _currentMonth = new DateTime(start.Year, start.Month, 1);
            RangeStart = start;
            RangeEnd = end;
            RenderCalendar();
        }

        /// <summary>清除所有选中的日期。</summary>
        public void Clear()
        {
            _selectedDate = null;
            _rangeStart = null;
            _rangeEnd = null;
            SelectedDate = null;
            RangeStart = null;
            RangeEnd = null;
            UpdateDateDisplay();
            RenderCalendar();
        }
    }
}

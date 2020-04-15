using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Xamarin.Forms.GenGridView
{
    public class GenGridView : View
    {
        public static readonly BindableProperty ItemAlignmentXProperty = BindableProperty.CreateAttached(
            "ItemAlignmentX", typeof(double),
            typeof(GenGridView), -1.0d,
            validateValue: (bindable, value) =>
                ((double) value >= 0.0d && (double) value <= 1.0d) || Math.Abs((double) value + 1.0d) < double.Epsilon);

        public static readonly BindableProperty ItemAlignmentYProperty = BindableProperty.CreateAttached(
            "ItemAlignmentY", typeof(double),
            typeof(GenGridView), -1.0d,
            validateValue: (bindable, value) =>
                ((double) value >= 0.0d && (double) value <= 1.0d) || Math.Abs((double) value + 1.0d) < double.Epsilon);

        public static readonly BindableProperty IsHorizontalProperty =
            BindableProperty.CreateAttached("IsHorizontal", typeof(bool),
                typeof(GenGridView), true);

        public static readonly BindableProperty SelectedItemProperty =
            BindableProperty.CreateAttached("SelectedItem", typeof(object),
                typeof(GenGridView), null, BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);

        public static readonly BindableProperty FocusedItemProperty =
            BindableProperty.CreateAttached("FocusedItem", typeof(object),
                typeof(GenGridView), null, BindingMode.Default, propertyChanged: OnFocusedItemChanged);

        public static readonly BindableProperty ControlModeProperty =
            BindableProperty.CreateAttached("ControlMode", typeof(ControlMode),
                typeof(GenGridView), ControlMode.Native, BindingMode.TwoWay);

        public static readonly BindableProperty IsHighlightProperty =
            BindableProperty.CreateAttached("IsHighlight", typeof(bool),
                typeof(GenGridView), true, BindingMode.TwoWay);

        public static readonly BindableProperty ItemsSourceProperty =
            BindableProperty.CreateAttached(nameof(ItemsSource), typeof(IEnumerable), typeof(GenGridView), null,
                BindingMode.Default, propertyChanged: OnItemsSourceChanged);

        public static readonly BindableProperty ItemTemplateProperty =
            BindableProperty.CreateAttached(nameof(ItemTemplate), typeof(DataTemplate), typeof(GenGridView), null,
                BindingMode.Default, propertyChanged: OnItemTemplateChanged);

        public static readonly BindableProperty HorizontalScrollBarVisibilityProperty =
            BindableProperty.CreateAttached(nameof(HorizontalScrollBarVisibility), typeof(ScrollBarVisibility),
                typeof(GenGridView),
                ScrollBarVisibility.Default);

        public ScrollBarVisibility HorizontalScrollBarVisibility
        {
            get => (ScrollBarVisibility) GetValue(HorizontalScrollBarVisibilityProperty);
            set => SetValue(HorizontalScrollBarVisibilityProperty, value);
        }

        public static readonly BindableProperty VerticalScrollBarVisibilityProperty =
            BindableProperty.CreateAttached(nameof(VerticalScrollBarVisibility), typeof(ScrollBarVisibility),
                typeof(ItemsView), ScrollBarVisibility.Default);

        public ScrollBarVisibility VerticalScrollBarVisibility
        {
            get => (ScrollBarVisibility) GetValue(VerticalScrollBarVisibilityProperty);
            set => SetValue(VerticalScrollBarVisibilityProperty, value);
        }

        private static void OnItemsSourceChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            ((GenGridView) bindable)._templatedItemsGenGridList.ItemsSource = (IEnumerable) newvalue;
        }

        private static void OnItemTemplateChanged(BindableObject bindable, object oldvalue, object newvalue)
        {
            ((GenGridView) bindable)._templatedItemsGenGridList.ItemTemplate = (DataTemplate) newvalue;
        }

        private static void OnSelectedItemChanged(BindableObject bindable, object oldValue, object newValue)
        {
            ((GenGridView) bindable).SelectedItemChanged?.Invoke(bindable,
                new SelectedItemChangedEventArgs(newValue, ((GenGridView) bindable).Items.IndexOf((View) newValue)));
        }

        private static void OnFocusedItemChanged(BindableObject bindable, object oldValue, object newValue)
        {
            ((GenGridView) bindable).FocusedItemChanged?.Invoke(bindable,
                new FocusedItemChangedEventArgs(newValue, ((GenGridView) bindable).Items.IndexOf((View) newValue)));
        }

        private readonly TemplatedItemsGenGridList _templatedItemsGenGridList = new TemplatedItemsGenGridList();

        public event NotifyCollectionChangedEventHandler ItemsChanged
        {
            add => _templatedItemsGenGridList.CollectionChanged += value;
            remove => _templatedItemsGenGridList.CollectionChanged -= value;
        }

        public ReadOnlyCollection<View> Items => _templatedItemsGenGridList.Items;

        public event EventHandler<SelectedItemChangedEventArgs> SelectedItemChanged;
        public event EventHandler<FocusedItemChangedEventArgs> FocusedItemChanged;
        public event EventHandler<ScrollToRequestEventArgs> ScrollToRequested;

        protected virtual void OnScrollToRequested(ScrollToRequestEventArgs e)
        {
            ScrollToRequested?.Invoke(this, e);
        }

        public void ScrollTo(int index, ScrollToPosition position = ScrollToPosition.Center, bool animate = true)
        {
            OnScrollToRequested(new ScrollToRequestEventArgs(index, 0, position, animate));
        }

        public void ScrollTo(object item, ScrollToPosition position = ScrollToPosition.Center, bool animate = true)
        {
            OnScrollToRequested(new ScrollToRequestEventArgs(item, 0, position, animate));
        }

        public IEnumerable ItemsSource
        {
            get => (IEnumerable) GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public DataTemplate ItemTemplate
        {
            get => (DataTemplate) GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        public bool IsHighlight
        {
            get => (bool) GetValue(IsHighlightProperty);
            set => SetValue(IsHighlightProperty, value);
        }

        public ControlMode ControlMode
        {
            get => (ControlMode) GetValue(ControlModeProperty);
            set => SetValue(ControlModeProperty, value);
        }

        public object FocusedItem
        {
            get => GetValue(FocusedItemProperty);
            set => SetValue(FocusedItemProperty, value);
        }

        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public bool IsHorizontal
        {
            get => (bool) GetValue(IsHorizontalProperty);
            set => SetValue(IsHorizontalProperty, value);
        }

        public double ItemAlignmentX
        {
            get => (double) GetValue(ItemAlignmentXProperty);
            set => SetValue(ItemAlignmentXProperty, value);
        }

        public double ItemAlignmentY
        {
            get => (double) GetValue(ItemAlignmentYProperty);
            set => SetValue(ItemAlignmentYProperty, value);
        }
    }
}
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using ElmSharp;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Tizen;
using EEntry = ElmSharp.Entry;
using Specific = Xamarin.Forms.PlatformConfiguration.TizenSpecific.Entry;
using Controls = Xamarin.Forms.GenGridView;

[assembly: ExportRenderer(typeof(Controls.GenGridView), typeof(Xamarin.Forms.GenGridView.Tizen.GenGridViewRenderer))]

namespace Xamarin.Forms.GenGridView.Tizen
{
    public class GenGridViewRenderer : ViewRenderer<Controls.GenGridView, GenGrid>
    {
        private IGenGridViewController _gridViewController;
        private IGenGridLayoutManager _genGridLayoutManager;

        public GenGridViewRenderer()
        {
            RegisterPropertyHandler(Controls.GenGridView.ItemAlignmentXProperty, UpdateItemAlignmentX);
            RegisterPropertyHandler(Controls.GenGridView.ItemAlignmentYProperty, UpdateItemAlignmentY);
            RegisterPropertyHandler(Controls.GenGridView.HorizontalScrollBarVisibilityProperty,
                UpdateHorizontalScrollBarVisibility);
            RegisterPropertyHandler(Controls.GenGridView.VerticalScrollBarVisibilityProperty,
                UpdateVerticalScrollBarVisibility);
            RegisterPropertyHandler(Controls.GenGridView.IsHorizontalProperty, UpdateIsHorizontal);
            RegisterPropertyHandler(Controls.GenGridView.ControlModeProperty, UpdateControlMode);
            RegisterPropertyHandler(Controls.GenGridView.IsHighlightProperty, UpdateIsHighlight);
        }

        private void UpdateIsHighlight()
        {
            Control.IsHighlight = Element.IsHighlight;
        }

        private void UpdateControlMode()
        {
            if (_gridViewController != null)
            {
                _gridViewController.ItemFocused -= UpdateFocusedItem;
                _gridViewController.ItemSelected -= UpdateSelectedItem;
                _gridViewController.Dispose();
            }

            switch (Element.ControlMode)
            {
                case ControlMode.Native:
                    Control.SelectionMode = GenItemSelectionMode.Default;
                    _gridViewController = new GenGridViewNativeController(Control);
                    break;
                case ControlMode.Custom:
                    Control.SelectionMode = GenItemSelectionMode.None;
                    _gridViewController = new GenGridViewCustomController(Control);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _gridViewController.Add(Element.Items);
            _gridViewController.ItemFocused += UpdateFocusedItem;
            _gridViewController.ItemSelected += UpdateSelectedItem;
        }

        protected virtual GenGrid CreateNativeControl()
        {
            var window = Forms.NativeParent;
            var grid = new GenGrid(window);
            return grid;
        }

        protected override void OnElementChanged(ElementChangedEventArgs<Controls.GenGridView> e)
        {
            if (Control == null)
            {
                SetNativeControl(CreateNativeControl());
                Control.SelectionMode = GenItemSelectionMode.None;
                Control.Changed += (sender, args) => { _genGridLayoutManager.LayoutItems(); };
                _genGridLayoutManager = new GenGridItemsLayoutManager(Control, Element);
            }

            if (e.OldElement != null)
            {
                e.OldElement.ItemsChanged -= TemplatedItemsOnCollectionChanged;
                e.OldElement.ScrollToRequested -= OnScrollToRequested;
                UpdateControl();
            }

            if (e.NewElement != null)
            {
                e.NewElement.ItemsChanged += TemplatedItemsOnCollectionChanged;
                e.NewElement.ScrollToRequested += OnScrollToRequested;
            }

            base.OnElementChanged(e);
        }

        private void OnScrollToRequested(object sender, ScrollToRequestEventArgs e)
        {
            if (e.Item != null)
                _gridViewController.ScrollTo((View) e.Item, e.ScrollToPosition, e.IsAnimated);
            else
                _gridViewController.ScrollTo(e.Index, e.ScrollToPosition, e.IsAnimated);
        }

        private void UpdateControl()
        {
            if (Control == null) return;
            _gridViewController.Reset();
            _gridViewController.Add(Element.Items);
        }

        private void TemplatedItemsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    _gridViewController.Add(e.NewItems);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    _gridViewController.Remove(e.OldItems);
                    break;
                case NotifyCollectionChangedAction.Reset:
                case NotifyCollectionChangedAction.Move:
                case NotifyCollectionChangedAction.Replace:
                    UpdateControl();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void UpdateSelectedItem(object sender, GenGridItemEventArgs args)
        {
            Element.SelectedItem = args.Item.Data;
        }

        private void UpdateFocusedItem(object sender, GenGridItemEventArgs args)
        {
            Element.FocusedItem = args.Item.Data;
        }

        private void UpdateIsHorizontal()
        {
            Control.IsHorizontal = Element.IsHorizontal;
        }

        private static ScrollBarVisiblePolicy VisiblePolicyMap(ScrollBarVisibility visibility)
        {
            ScrollBarVisiblePolicy result;
            switch (visibility)
            {
                case ScrollBarVisibility.Always:
                    result = ScrollBarVisiblePolicy.Visible;
                    break;
                case ScrollBarVisibility.Default:
                    result = ScrollBarVisiblePolicy.Auto;
                    break;
                case ScrollBarVisibility.Never:
                    result = ScrollBarVisiblePolicy.Invisible;
                    break;
                default:
                    throw new InvalidEnumArgumentException();
            }

            return result;
        }

        private void UpdateVerticalScrollBarVisibility()
        {
            Control.VerticalScrollBarVisiblePolicy = VisiblePolicyMap(Element.VerticalScrollBarVisibility);
        }

        private void UpdateHorizontalScrollBarVisibility()
        {
            Control.HorizontalScrollBarVisiblePolicy = VisiblePolicyMap(Element.HorizontalScrollBarVisibility);
        }

        private void UpdateItemAlignmentX()
        {
            Control.ItemAlignmentX = Element.ItemAlignmentX;
        }

        private void UpdateItemAlignmentY()
        {
            Control.ItemAlignmentY = Element.ItemAlignmentY;
        }
    }
}
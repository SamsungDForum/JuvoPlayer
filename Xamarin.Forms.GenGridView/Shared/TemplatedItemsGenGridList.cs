using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace Xamarin.Forms.GenGridView
{
    public class TemplatedItemsGenGridList : INotifyCollectionChanged
    {
        private DataTemplate _itemTemplate;
        private IEnumerable _itemsSource;
        private readonly ObservableList<View> _templatedList;
        public ReadOnlyCollection<View> Items => _templatedList.ToList().AsReadOnly();

        public TemplatedItemsGenGridList()
        {
            _templatedList = new ObservableList<View>();
            _templatedList.CollectionChanged += (sender, args) => { CollectionChanged?.Invoke(sender, args); };
        }

        public DataTemplate ItemTemplate
        {
            get => _itemTemplate;
            set
            {
                _itemTemplate = value;
                Update();
            }
        }

        private void Update()
        {
            if (_itemsSource == null || _itemTemplate == null) return;
            var tmpList = CreateTemplatedItems(_itemsSource);
            _templatedList.Clear();
            _templatedList.AddRange(tmpList);
        }

        public IEnumerable ItemsSource
        {
            get => _itemsSource;
            set
            {
                if (value is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += OnItemsSourceChanged;
                }

                if (_itemsSource is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= OnItemsSourceChanged;
                }
                _itemsSource = value;
                Update();
            }
        }

        private IEnumerable<View> CreateTemplatedItems(IEnumerable sources)
        {
            var tmpList = new List<View>();
            foreach (var source in sources)
            {
                var view = ((View) _itemTemplate.CreateContent());
                view.BindingContext = source;
                tmpList.Add(view);
            }
            return tmpList;
        }

        private void OnItemsSourceChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    _templatedList.AddRange(CreateTemplatedItems(e.NewItems));
                    break;
                case NotifyCollectionChangedAction.Remove:
                    _templatedList.RemoveAt(e.OldStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Reset:
                case NotifyCollectionChangedAction.Move:
                case NotifyCollectionChangedAction.Replace:
                    Update();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        public event NotifyCollectionChangedEventHandler CollectionChanged;
        
        private class ObservableList<T>
            : ObservableCollection<T>
        {
            public void AddRange(IEnumerable<T> range)
            {
                if (range == null)
                    throw new ArgumentNullException(nameof(range));

                var items = range.ToList();
                int index = Items.Count;
                foreach (T item in items)
                    Items.Add(item);

                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items, index));
            }
        }
    }
}
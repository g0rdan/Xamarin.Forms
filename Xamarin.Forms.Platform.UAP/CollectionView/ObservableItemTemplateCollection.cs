﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Xamarin.Forms.Platform.UWP
{
	internal class ObservableItemTemplateCollection : IReadOnlyList<ItemTemplateContext>, INotifyCollectionChanged
	{
		readonly IList _itemsSource;
		readonly DataTemplate _itemTemplate;
		readonly BindableObject _container;
		readonly double _itemHeight;
		readonly double _itemWidth;
		readonly Thickness _itemSpacing;
		readonly INotifyCollectionChanged _notifyCollectionChanged;

		public int Count => _itemsSource.Count;

		readonly List<ItemTemplateContext> _itemTemplateContexts;

		public ItemTemplateContext this[int index]
		{
			get
			{
				if (_itemTemplateContexts[index] == null)
				{
					_itemTemplateContexts[index] = new ItemTemplateContext(_itemTemplate, _itemsSource[index], _container, _itemHeight, _itemWidth, _itemSpacing);
				}

				return _itemTemplateContexts[index];
			}
		}

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public ObservableItemTemplateCollection(IList itemsSource, DataTemplate itemTemplate, BindableObject container, double? itemHeight = null, double? itemWidth = null, Thickness? itemSpacing = null)
		{
			if (!(itemsSource is INotifyCollectionChanged notifyCollectionChanged))
			{
				throw new ArgumentException($"{nameof(itemsSource)} must implement {nameof(INotifyCollectionChanged)}");
			}

			_notifyCollectionChanged = notifyCollectionChanged;

			_itemsSource = itemsSource;
			_itemTemplate = itemTemplate;
			_container = container;

			if (itemHeight.HasValue)
				_itemHeight = itemHeight.Value;

			if (itemWidth.HasValue)
				_itemWidth = itemWidth.Value;

			if (itemSpacing.HasValue)
				_itemSpacing = itemSpacing.Value;

			_itemTemplateContexts = new List<ItemTemplateContext>(_itemsSource.Count);

			for (int n = 0; n < _itemsSource.Count; n++)
			{
				_itemTemplateContexts.Add(null);
			}

			_notifyCollectionChanged.CollectionChanged += InnerCollectionChanged;
		}

		public void CleanUp()
		{
			_notifyCollectionChanged.CollectionChanged -= InnerCollectionChanged;
		}

		void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
		{
			CollectionChanged?.Invoke(this, args);
		}

		void InnerCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
		{
			switch (args.Action)
			{
				case NotifyCollectionChangedAction.Add:
					Add(args);
					break;
				case NotifyCollectionChangedAction.Move:
					Move(args);
					break;
				case NotifyCollectionChangedAction.Remove:
					Remove(args);
					break;
				case NotifyCollectionChangedAction.Replace:
					Replace(args);
					break;
				case NotifyCollectionChangedAction.Reset:
					Reset();
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		void Add(NotifyCollectionChangedEventArgs args)
		{
			var startIndex = args.NewStartingIndex > -1 ? args.NewStartingIndex : _itemsSource.IndexOf(args.NewItems[0]);

			var count = args.NewItems.Count;

			for(int n = 0; n < count; n++)
			{
				var itc = new ItemTemplateContext(_itemTemplate, args.NewItems[n], _container, _itemHeight, _itemWidth, _itemSpacing);
				_itemTemplateContexts.Insert(startIndex + n, itc);
 				OnCollectionChanged(new NotifyCollectionChangedEventArgs(args.Action, itc, startIndex + n));
			}
		}

		void Move(NotifyCollectionChangedEventArgs args)
		{
			var count = args.NewItems.Count;

			if (args.OldStartingIndex > args.NewStartingIndex)
			{
				for (int n = 0; n < count; n++)
				{
					var toMove = _itemTemplateContexts[args.OldStartingIndex + n];
					_itemTemplateContexts.RemoveAt(args.OldStartingIndex + n);
					_itemTemplateContexts.Insert(args.NewStartingIndex + n, toMove);
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, 
						args.NewStartingIndex + n, args.OldStartingIndex + n));
				}

				return;
			}

			for(int n = count - 1; n >= 0; n--)
			{
				//Move(args.OldStartingIndex + n, args.NewStartingIndex + n);

				var toMove = _itemTemplateContexts[args.OldStartingIndex + n];
				_itemTemplateContexts.RemoveAt(args.OldStartingIndex + n);
				_itemTemplateContexts.Insert(args.NewStartingIndex + n, toMove);
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move,
					toMove, args.NewStartingIndex + n, args.OldStartingIndex + n));
			}
		}

		void Remove(NotifyCollectionChangedEventArgs args)
		{
			var startIndex = args.OldStartingIndex;

			if (startIndex < 0)
			{
				// INCC implementation isn't giving us enough information to know where the removed items were in the
				// collection. So the best we can do is a full Reset.
				Reset();
				return;
			}

			var count = args.OldItems.Count;

			for(int n = startIndex + count - 1; n >= startIndex; n--)
			{
				//RemoveAt(n);
				var toRemove = this[n];
				_itemTemplateContexts.RemoveAt(n);

				//OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
				//	toRemove, n));

				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
					toRemove, n));
			}
		}

		void Replace(NotifyCollectionChangedEventArgs args)
		{
			var newItemCount = args.NewItems.Count;

			if (newItemCount == args.OldItems.Count)
			{
				for (int n = 0; n < newItemCount; n++)
				{
					var index = args.OldStartingIndex + n;
					var oldItem = this[index];
					var newItem = new ItemTemplateContext(_itemTemplate, args.NewItems[n], _container, _itemHeight, _itemWidth, _itemSpacing);
					_itemTemplateContexts[index] = newItem;
					var update = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, newItem, oldItem, index);
					OnCollectionChanged(update);
				}
			}
			else
			{
				// If we're replacing one set with an equal size set, we can do a soft reset; if not, we have to completely
				// rebuild the collection
				Reset();
			}
		}

		void Reset()
		{
			_itemTemplateContexts.Clear();
			_itemTemplateContexts.Capacity = _itemsSource.Count;

			for (int n = 0; n < _itemsSource.Count; n++)
			{
				_itemTemplateContexts.Add(null);
			}

			var reset = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
			OnCollectionChanged(reset);
		}

		public IEnumerator<ItemTemplateContext> GetEnumerator()
		{
			return _itemTemplateContexts.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return _itemTemplateContexts.GetEnumerator();
		}
	}
}